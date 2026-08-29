---
title: Sesión 2026-08-12 — Estabilización de la pantalla de Roles
type: sesion
status: vigente
tags:
  - sesion
  - bimbo
  - wpf
  - rendimiento
  - rbac
date: 2026-08-12
updated: 2026-08-12
summary: "Roles dejó de trabarse, de crashear y de duplicar la carga. Se alineó con la arquitectura del resto de los formularios: ComboBox estándar, sin scroll horizontal,…"
scope: []
symbols:
  - AccionItemVm
  - AffectsParentMeasure
  - AlternarSelectorRolCommand
  - AplicarFiltro
  - ArrangeOverride
  - ArreglarFila
  - Auto
  - CalcularRejilla
  - ComboBox
  - DataContext
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude
---

# Sesión 2026-08-12 — Estabilización de la pantalla de Roles

> [!success] Resultado
> Roles dejó de trabarse, de crashear y de duplicar la carga. Se alineó con la arquitectura del resto de los formularios: `ComboBox` estándar, sin scroll horizontal, sin bindings que recorran el árbol y con el encabezado visible desde el primer frame.

---

## Problema

El usuario reportó, con video y capturas: la app **crashea**, se traba al maximizar, el desplegable de roles sale **despegado en la esquina superior izquierda**, y abrir/cerrar repetidamente degrada todo el sistema.

---

## Por qué Roles y no las otras pantallas

Roles vino de un mockup React traducido a XAML y adoptó cinco mecanismos que **ninguna** otra vista usa:

| | Las otras 12 vistas | Roles (antes) |
|---|---|---|
| Lista | `DataGrid` virtualizado + `PageSize=50` | `ItemsControl` anidado, ~600 visuales de golpe |
| Scroll horizontal | `Disabled` | **`Auto`** |
| Desplegables | `ComboBox` | **`Popup` a mano** |
| `RelativeSource AncestorType` | **0** | ~85 instanciados |
| Debounce de búsqueda | Sí | **No** |

---

## Causa raíz: bucle de layout infinito

**Introducido por mí** en el commit de `MinColumnWidth` + scroll horizontal. Documentado en detalle en [[WPF - Bucle de Layout por Medir en ArrangeOverride]].

`SpanningGridPanel` medía a sus hijos en **ambos** overrides con **anchos distintos**: `MeasureOverride` recibía ∞ (por el scroll horizontal) y usaba el mínimo, 250; `ArrangeOverride` recibía el ancho real y usaba 342,5. Con `TextWrapping="Wrap"` la altura cambia entre esos anchos, y medir a mano en el arreglo dispara `InvalidateMeasure` → **el layout nunca converge**. WPF corta a las 153 iteraciones: ~12.900 mediciones por gesto.

---

## Cambios aplicados

### Layout
- `SpanningGridPanel.cs` **reescrito**: `CalcularRejilla` es una función pura del ancho, y `ArrangeOverride` **ya no mide**. `ArreglarFila` pasó de `return` a `continue`.
- `MinColumnWidth` ahora **reduce la cantidad de columnas** (4→3→2→1) en vez de forzar scroll horizontal.
- `RolesView.xaml`: `HorizontalScrollBarVisibility` de `Auto` a `Disabled`, como las otras 12 vistas.

### Crash
- `UserControl_Loaded` es `async void`: una excepción que se escape **tumba la aplicación**. Ahora va en `try/catch` con log Serilog y `MostrarErrorCarga()`, que deja la pantalla en un estado legible en vez de un spinner eterno.
- Guard `ReferenceEquals` post-`await` (patrón de `UsuariosView`).
- `LoadingPanel` declara `Visibility="Collapsed"` en XAML, como las otras tres vistas.

### Desplegable de roles
- El `Popup` a mano se reemplazó por un **`ComboBox` estándar**. Un `Popup` es una ventana top-level con su propio HWND y **WPF no lo reposiciona al maximizar** — por eso caía en el origen de la pantalla. El `ComboBox` usa el `PART_Popup` del tema del sistema, que sí se reposiciona.
- Se eliminaron `SelectorRolAbierto`, `AlternarSelectorRolCommand`, el `DataTemplate` `RolItemSelector` y `EsSeleccionado`.

### Carga duplicada
- `MainViewModel.Navigate` ahora guarda la ruta vigente y **no re-navega si coincide**. Los VM de ruta son `class` (igualdad por referencia), así que sin esta guarda cualquier clic repetido en el sidebar destruía la vista y repetía la consulta a la BD.

### Encabezado desde el primer frame
- El encabezado, el buscador y los filtros son parte fija del formulario y ya no se ocultan. Solo el área de tarjetas muestra el spinner — mismo criterio que Productos, Usuarios y Categorías, donde lo único que se oculta es el `DataGrid`.

### Menos trabajo por interacción
- **Debounce de 180 ms** en el filtro de permisos. Cada `AplicarFiltro` escribe `Visible` en 34 objetos bindeados a `ContentPresenter.Visibility` (`AffectsParentMeasure`): sin espera, cada tecla disparaba una pasada de layout completa.
- **Se eliminaron los ~85 bindings `RelativeSource AncestorType=UserControl`.** Los comandos y `PuedeEditar` viajan ahora en `ModuloItemVm`/`AccionItemVm`, así que la plantilla bindea contra su propio `DataContext` en vez de recorrer ~20 niveles del árbol visual por binding.
- **Se quitó la consulta de conteo de usuarios por rol**: ya no se muestra en ningún lado desde que el selector es un `ComboBox`. Era un viaje de red desperdiciado en cada apertura. Afecta a [[ADR-014 - Precarga unica y cache del catalogo RBAC]], que pasa de 4 consultas paralelas a 3.

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores**, **0 advertencias nuevas** en los archivos tocados.
- Sin referencias muertas: `SelectorRolAbierto`, `RolItemSelector`, `EsSeleccionado`, `UsuariosPorRol` ya no aparecen en el código.

> [!warning] Sin verificación visual en runtime
> No se pudo ejecutar la aplicación desde la sesión. Queda pendiente la prueba manual — ver **P-030**.

---

## Lo que NO cambió

- No se tocó el esquema de Supabase ni las políticas RLS.
- No se tocó `Permiso` / `PermisoCatalogo` / `SesionPermisos`.
- **No se tocaron los frenos de rendimiento del resto de la app** (decisión del usuario): quedan registrados como **P-031**.

---

## Relaciones

- [[WPF - Bucle de Layout por Medir en ArrangeOverride]] — la trampa central
- [[Vista Descargada Durante un await (async void Loaded)]] — el guard post-await
- [[ADR-014 - Precarga unica y cache del catalogo RBAC]]
- [[Deuda Técnica - Pendientes]] — P-030 y P-031
