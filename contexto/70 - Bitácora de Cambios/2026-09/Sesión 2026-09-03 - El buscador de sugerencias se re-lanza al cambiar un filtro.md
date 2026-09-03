---
title: "Sesión 2026-09-03 — El buscador de sugerencias se re-lanza al cambiar un filtro"
tags:
  - sesion
  - ui
  - wpf
  - buscador
  - mantenibilidad
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando
---

# Sesión 2026-09-03 — El buscador de sugerencias se re-lanza al cambiar un filtro

> [!success] Resultado
> El `SuggestionSearchBox` de las 8 tablas de lista con filtros ahora **re-consulta
> automáticamente al cambiar cualquier filtro u orden**, con el texto que ya está
> escrito. Antes había que borrar y reescribir una letra. Se resolvió con **un
> único método por ViewModel** (`AplicarCambioDeFiltro()`), que además elimina la
> duplicación de `_page = 1; _ = CargarPaginaAsync();` repartida en ~15 setters.
> Compila 0/0, 243/243 tests.

---

## Problema

1. Filtro en "Activos", escribo el nombre de un producto **inactivo** → el popup
   dice "Sin resultados" (correcto).
2. Cambio el filtro a "Inactivos" → la grilla recarga, **pero el popup se queda
   igual**. Solo se actualizaba borrando una letra y volviéndola a escribir.

**Causa:** el único disparador de `RefrescarSugerenciasAsync()` era el setter de
`Query` (al teclear). Los setters de filtro/orden hacían `_page = 1;
_ = CargarPaginaAsync();` pero no re-lanzaban las sugerencias. La consulta de
sugerencias ya lee `BuildFiltros()` (o `_estadoFiltro`/`_rolFiltro`) en el momento
de ejecutar, así que bastaba con volver a invocarla.

## Cambio

En cada VM de lista con buscador, un choke point único:

```csharp
private void AplicarCambioDeFiltro()
{
    _page = 1;
    _ = CargarPaginaAsync();
    _ = RefrescarSugerenciasAsync();   // <- lo nuevo
}
```

Todos los setters de filtro/orden y `LimpiarFiltros()` llaman a ese método en vez
de repetir `_page = 1; _ = CargarPaginaAsync();`. En **Bitácora** ya existía el
choke point con otro nombre (`ReiniciarYCargar()`, lo llaman los 5 setters de
filtro) — solo se le agregó la línea de `RefrescarSugerenciasAsync()`.

**No se tocó** `SuggestionDebouncer` ni `SuggestionSearchBox` ni `Styles.xaml`:
`RefrescarSugerenciasAsync()` ya encapsula el closure por VM y el debouncer ya
cierra el popup si el texto está vacío (`q.Length < 2 → aplicar(null)`).

### Archivos (solo `*ViewModel.cs`, bajo `CapaUI/Formularios/Principal/Pantallas/`)

| VM | Setters ruteados |
|---|---|
| `Productos/ProductosViewModel` | `EstadoFiltro`, `FabricanteIdFiltro`, `PaisIdFiltro`, `CategoriaIdFiltro`, `ProveedorIdFiltro`, `Orden` + `LimpiarFiltros` |
| `Presentaciones/PresentacionesViewModel` | `EstadoFiltro`, `Orden` + `LimpiarFiltros` |
| `Proveedores/ProveedoresViewModel` | `EstadoFiltro` + `LimpiarFiltros` |
| `Fabricantes/FabricantesViewModel` | `EstadoFiltro`, `PaisIdFiltro` + `LimpiarFiltros` |
| `Categorias/CategoriasViewModel` | `EstadoFiltro` + `LimpiarFiltros` |
| `Empleados/EmpleadosViewModel` | `EstadoFiltro` + `LimpiarFiltros` |
| `Usuarios/UsuariosViewModel` | `EstadoFiltro`, `RolFiltro` + `LimpiarFiltros` |
| `Bitacora/BitacoraViewModel` | `UsuarioFiltro`, `ModuloFiltro`, `AccionFiltro`, `FechaDesde`, `FechaHasta` (vía `ReiniciarYCargar()`) + `LimpiarFiltros` |

`UsuariosViewModel.NavegarARegistroAsync` (salto programático a un usuario desde
notificación) sigue con su `_page = 1; ... CargarPaginaAsync()` inline **a
propósito**: no es un cambio de filtro de UI y no debe abrir el popup.

**No se tocaron:** Contactos Fabricantes/Proveedores (sin filtro de estado, siempre
activos), Roles (sin `SuggestionSearchBox`).

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores, 0 advertencias**.
- `dotnet test BimboProyecto.Tests` → **243/243**.
- Manual pendiente (app cerrada / reiniciada): en Productos, escribir un inactivo
  con filtro "Activos" → "Sin resultados"; clic "Inactivos" **sin tocar el texto**
  → el popup se actualiza solo. Repetir con ComboBox de filtro, con el Orden, y con
  "Limpiar filtros". Caja vacía + clic entre filtros → sin popup, sin parpadeo.
- Regresión: teclear sigue con debounce; seleccionar sugerencia sigue navegando;
  paginar con la caja vacía no dispara consultas extra.

## Nota multi-agente

Cambio 100 % en `*ViewModel.cs`. En paralelo, Antigravity estaba en un pase de las
View XAML (fallbacks contextuales, headers, columna #) — sin solape.

## Relaciones

- [[CLAUDE]] — sección "Búsqueda con sugerencias" actualizada con el choke point
- [[Paginación y Búsqueda - Arquitectura Detallada]]
- [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]] — origen del `SuggestionDebouncer`
- [[Arquitectura Actual]]
