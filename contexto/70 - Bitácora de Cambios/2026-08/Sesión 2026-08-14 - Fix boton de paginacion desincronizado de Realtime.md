---
title: "Sesión 2026-08-14 — Fix: botones de paginación desincronizados de Realtime"
type: sesion
status: vigente
tags:
  - sesion
  - realtime
  - paginacion
  - bugfix
date: 2026-08-14
updated: 2026-08-14
summary: "Los botones numerados de paginación (Productos, Categorías, Fabricantes, Proveedores, ContactosFabricantes, ContactosProveedores) ahora se reconstruyen también…"
scope:
  - CapaUI/Formularios/Principal/Pantallas/Categorias
  - CapaUI/Formularios/Principal/Pantallas/ContactosFabricantes
  - CapaUI/Formularios/Principal/Pantallas/ContactosProveedores
  - CapaUI/Formularios/Principal/Pantallas/Fabricantes
  - CapaUI/Formularios/Principal/Pantallas/Productos
  - CapaUI/Formularios/Principal/Pantallas/Proveedores
symbols:
  - CanExecute
  - CargarDatosAsync
  - CargarPaginaAsync
  - CargarPaginaSilenciosamenteAsync
  - Command
  - Empleados
  - ItemsControl
  - ItemsSource
  - OnCambioProducto
  - OnPropertyChanged
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Sonnet 5 (Claude Code)
---

# Sesión 2026-08-14 — Fix: botones de paginación desincronizados de Realtime

> [!success] Resultado
> Los botones numerados de paginación (Productos, Categorías, Fabricantes, Proveedores, ContactosFabricantes, ContactosProveedores) ahora se reconstruyen también cuando `TotalPages` cambia por Realtime sin que cambien las filas visibles — antes se quedaban con el árbol viejo hasta cerrar y reabrir el módulo. Fix de 1 línea por módulo, sin llamadas de red nuevas. Build: `0 errores`, `2 advertencias` preexistentes (`RolesViewModel`, sin relación).

> [!failure] Este fix causó una regresión, corregida el mismo día
> `RefrescarPaginacion()` no solo reconstruía los botones: también hacía `DgX.ItemsSource = _vm.PageRows`. Ese efecto secundario era invisible mientras el único disparador fuera `PageRows`. Al agregarle `TotalPages` como segundo disparador, el método pasó a correr desde el setter de `Page` — que notifica `TotalPages` **antes** de salir a pedir los datos — y la grilla quedaba rebindeada a las filas de la página anterior mientras los botones ya mostraban el número nuevo.
>
> Síntoma reportado: *"sale el número correcto pero la página es la misma que la anterior"*. Corregido separando ambas responsabilidades en [[Sesión 2026-08-14 - Regresion la grilla mostraba la pagina anterior]]. **Leer las dos notas juntas.**

---

## Problema reportado

> "las etiquetas de cantidad de productos actualizaron pero los botones de paginación no, por lo que al seleccionar la última página y seguir paginando hacia delante muestra las páginas del principio pero en el número de páginas muestra como que está en la 12 (ya que crecieron los registros) pero realmente se está pasando a la 1 de nuevo [...] al volver a abrir el modal sí se actualiza"

Es decir: Realtime agregó productos mientras el usuario estaba en la última página (ej. 11 de 11), el total pasó a necesitar una página 12, las etiquetas de conteo (TOTAL/ACTIVOS/INACTIVOS) se actualizaron solas, pero los botones numerados de paginación seguían mostrando el árbol viejo (`1…10,11`) — tocar "siguiente" desde ahí llevaba a la página 1, no a la 12.

---

## Diagnóstico

`ProductosViewModel.CargarPaginaSilenciosamenteAsync(actualizarFilas, esInsert)` — el handler de Realtime para INSERT — decide **a propósito** no reasignar `PageRows` cuando el INSERT crea una página nueva y el usuario está parado en la vieja última página:

```csharp
bool debeActualizarFilas = actualizarFilas && (!esInsert || _page == nuevoTotalPages);
```

Esto es intencional y ya estaba documentado en [[Sesión 2026-05-26 - Realtime Silent Refresh Productos]]: no le saca de abajo las filas que el usuario está mirando. Ese mismo método **sí** actualiza `TotalCount/ActivosCount/InactivosCount/_filteredCount` y **sí** dispara `OnPropertyChanged(nameof(TotalPages))` + `NotifyPaginationCanExecuteChanged()` — dentro del ViewModel, `TotalPages` y el `CanExecute` de los botones `«/‹/›/»` (bindeados a `Command`) quedaban correctos.

