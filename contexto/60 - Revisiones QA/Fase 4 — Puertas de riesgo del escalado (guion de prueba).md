---
title: "Fase 4 — Puertas de riesgo del escalado (guion de prueba)"
tags:
  - qa
  - escalado
  - wpf
  - rendimiento
  - windowchrome
date: 2026-09-20
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente)
revisor: Fernando
estado: Absorbido por la matriz de la Fase 7
---

# Fase 4 — Puertas de riesgo del escalado (guion de prueba)

> [!info] Este guion quedó absorbido
> Sus dos puertas pendientes (rendimiento en Pesajes, y el clic bajo la barra de título) están dentro
> de **[[Fase 7 — Matriz de prueba visual del escalado]]**, que es una sola pasada por la app en vez
> de tres. Ir directo a esa. Este archivo queda como registro de por qué cada puerta existía.

> [!warning] Por qué esto va antes de construir la pantalla
> Si alguna de estas tres se cae, cambia el diseño del escalado. Hacerlas ahora evita tirar a la
> basura la pantalla de *Mi Usuario*.

---

## Cómo forzar un factor sin pantalla de preferencias

No hace falta tocar la base ni loguearse dos veces. Dos formas, la primera gana:

**Variable de entorno** (una corrida, sin editar archivos):

```powershell
$env:UI_ESCALA = "0.8"; dotnet run --project CapaUI
```

**`CapaUI/App.config`** (persiste entre corridas):

```xml
<add key="UI_ESCALA" value="0.8" />
```

Valores válidos: 0.70 a 1.30 en pasos de 0.05. Fuera de rango se recorta; vacío o ausente usa la
preferencia guardada. Mientras haya un factor forzado la app **no** deja guardar uno nuevo, para que
no convivan dos fuentes de verdad. Queda registrado en el log como
`[EscalaService] Factor forzado por UI_ESCALA`.

> La ventana de login **siempre** va a 1.0 por diseño. La escala se aplica al entrar.

---

## Puerta 1 — Sombras y rendimiento en Pesajes

**Por qué acá:** `PesajeView` concentra 6 `DropShadowEffect`, 3 `DataGrid` y `RowHeight` fijo (40, 46,
40). Un efecto rasteriza a la resolución que impone el transform, y la regla 12 de `AGENTS.md`
(*Zero-Shader Layout*) existe porque esto ya fue un problema antes.

| # | Paso | Pasa si |
|:--|:--|:--|
| 1.1 | Abrir Pesajes con `UI_ESCALA = 0.75` | Entra sin demora perceptible respecto de 1.0 |
| 1.2 | Scrollear las tres grillas de arriba abajo, rápido | Sin tirones ni filas que se dibujan a destiempo |
| 1.3 | Abrir `PesajeModal` y mover el panel lateral | El gráfico de pesadas responde igual que a 1.0 |
| 1.4 | Mirar los bordes de las tarjetas con sombra | Sin halos sucios ni bandas; la sombra se ve pareja |
| 1.5 | Repetir 1.1 a 1.4 con `UI_ESCALA = 1.1` | Igual |

**Si se cae:** se achica el rango (por ejemplo 0,85–1,15) o se revisan las sombras de esa pantalla.
No invalida el mecanismo.

---

## Puerta 2 — `{DynamicResource}` en el `Setter.Value` del popup

**Ya resuelta, no hace falta probarla a mano.** Verificada el 2026-09-20 con un arnés WPF aparte:
funciona y además sigue los cambios en vivo. El `ScaleTransform` de un `Setter` **no** queda
congelado. Detalle y mediciones en
[[WPF - Escalar Popups y ToolTips que no heredan el LayoutTransform]].

Consecuencia: la Fase 6 usa un solo setter en el `PART_Popup` del `ControlTemplate` del `ComboBox`
y el plan B (recorrer popups por código) se descarta.

---

## Puerta 3 — La barra de título y el clic que arrastra

**Por qué:** `WindowChrome.CaptionHeight` vale 56 en `MainWindow` y coincide con la fila de la barra
superior, pero se expresa en coordenadas de la ventana y **no** lo alcanza el `LayoutTransform`. A
0,8× la barra pasa a medir 44,8 px visuales mientras Windows sigue tratando la franja 0–56 como área
de título.

**Ya lleva corrección preventiva:** `EscalaService` escala `CaptionHeight` junto con el contenido.
Esta prueba confirma que la corrección alcanza. `ResizeBorderThickness` queda sin escalar a
propósito — es zona de agarre del mouse, no diseño.

| # | Paso | Pasa si |
|:--|:--|:--|
| 3.1 | Con `UI_ESCALA = 0.75`, clic sostenido **justo debajo** de la barra superior, sobre el borde del sidebar, y mover el mouse | La ventana **no** se arrastra |
| 3.2 | Clic en el primer ítem del sidebar (Usuarios) | Abre el submenú, no arrastra |
| 3.3 | Clic en el ícono de notificaciones y en el de configuración | Responden normal |
| 3.4 | Arrastrar desde la barra superior (zona vacía) | La ventana **sí** se mueve |
| 3.5 | Doble clic en la barra superior | Maximiza/restaura |
| 3.6 | Repetir 3.1 a 3.5 con `UI_ESCALA = 1.2` | Igual |

**Si se cae:** hay que llevar `CaptionHeight` a un valor calculado desde la fila real en vez del
declarado, o mover la fila superior fuera del área de caption.

---

## Extra de bajo costo, ya que la app está abierta

| # | Paso | Pasa si |
|:--|:--|:--|
| E.1 | A 0,75×, abrir el detalle de una notificación | La ventana abre **escalada**, no a 1.0 |
| E.2 | A 1,1×, abrir el mismo detalle | El contenido entra; no queda texto cortado a la derecha |
| E.3 | A 0,75× y 1,1×, abrir cualquier `ComboBox` | El desplegable queda a 1.0 — **es lo esperado hasta la Fase 6**, anotar y seguir |
| E.4 | A 0,75×, achicar la ventana hasta el tope | Frena antes de que el contenido se recorte |
| E.5 | Abrir Roles y el modal de rol | Abre a 1.0 — **esperado hasta la Fase 5**, que lo convierte en `UserControl` |

---

## Resultado

| Puerta | Veredicto | Notas |
|:--|:--|:--|
| 1 — Sombras y rendimiento | | |
| 2 — Popup y `Freezable` | ✅ Verificada en arnés | Sin intervención manual |
| 3 — Barra de título | | |

---

## Relaciones

- [[Inventario — Superficie de escalado propio (LayoutTransform global)]]
- [[WPF - Escalar Popups y ToolTips que no heredan el LayoutTransform]]
- [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]]
- [[Convenciones de UI (WPF) — leer antes de tocar XAML]]
