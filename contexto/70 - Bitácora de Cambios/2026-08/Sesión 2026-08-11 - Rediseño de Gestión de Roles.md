---
title: "Sesión 2026-08-11 — Rediseño de Gestión de Roles"
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

# Sesión 2026-08-11 — Rediseño de Gestión de Roles

> [!success] Resultado
> La pantalla de Roles pasó de tardar ~3 s en blanco a cargar de una, y se rehízo por completo siguiendo un mockup externo. Quedaron un panel de layout reutilizable y un ADR sobre la estrategia de precarga.
>
> El esqueleto con shimmer se implementó y **se revirtió el mismo día** — ver la sección de reversión.

---

## Problema / motivo

Tres cosas encadenadas:

1. **Lentitud**: cada entrada a Roles esperaba ~3 s en blanco.
2. **Maquetado**: había que traducir a XAML un rediseño hecho en React (`wpf-roles/`), fiel al original.
3. **Efecto de carga**: se intentó replicar el esqueleto animado de otras apps; terminó descartado (ver abajo).

---

## Cambios aplicados

### Rendimiento y acceso a datos — ver [[ADR-014 - Precarga unica y cache del catalogo RBAC]]

- `CapaAplicacion4/Usuarios/Dtos/RolesResumenDto.cs` **(nuevo)** — bundle de precarga: roles + catálogo + asignaciones de todos los roles + conteo de usuarios.
- `CapaAplicacion4/Usuarios/Interfaces/IRolPermisoRepository.cs` — se agregó `ObtenerResumenAsync`.
- `CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs` — implementación; el catálogo se extrajo a `CargarCatalogoAsync` con caché estática + `SemaphoreSlim`; la validación de permiso se unificó en `ExigirLectura()`.
- `CapaDatos/Repositories/Usuarios/RolRepository.cs` — misma caché estática para la lista de roles.

**Efecto neto:** cambiar de rol dejó de tocar la red. Solo mueve un booleano en objetos que ya están en pantalla.

### Componentes reutilizables (sirven fuera de Roles)

- `CapaUI/Core/Controls/SpanningGridPanel.cs` **(nuevo)** — panel de columnas uniformes **con soporte de span**, el equivalente WPF de `grid-column: span 2`. Existe porque `UniformGrid` no permite span y fuerza celdas iguales, y `WrapPanel` no estira los elementos de una fila a la misma altura. Empaquetado voraz, una sola pasada compartida entre medir y arreglar. Incluye `MinColumnWidth` para el corte de responsividad (ver más abajo).
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

### Responsividad con tope (`MinColumnWidth`)

Al angostar la ventana las tarjetas se encogían hasta tapar el texto. Se agregó un **piso de ancho por columna**: fluido hasta ahí, y a partir de ese punto aparece una barra horizontal en vez de seguir aplastando.

- `SpanningGridPanel.MinColumnWidth` (DP nueva, `AffectsMeasure`). En Roles vale **220** (grilla de tarjetas y esqueleto).
- `MeasureOverride` ahora reporta **el ancho que el panel necesita, no el que le ofrecieron**. Cuando ese ancho supera al disponible, el `ScrollViewer` contenedor lo detecta y saca la barra sola.
- `RolesView.xaml`: el `ScrollViewer` del cuerpo pasó de `HorizontalScrollBarVisibility="Disabled"` a `"Auto"`.

> [!important] Dentro de un ScrollViewer hay que medir dos veces
> Con scroll horizontal habilitado, el `ScrollViewer` mide su contenido con **ancho infinito** y recién lo arregla con el ancho real. El panel resuelve las dos puntas:
>
> 1. Ancho infinito significa "sin restricción" → se usa `MinColumnWidth`.
> 2. La pasada de **arreglo vuelve a medir** a los hijos con el ancho definitivo. Sin eso, el texto quedaría recortado según el ancho equivocado (el mínimo) aunque en pantalla sobrara lugar. Medir con la misma restricción es un no-op para WPF, así que no cuesta nada cuando los anchos coinciden.

La barra de herramientas y el pie quedan **fuera** de ese scroll: al angostar mucho, las tarjetas se desplazan por debajo de un encabezado fijo.

---

### Esqueleto con shimmer — implementado y revertido el mismo día

Se implementó el efecto completo (esqueleto + brillo animado), primero en Roles y después extraído a un control `ShimmerPresenter` reutilizable que también se aplicó a Productos, reemplazando ahí el spinner.

**Se revirtió entero** a pedido del usuario: en su máquina producía artefactos visuales raros y lentitud. Se volvió al spinner con leyenda, que es lo que usan el resto de los formularios.

Qué se borró: `ShimmerPresenter.cs`, `Resources/Skeleton.xaml` (y su merge en `App.xaml`), el estilo `RolEsqueletoTarjeta` y los pinceles de esqueleto de `RolesResources.xaml`, el estilo `SkeletonFilaProducto`, y `DuracionMinimaEsqueletoMs` / `SostenerEsqueletoAsync` / `MostrarEsqueleto` del `RolesViewModel` (ese retraso artificial de 1 s ya no existe).

Qué quedó en su lugar:

