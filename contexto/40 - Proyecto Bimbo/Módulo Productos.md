---
title: Módulo Productos
tags:
  - bimbo
  - modulo
  - productos
---

# Módulo Productos

> [!abstract]
> El módulo más completo del proyecto. Referencia para implementar nuevos módulos.

---

## Archivos

```
CapaAplicacion4/Productos/
  Dtos/ProductoDto.cs          → DTO con FKs para UI
  Dtos/FiltroItem.cs           → { int? Id, string Nombre } para ComboBox
  Queries/PagedResult.cs       → { Items, Total, Activos, Inactivos }
  Queries/ProductoFiltros.cs   → { IdEstado, IdFabricante, IdPais }
  Interfaces/IProductoRepository.cs
    GetPagedAsync               → paginación server-side + conteos
    BuscarSugerenciasAsync      → ILike server-side, Limit 10
    GetFabricantesAsync         → ComboBox fabricantes
    GetPaisesAsync              → ComboBox países
    GetCategoriasAsync          → ComboBox categorías (para modal)
    GetPaginaDeProductoAsync    → calcula en qué página está un producto (cross-page search)
    CreateAsync / UpdateAsync / DeleteAsync (soft)

CapaDatos/Repositories/Productos/
  ProductoCrudRepository.cs    → implementa IProductoRepository

CapaDatos/Repositories/Search/
  ProductoSearchRepository.cs  → implementa IRepository<Producto>

CapaUI/Formularios/Principal/Pantallas/Productos/
  ProductosView.xaml           → layout con DataGrid, filtros, paginación, spinner
  ProductosView.xaml.cs        → code-behind: DI, highlight, modal, spinner animation
  ProductosViewModel.cs        → ObservableObject + IProductoRepository + IRealtimeService
  ProductoModal.xaml           → modal crear/editar
  ProductoModal.xaml.cs        → acepta ProductoDto?
```

---

## Flujo de datos — Carga inicial

```
UserControl_Loaded
    ↓ App.Services.GetRequiredService<ProductosViewModel>()
    ↓ CargarDatosAsync()
    ├── Task.WhenAll(                          ← paralelo
    │     _repo.GetFabricantesAsync(),
    │     _repo.GetPaisesAsync()
    │   )
    └── CargarPaginaAsync()
            ↓ Task.WhenAll(                    ← paralelo interno
            │     query.Order("id_producto", Asc).Range(from, to).Get(),
            │     GetConteosAsync(filtros, client)   ← 2 queries paralelas
            │   )
            → PageRows, TotalCount, ActivosCount, InactivosCount
    └── _realtime.SuscribirAsync("productos", OnCambioProducto)
```

> [!note] Conteos reales
> `GetConteosAsync` ejecuta dos `Get()` con `Select("id_producto")` — una sin filtro de estado (total) y otra con `id_estado = 1` (activos). Cuenta `Models.Count` en el cliente. No usa `CountType.Exact`. Ver [[Paginación y Búsqueda - Arquitectura Detallada]].

---

## Flujo de datos — Búsqueda con sugerencias

```
TxtBusqueda.TextChanged
    ↓ _vm.Query = text
    ↓ RefrescarSugerenciasAsync() [debounce 300ms, cancela token previo]
    ↓ _repo.BuscarSugerenciasAsync(q, filtros)   ← ILike server-side, Limit 10
    → Suggestions: ObservableCollection<ProductoDto>
    ↓ ShowSuggestions = true → SuggestionsPopup.IsOpen = true

Enter / Click
    ↓ SeleccionarSugerencia(ProductoDto p)
    │
    ├── CASO A: producto está en PageRows (misma página)
    │       → Seleccionado = enPagina  (no recarga)
    │
    └── CASO B: producto está en otra página
            → _pendingSelectionId = p.Id
            → NavegarAPaginaDeProductoAsync(p.Id)
                    ↓ _repo.GetPaginaDeProductoAsync(idProducto, size, filtros)
                    │   (cuenta registros con id_producto < idProducto → page = previos/size + 1)
                    ↓ _page = página calculada (sin disparar CargarPaginaAsync doble)
                    ↓ CargarPaginaAsync()
                    └── al terminar: Seleccionado = PageRows.First(x.Id == _pendingSelectionId)
```

> [!important] `_pendingSelectionId`
> Es el mecanismo de selección diferida cross-page. Se asigna antes de `CargarPaginaAsync()` y se consume al final de ese método. Si la carga falla, se limpia (`_pendingSelectionId = null`) para evitar selecciones fantasma.

