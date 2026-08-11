---
title: "Sesión 2026-08-11 — Rediseño de Gestión de Roles y esqueleto con shimmer"
tags:
  - sesion
  - bimbo
  - rbac
  - wpf
  - rendimiento
date: 2026-08-11
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude
---

# Sesión 2026-08-11 — Rediseño de Gestión de Roles y esqueleto con shimmer

> [!success] Resultado
> La pantalla de Roles pasó de tardar ~3 s en blanco a cargar de una, y se rehízo por completo siguiendo un mockup externo. En el camino quedaron un panel de layout reutilizable, un ADR sobre la estrategia de precarga y una referencia técnica del efecto de carga.

---

## Problema / motivo

Tres cosas encadenadas:

1. **Lentitud**: cada entrada a Roles esperaba ~3 s en blanco.
2. **Maquetado**: había que traducir a XAML un rediseño hecho en React (`wpf-roles/`), fiel al original.
3. **Efecto de carga**: replicar el esqueleto animado que usan otras apps, sin cargarle CPU a la máquina más débil del equipo.

---

## Cambios aplicados

### Rendimiento y acceso a datos — ver [[ADR-014 - Precarga unica y cache del catalogo RBAC]]

- `CapaAplicacion4/Usuarios/Dtos/RolesResumenDto.cs` **(nuevo)** — bundle de precarga: roles + catálogo + asignaciones de todos los roles + conteo de usuarios.
- `CapaAplicacion4/Usuarios/Interfaces/IRolPermisoRepository.cs` — se agregó `ObtenerResumenAsync`.
- `CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs` — implementación; el catálogo se extrajo a `CargarCatalogoAsync` con caché estática + `SemaphoreSlim`; la validación de permiso se unificó en `ExigirLectura()`.
- `CapaDatos/Repositories/Usuarios/RolRepository.cs` — misma caché estática para la lista de roles.

**Efecto neto:** cambiar de rol dejó de tocar la red. Solo mueve un booleano en objetos que ya están en pantalla.

### Componentes reutilizables (sirven fuera de Roles)

- `CapaUI/Core/Controls/SpanningGridPanel.cs` **(nuevo)** — panel de columnas uniformes **con soporte de span**, el equivalente WPF de `grid-column: span 2`. Existe porque `UniformGrid` no permite span y fuerza celdas iguales, y `WrapPanel` no estira los elementos de una fila a la misma altura. Empaquetado voraz, una sola pasada compartida entre medir y arreglar.
- `CapaUI/Converters/IconoModuloConverter.cs` **(nuevo)** — clave de módulo → `Geometry`, parseadas una vez y congeladas.
- `CapaUI/Converters/ColorHexABrushConverter.cs` **(nuevo)** — hex → `Brush` congelado, con caché por color.

### Pantalla de Roles (reescritura completa)

- `RolesResources.xaml` **(nuevo)** — todo el sistema visual en un solo lugar: paleta, geometrías y plantillas. Los `Freezable` llevan `po:Freeze="True"` para compartirse en vez de clonarse por elemento.
- `RolesView.xaml` / `.xaml.cs` / `RolesViewModel.cs` — reescritos.

Decisiones de implementación que importan más que el estilo:

- **Los ~28 `AccionItemVm` se crean una sola vez.** Cambiar de rol no recrea la colección ni el árbol visual.
- **Las filas de permiso son `Button` con comando, no `CheckBox`.** Así el ViewModel mantiene los contadores exactos sin suscribirse al `PropertyChanged` de 28 objetos — 28 suscripciones que además habría que desenganchar al salir.
- **`RolesViewModel : IDisposable`** con `CancellationTokenSource`; la vista desengancha el evento `Toast`, para los timers, hace `Dispose()` y anula el `DataContext` en `Unloaded`. Mismo patrón que `CategoriasView`.
- Estado **draft vs. guardado** para contar cambios sin aplicar y poder descartar.

### Esqueleto con shimmer — ver [[WPF - Esqueleto con Shimmer (Skeleton Loading)]]

Primera pantalla del proyecto con el efecto. Cuatro decisiones de costo:

1. **Un solo `Rectangle`** de brillo sobre todas las tarjetas, no uno por caja (1 animación en vez de ~24).
2. Se anima **`Brush.RelativeTransform`** (espacio 0..1): no invalida layout y no hace falta conocer el ancho ni bindear a `ActualWidth`.
3. **`Timeline.DesiredFrameRate = 20`** en vez de los ~60 por defecto.
4. Se **frena** al ocultarse el esqueleto y en `Unloaded` — WPF no detiene animaciones de elementos colapsados y un `RepeatBehavior.Forever` sin parar deja viva la referencia.

Además, `DuracionMinimaEsqueletoMs = 1000`: con el catálogo cacheado la respuesta vuelve en milisegundos y el esqueleto alcanzaba a **parpadear**. Es un retraso deliberado — el intercambio está anotado en la referencia, y basta bajar la constante a 0 para desactivarlo.

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores**. Las 46 advertencias son las preexistentes de nulabilidad (CS8603/8604/8618/8826); **0 en los archivos nuevos**.
- El build completo de la solución falla con `MSB3027`/`MSB3021` **solo por archivos bloqueados** (Visual Studio y la app abiertos), no por código. Para verificar sin ese ruido: compilar con `-p:BaseOutputPath` a un directorio temporal.

> [!warning] Sin verificación visual en runtime
> El maquetado **no se vio corriendo** en esta sesión: la app estaba abierta bloqueando los DLL de salida. Se comprobó que compila y que los BAML se generan, nada más.

---

## Lo que NO cambió

- No se tocó el esquema de Supabase, ni migraciones, ni políticas RLS.
- No se tocó `Permiso` / `PermisoCatalogo` / `SesionPermisos`: el contrato de las 28 acciones sigue igual.
- No se implementó invalidación de las cachés estáticas — hoy no hay CRUD de roles ni del catálogo que la necesite (ver consecuencias del ADR-014).
- No se hicieron commits ni push.

---

## Pendiente

- **Medir el shimmer en la PC con UHD 630** (la del caso de [[WPF - Rendimiento de Efectos y Niveles de Renderizado]]). Hasta entonces la referencia queda en `lifecycle: draft`. Qué mirar: el pico de CPU/GPU debe volver a línea base al aparecer las tarjetas; si queda elevado, el shimmer no se frenó.
- Revisar el maquetado corriendo y ajustar si algo no cae como el mockup.

---

## Relaciones

- [[ADR-014 - Precarga unica y cache del catalogo RBAC]] — la decisión de fondo
- [[WPF - Esqueleto con Shimmer (Skeleton Loading)]] — la técnica, con sus palancas de costo
- [[Sesión 2026-08-09 - Implementación RBAC visual y gestión de roles]] — la versión anterior de esta pantalla
- [[Módulo Usuarios]]
- [[Arquitectura Actual]]