- **Productos** volvió exactamente a su spinner original (`LoadingPanel` + `SpinnerPath` + `IniciarSpinner`/`DetenerSpinner`). Sin cambios netos respecto de como estaba al empezar la sesión.
- **Roles** ganó el mismo spinner con la leyenda "Cargando roles…", siguiendo el patrón de `ProductosView`/`CategoriasView`.

> [!important] El bug de "se cargaban dos diseños a la vez"
> El usuario reportó que en Roles se veían dos maquetados encimados. La causa: el esqueleto y el contenido real convivían superpuestos en un `Grid`, cada uno con su `Visibility` bindeada al ViewModel. **Mientras el `DataContext` es null los dos bindings fallan y ambas capas quedan con su valor por defecto — visibles.**
>
> Por eso el resto de los formularios del proyecto **no bindean la visibilidad del panel de carga**: la manejan desde el code-behind en `ActualizarCarga()`. Ese patrón no tiene el hueco del arranque. Roles ahora lo sigue.

El análisis técnico del efecto queda archivado en [[WPF - Esqueleto con Shimmer (Skeleton Loading)]] con la sección "Por qué se revirtió", por si alguien lo reintenta.

### Bug de concurrencia: `NullReferenceException` al abrir y cerrar rápido

El usuario reportó que `UsuariosView.PoblarRoles()` explotaba al abrir y cerrar la pantalla rápido, y que el sistema "se sentía lento". Son dos caras del mismo problema. Documentado en [[Vista Descargada Durante un await (async void Loaded)]].

**El crash:** `Loaded` es `async void`; después de `await _vm.CargarDatosAsync()` viene `PoblarRoles()`. Si el usuario cierra mientras carga, `Unloaded` pone `_vm = null!` y la continuación del await vuelve sobre una vista ya descargada.

- `UsuariosView` / `BitacoraView`: se captura el VM **antes** del await y se compara con `ReferenceEquals` después. Se usa comparación por referencia y no contra null a propósito: el chequeo de null no cubre el abrir-cerrar-abrir rápido, donde `_vm` no es null pero es **otro** VM.
- `PoblarRoles` / `PoblarFiltros` / `OnVmPropertyChanged`: guard `if (_vm == null) return;` como segunda línea.

**La lentitud** (misma raíz, otro efecto): `UsuariosViewModel` no tenía `CancellationTokenSource`, así que cerrar la pantalla dejaba la consulta HTTP viva hasta terminar — abrir y cerrar varias veces acumulaba consultas compitiendo. Los repositorios **ya aceptaban `CancellationToken`**; simplemente nadie se lo pasaba. Se agregó CTS cancelado en `Dispose()`, el token en las llamadas y guards `if (_disposed) return;` tras cada await.

**Hallazgo extra — el timer fantasma:** el patrón `Task.WhenAny(task, Task.Delay(TimeoutMs))` está en **9 ViewModels** y el `Task.Delay` nunca se cancela: cada carga de página dejaba un timer de 10 s vivo aunque la consulta volviera en 200 ms. Corregido en Usuarios con un `CancellationTokenSource` enlazado.

Los otros 7 ViewModels **no crashean** (no tienen código después del await), pero les falta la cancelación y el arreglo del timer: quedó como **P-029**, sin tocarlos, para no meter riesgo en 7 módulos que hoy funcionan.

### Apertura lenta de Roles y parpadeo de maquetado

El usuario reportó que Roles **no abre como los demás módulos**: tarda, y por medio segundo se ve un maquetado distinto antes del definitivo. Tres causas, todas corregidas.

**1. Tres viajes de red en serie.** `ObtenerResumenAsync` esperaba el catálogo, después roles+asignaciones, y después el conteo de usuarios. La pantalla pagaba la **suma** de los tres. Ahora las cuatro consultas salen juntas en un solo `Task.WhenAll`: se paga el viaje más lento, no la suma.

**2. Encabezado, barra de herramientas y pie dibujados sin datos.** Solo el cuerpo se ocultaba mientras cargaba; el resto se dibujaba con los stats en 0, el selector de rol vacío y los segmentados sin selección, y al llegar la respuesta se poblaba de golpe. **Eso era el "otro diseño" de medio segundo.** Ahora `ActualizarCarga()` muestra u oculta la pantalla entera, y el spinner quedó centrado (se movió fuera del `ScrollViewer`: adentro se mide con alto infinito y el centrado no tiene efecto).

**3. `OpcionEstado` / `OpcionVista` se asignaban después del `await`.** Los selectores segmentados vivían sin selección hasta que respondía la BD y después saltaban a su estado real. Se movieron al **constructor** del ViewModel.

**Además, se quitaron 7 `DropShadowEffect`** — uno por tarjeta de módulo (×6) más el de la barra de herramientas, y el del item seleccionado del segmentado se reemplazó por un borde. Las sombras son pixel shaders y ya están documentadas como el problema de rendimiento #1 del proyecto en [[WPF - Rendimiento de Efectos y Niveles de Renderizado]]; tener una por tarjeta contradecía esa guía. Quedan solo dos, ambas de elemento único: la insignia del encabezado y el popup del selector de rol.

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