---

## Paginación — Bloqueo durante carga

```
IsLoading = true
    → NotifyCanExecuteChangedFor(PrimeraPagina, PaginaAnterior, PaginaSiguiente, UltimaPagina)
    → PuedePaginaAnterior()  = !IsLoading && _page > 1
    → PuedePaginaSiguiente() = !IsLoading && _page < TotalPages
    → Botones «‹›» se deshabilitan automáticamente vía Command binding
    → Botones numéricos dinámicos: guard `if (_vm.IsLoading) return`

IsLoading = false
    → Botones se rehabilitan automáticamente

Timeout: 10s → ErrorCarga = "La carga tardó demasiado" + IsLoading = false
```

> [!important] Orden de asignación en `CargarPaginaAsync`
> `_filteredCount` se asigna **ANTES** de `PageRows` porque el `PropertyChanged` de `PageRows` dispara `RefrescarPaginacion()` que lee `TotalPages` (que depende de `_filteredCount`). Si se invierte el orden, la primera carga muestra solo `< 1 >` en vez de todas las páginas.

---

## Spinner de carga

```
LoadingPanel (StackPanel) con:
  - SpinnerPath: arco parcial "M 12 2 A 10 10 0 1 1 2 12" + RotateTransform
  - Animación: DoubleAnimation 0→360° en 0.8s, RepeatBehavior.Forever
  - Se inicia/detiene desde code-behind según IsLoading
```

La animación es responsabilidad de la **vista**, no del ViewModel (separación MVVM).

---

## Highlight de sugerencias

```csharp
// VisualTreeHelper manual porque ItemsControl no tiene selección nativa
private void ActualizarHighlight()
{
    for (int i = 0; i < SuggestionsList.Items.Count; i++)
    {
        var container = SuggestionsList.ItemContainerGenerator.ContainerFromIndex(i);
        var border = VisualTreeHelper.GetChild(container, 0) as Border;
        border.Background = (i == _vm.HighlightIndex) ? _highlightBrush : Transparent;
    }
}
```

---

## Filtros — ComboBox buscables

Tanto **Fabricante** como **País** son ComboBox editables con filtrado en memoria.
Ver: [[Sesión 2026-05-26 - ComboBox Fabricante Buscable]]

### Patrón: `ICollectionView` + `IsEditable`

```
CargarDatosAsync() carga la lista completa una sola vez
    ↓ GetFabricantesAsync() / GetPaisesAsync()  ← HTTP call al inicio
    ↓ PoblarFabricantes() / PoblarPaises()      ← en PropertyChanged del ViewModel
    ↓ CollectionViewSource.GetDefaultView(lista)
    → _fabricantesView / _paisesView  (ICollectionView)
    → asignado a CmbXxx.ItemsSource

PreviewKeyUp (cada tecla)
    ↓ CmbXxx.Text → predicado Contains (OrdinalIgnoreCase)
    ↓ _xxxView.Filter = predicado   ← filtra en memoria, cero HTTP
    ↓ CmbXxx.IsDropDownOpen = true

SelectionChanged
    ↓ _xxxView.Filter = null        ← limpia para próxima apertura
    ↓ _vm.XxxIdFiltro = selected?.Id
    ↓ CargarPaginaAsync()           ← recarga con filtro server-side

LimpiarFiltros
    ↓ _xxxView.Filter = null
    ↓ CmbXxx.SelectedIndex = 0     ← vuelve a "(Todos)"
```

### Propiedades XAML requeridas

```xml
<ComboBox IsEditable="True"
          IsTextSearchEnabled="False"
          StaysOpenOnEdit="True"
          DisplayMemberPath="Nombre"
          PreviewKeyUp="CmbXxx_PreviewKeyUp"
          SelectionChanged="CmbXxx_SelectionChanged"/>
```

| Propiedad | Por qué es necesaria |
|---|---|
| `IsEditable` | Habilita campo de texto para escribir |
| `IsTextSearchEnabled="False"` | Evita autocompletado nativo por primera letra |
| `StaysOpenOnEdit` | Mantiene dropdown abierto mientras se filtra |
| `DisplayMemberPath` | Muestra `Nombre` de `FiltroItem` en el campo |

> [!note] Filtrado client-side
> No se hace ninguna llamada HTTP al escribir. Los ítems ya están en `_todosFabricantes` / `_todosPaises` desde la carga inicial. `ICollectionView.Filter` opera en memoria.

