---
title: "WPF — Rendimiento de Efectos (DropShadowEffect) y Niveles de Renderizado"
tags:
  - referencia
  - wpf
  - rendering
  - rendimiento
  - gpu
  - dropshadoweffect
date: 2026-06-25
---

# WPF — Rendimiento de Efectos y Niveles de Renderizado

Referencia técnica nacida de un caso real: la app iba fluida en una PC pero con tirones, mouse lento y memoria oscilante en otra. Ver ejecución en [[Sesión 2026-06-25 - Optimización de Rendimiento en Modales (DropShadowEffect)]].

---

## El problema en una frase

WPF compone la UI en la **GPU**. Los efectos `DropShadowEffect` / `BlurEffect` son **pixel shaders** (de lo más caro del pipeline). Abusar de ellos —sobre todo con `BlurRadius` grande— hace que la app dependa de la potencia de la GPU de cada máquina. En GPUs integradas débiles aparecen tirones, input lag y vaivén de memoria.

---

## Por qué un `BlurRadius` alto es tan caro

- Un `BlurRadius="100"` significa que **cada píxel de salida promedia un vecindario de ~200 px**. Es una convolución enorme.
- WPF debe (1) renderizar el subárbol afectado en una **textura intermedia** y (2) correr el shader de desenfoque sobre ella.
- Un valor de **12–24 px se ve idéntico** a simple vista. 80–100 es desperdicio puro.

> [!tip] Regla práctica
> `BlurRadius` de sombras de UI: **≤ 24**. Sombras de inputs/botones pequeños: **≤ 8** o ninguna.

---

## El mecanismo del "mouse lento al hacer hover" (clave)

Si la sombra grande está en el **borde exterior** que envuelve todo un modal/tarjeta:

1. Pasás el mouse sobre **cualquier** hijo (botón, fila) → su trigger `IsMouseOver` cambia el fondo.
2. Eso **invalida** ese elemento.
3. Como es hijo del borde con la sombra, **WPF re-rasteriza todo el contenido y vuelve a aplicar el desenfoque** en cada movimiento.
4. En GPU débil eso satura, el hilo de UI espera → **el mouse se siente pegajoso**.
5. Las texturas intermedias creadas/descartadas en cada frame → **la RAM sube y baja** (no es fuga; es el GC limpiando buffers temporales).

**Multiplicadores del costo:**
- Cambiar el `Effect` dentro de un `Trigger` (ej. agrandar la sombra al enfocar) → fuerza re-render del efecto en cada foco.
- Efectos anidados (sombra del contenedor + sombra de cada hijo) → superficies intermedias multiplicadas.

---

## Niveles de renderizado de WPF (RenderCapability.Tier)

Fuente: [Graphics Rendering Tiers — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/graphics-rendering-tiers)

| Tier | Significado |
|---|---|
| **0** | Sin aceleración por hardware. **Todo en software (CPU)**. DirectX < 9. |
| **1** | Algunas funciones aceleradas. DirectX ≥ 9, VRAM ≥ 60 MB, pixel shader ≥ 2.0. |
| **2** | La mayoría acelerada. DirectX ≥ 9, VRAM ≥ 120 MB, vertex shader ≥ 2.0, ≥ 4 unidades de textura. |

- `DropShadowEffect` y `BlurEffect` se aceleran **solo en Tier 1/2** con pixel shader 2.0+. En Tier 0 son **100% CPU** y lentísimos.
- Riesgo en GPU integrada: comparte la RAM del sistema como VRAM. Según Microsoft, *"cualquier operación cuyo requerimiento de VRAM exceda la memoria de la GPU"* **pierde la aceleración y cae a software**. Texturas de sombra gigantes pueden disparar ese fallback.

### Cómo diagnosticar (sin tocar código)
- **Administrador de tareas → Rendimiento → GPU** y columna **GPU** en Detalles. Abrir un modal y mover el mouse:
  - sube la **GPU** → acelerado, pero la iGPU no da abasto.
  - sube la **CPU** y la GPU casi no → está renderizando en **software** (peor caso).
- `dxdiag` → pestaña Pantalla → versión/fecha del driver Intel. Drivers viejos degradan o desactivan la aceleración de WPF.
- En código (cuando se quiera medir): `System.Windows.Media.RenderCapability.Tier >> 16`.

---

## GPU integrada: por qué una PC sufre y otra no

| | PC fluida | PC con problemas |
|---|---|---|
| CPU | i5-12500H (12ª gen) | i7-10750H (10ª gen) |
| **iGPU** | **Iris Xe (~80 EUs)** | **UHD 630 (~24 EUs)** |
| RAM | 24 GB DDR4-3200 | 16 GB DDR4-2933 |

Aunque se llame "i7", el de 10ª gen tiene una iGPU **~3–4× más débil**. El cuello de botella de los efectos WPF es la **GPU**, no la CPU.

---

## El mito de la versión de .NET

- El **número de SDK** (ej. 10.0.300 vs 9.0.315) **no determina** cómo se ejecuta la app. Lo que importa es el **Desktop Runtime** que carga el proceso WPF.
- Una app `net8.0-windows` necesita el **Runtime de .NET 8**; si está instalado, dos PCs corren el **mismo WPF** sin importar el SDK. Por defecto una app net8.0 **no** salta a .NET 10 ([roll-forward](https://learn.microsoft.com/en-us/dotnet/core/versions/selection)).
- Las librerías (`CommunityToolkit.Mvvm`, `Supabase`, `Serilog`, `MS.DI`) son agnósticas al runtime: **no** hay incompatibilidad por versión de .NET ni por CPU.
- Único matiz secundario: si la app corriera sobre .NET 9/10, el GC activa **DATAS** por defecto → más colecciones Gen0/Gen1 y memoria que "sube y baja" más visible ([DATAS — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/datas)). Es **cosmético**; **no** causa el input lag. El lag es 100% renderizado.

> [!warning] Conclusión
> Tirones + mouse lento + memoria oscilante = **renderizado de efectos × GPU débil**, no la versión de .NET ni una librería.

---

## Buenas prácticas (checklist)

- [ ] `BlurRadius` de contenedores ≤ 24; eliminar sombras de inputs.
- [ ] No cambiar `Effect` dentro de `Trigger` (usar cambio de `BorderBrush`/`Background`, que es gratis).
- [ ] Evitar efectos anidados (sombra del padre + sombra de cada hijo).
- [ ] Para sombras **estáticas**, considerar `BitmapCache` / `RenderOptions.CachingHint="Cache"` → el desenfoque se rasteriza **una vez**, no por frame.
- [ ] **No** forzar `RenderOptions.ProcessRenderMode = SoftwareOnly` (empeora: los efectos en software cuestan aún más).
- [ ] Mantener `UseLayoutRounding`, `TextFormattingMode=Display` (ya aplicado, ver [[WPF - DPI Awareness y Escalado Multi-Resolución]]).

---

## Relaciones

- [[Sesión 2026-06-25 - Optimización de Rendimiento en Modales (DropShadowEffect)]] — ejecución del fix
- [[Sesión 2026-09-08 - Salto de columnas al scrollear y sombras sobre listas virtualizadas]] — sombra de contenedor desacoplada en 9 grillas con scroll
- [[WPF - Esqueleto con Shimmer (Skeleton Loading)]] — qué propiedades sí se pueden animar barato
- [[WPF - DPI Awareness y Escalado Multi-Resolución]]
- [[WPF - Texto Dentado en Fondos de Color]]
- [[Arquitectura Actual]]