El bug estaba en la vista. `ProductosView.xaml.cs` reconstruye el `ItemsControl` de números de página (`PaginacionPanel`, poblado a mano en el code-behind, sin `ItemsSource` bindeado) llamando a `RefrescarPaginacion()` — pero el `switch` de `OnVmPropertyChanged` solo tenía:

```csharp
case nameof(ProductosViewModel.PageRows): RefrescarPaginacion(); break;
```

Sin ningún `case` para `TotalPages`. Cuando `PageRows` no cambiaba (el caso de arriba), la vista nunca se enteraba de que `TotalPages` sí había cambiado, y `RefrescarPaginacion()` no se volvía a llamar — los botones numerados quedaban con el árbol viejo hasta la próxima recarga completa del módulo (`UserControl_Loaded` → `CargarDatosAsync` → `CargarPaginaAsync`, que siempre reasigna `PageRows`).

**El mismo patrón duplicado** (`RealtimeAwareViewModel` + paginación server-side + `ItemsControl PaginacionPanel` poblado a mano + `RefrescarPaginacion()` disparado solo por `case PageRows`) existe en 5 módulos más: Categorías, Fabricantes, Proveedores, ContactosFabricantes, ContactosProveedores — mismo bug latente, sin reportar todavía. `Usuarios` y `Empleados` tienen el mismo `PaginacionPanel` en su code-behind pero sus ViewModels no heredan de `RealtimeAwareViewModel` (sin Realtime hoy), así que no lo disparan — pero lo heredarían si algún día se les agrega Realtime siguiendo el checklist. `Roles` no tiene paginación server-side, no aplica.

---

## Solución

Agregar `case nameof(<ViewModel>.TotalPages): RefrescarPaginacion(); break;` junto al `case` de `PageRows` existente, en los 6 code-behind:

- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriasView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricantesView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedoresView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/ContactosFabricantes/ContactosFabricantesView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/ContactosProveedores/ContactosProveedoresView.xaml.cs`

Es el fix más barato posible para este bug:

- **Cero llamadas de red nuevas** — `RefrescarPaginacion()` ya existía, solo reconstruye 7-9 `Button` en memoria a partir de `_vm.TotalPages`/`_vm.Page` (ya frescos).
- **Cero cambios en el ViewModel** — `TotalPages` ya notificaba `OnPropertyChanged` en los 3 puntos que importan; el problema nunca estuvo ahí, solo en que la vista no escuchaba ese aviso.
- **Idempotente**: cuando `PageRows` y `TotalPages` cambian juntos (carga de usuario), `RefrescarPaginacion()` corre dos veces seguidas sin efecto visible ni costo real.

Se descartaron dos alternativas por sobre-ingeniería para el tamaño del problema: un control de paginación compartido con `ItemsSource` bindeado (refactor grande) y mover el árbol de páginas a una propiedad calculada del ViewModel (movería lógica de presentación a una capa que hoy no la tiene).

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores**, 2 advertencias preexistentes (`RolesViewModel.cs`, firma de método parcial — sin relación con este cambio).
- **Pendiente en runtime**: abrir Productos con la última página casi llena, insertar una fila nueva por SQL directo en Supabase, confirmar que el `ItemsControl` de números crece sin recargar el módulo y que "siguiente" desde la vieja última página ahora sí lleva a la nueva. Repetir en al menos un módulo más de los 5 restantes.

## Lo que NO se hizo

- No se tocó el caso `DELETE`, que `OnCambioProducto` no maneja explícitamente — no es parte de este bug, no se investigó a fondo.
- No se construyó un control de paginación compartido — el fix se mantuvo mínimo a propósito.

## Estado en git

Sin commitear al cierre de esta nota. El árbol de trabajo mezcla este fix con otro trabajo hecho en paralelo el mismo día (catálogo de `unidad_medida`, logo de empresa, Realtime en columnas de join) — separar los commits queda a criterio del usuario.

---

## Relaciones

- [[Sesión 2026-05-26 - Realtime Silent Refresh Productos]] — origen de `CargarPaginaSilenciosamenteAsync`/`debeActualizarFilas`, con adenda agregada hoy
- [[Checklist - Replicar Módulo con Realtime]] — actualizado con este paso
- [[Paginación y Búsqueda - Arquitectura Detallada]] — sección 6 actualizada
- [[Módulo Productos]]
- [[Sesión 2026-08-14 - Realtime en columnas de join de Productos]] — otro fix de Realtime en Productos el mismo día, causa raíz distinta