---

## Realtime

Integrado en [[Sesión 2026-05-24 - Implementación Gestor Realtime Completa]].
Refinado en [[Sesión 2026-05-26 - Realtime Silent Refresh Productos]] — eliminación del spinner para eventos Realtime.
Ver documento completo: [[Paginación y Búsqueda - Arquitectura Detallada]].

```
WebSocket (Supabase Realtime)
    ↓ PostgresChangesResponse (tabla "productos")
    ↓ RealtimeService.ExtraerCambio() → CambioRealtime { Operacion, IdRegistro, NuevoEstado }
    ↓ SynchronizationContext.Post → UI thread
    ↓ OnCambioProducto(cambio)

    INSERT
        → CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: true)
              ├── Consulta GetPagedAsync (sin spinner)
              ├── Calcula nuevoTotalPages con datos frescos
              ├── _page == nuevoTotalPages → aplica filas + conteos (INSERT puede estar aquí)
              └── _page <  nuevoTotalPages → solo conteos (INSERT creó página nueva)

    UPDATE en página actual (o IdRegistro desconocido)
        → CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: false)
              └── Recarga filas silenciosamente + restaura Seleccionado por Id

    UPDATE en otra página
        → RefrescarConteosAsync()   ← solo chips de conteo, sin tocar PageRows
              └── si página quedó vacía y _page > 1 → Page = _page - 1 (retroceso automático)
```

> [!important] Regla de oro: spinner SOLO para acciones del usuario
> `CargarPaginaAsync()` (con spinner) → filtros, paginación, carga inicial, modal guardado
> `CargarPaginaSilenciosamenteAsync()` (sin spinner) → exclusivamente eventos Realtime

### Garantías de `CargarPaginaSilenciosamenteAsync`

| Garantía | Mecanismo |
|---|---|
| No muestra spinner | Nunca toca `IsLoading` |
| No sobreescribe carga de usuario activa | Guard `if (IsLoading) return` |
| No aplica resultado obsoleto si usuario navegó | `genCapturada = _loadGeneration` (sin `++`), guard al final |
| Preserva selección activa tras reconstruir `PageRows` | Captura `Seleccionado?.Id`, restaura por Id post-swap |
| Errores silenciosos | `try/catch` doble → Serilog, sin `ErrorCarga` |

Lifecycle del canal:
- `SuscribirAsync` al terminar `CargarDatosAsync` → abre canal WebSocket
- `Dispose()` → `Desuscribir` → cierra canal si era el último suscriptor

---

## Patrones en uso

- [[Repository Pattern]] — `IProductoRepository` inyectado en ViewModel
- [[Observer Pattern]] — `ObservableObject` + `[ObservableProperty]` + `[RelayCommand]`
- [[Clean Architecture]] — ViewModel no toca Supabase directamente
- [[Result Pattern]] — `Result<T>` en todas las operaciones del repositorio
- [[Gestor Realtime - Diseño Arquitectónico]] — Suscripción por vista activa con `IRealtimeService`
- [[Paginación y Búsqueda - Arquitectura Detallada]] — Paginación server-side, cross-page search, Realtime handler completo

---

## Notas críticas

> [!bug] Fabricante no hereda BaseModel
> Nunca usar `client.From<Fabricante>()`. Cargar siempre vía join de productos.

> [!warning] CanUserAddRows="False"
> Todos los DataGrid deben tener esta propiedad para evitar filas inline.

> [!warning] El buscador SIEMPRE es `controls:SuggestionSearchBox`, nunca un `TextBox` plano
> Módulo Usuarios se construyó con un `TextBox` + `TextChanged` en vez del control compartido — mismo look-and-feel roto, sin popup ni navegación por teclado, aunque el ViewModel ya tenía `Suggestions`/`ShowSuggestions`/`HighlightIndex` listos. Se detectó y corrigió el 2026-07-26. Al replicar este módulo, verificar SIEMPRE que la vista use `<controls:SuggestionSearchBox Query="{Binding Query, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" HighlightIndex="{Binding HighlightIndex, Mode=TwoWay}" ItemSelected="SearchBox_ItemSelected"/>` + mapeo `SuggestionItemData` en el code-behind — no un `TextBox` binding directo a `Query`. Ver [[Sesión 2026-07-26 - Fix Buscador Usuarios (SuggestionSearchBox)]].
