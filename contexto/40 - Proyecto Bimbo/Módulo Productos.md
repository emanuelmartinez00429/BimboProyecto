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
  Dtos/ProductoDto.cs          → DTO con FKs, peso, precio y auditoría para UI
  Dtos/FiltroItem.cs           → { int? Id, string Nombre } para ComboBox
  Queries/PagedResult.cs       → { Items, Total, Activos, Inactivos }
  Queries/ProductoFiltros.cs   → { IdEstado, IdFabricante, IdPais, IdProveedor, IdCategoria } (desde 2026-08-14)
  Interfaces/IProductoRepository.cs
    GetPagedAsync               → paginación server-side + conteos
    BuscarSugerenciasAsync      → ILike server-side (columna normalizada, ver búsqueda sin tildes), Limit 10
    GetFabricantesAsync         → ComboBox fabricantes
    GetPaisesAsync              → ComboBox países
    GetCategoriasAsync          → ComboBox categorías (para modal Y para el filtro de la grilla, desde 2026-08-14)
    GetPaginaDeProductoAsync    → calcula en qué página está un producto (cross-page search)
    CreateAsync / UpdateAsync / DeleteAsync (soft)

CapaDatos/Repositories/Productos/
  ProductoCrudRepository.cs    → implementa IProductoRepository

CapaDatos/Repositories/Search/
  ProductoSearchRepository.cs  → implementa IRepository<Producto>

CapaUI/Formularios/Principal/Pantallas/Productos/
  ProductosView.xaml           → layout con DataGrid (columnas Creado y Actualizado antes de Estado), filtros, paginación, spinner y EmptyStateOverlay (desde 2026-09-02)
  ProductosView.xaml.cs        → code-behind: DI, highlight, modal, spinner animation
  ProductosViewModel.cs        → ObservableObject + IProductoRepository + IRealtimeService (NoResults y MensajeSinResultados)
  ProductoModal.xaml           → modal crear/editar + campos de peso, tara, precio y auditoría
  ProductoModal.xaml.cs        → acepta ProductoDto?
```

---

## Flujo de datos — Creación auditada

Desde 2026-08-15 la creación ya no ejecuta un `INSERT` PostgREST directo desde
`ProductoCrudRepository`. El repositorio toma el usuario autenticado desde
`IUsuarioSesionService` y llama la RPC `ingresar_producto_tabla_bitacora` con los
campos del `ProductoDto` y `SesionActual.IdUsuario`.

```
ProductoModal.BtnGuardar_Click
    ↓ IProductoRepository.CreateAsync(dto)
    ↓ ProductoCrudRepository
       ├── exige una sesión activa
       └── client.Rpc("ingresar_producto_tabla_bitacora", parámetros)
             ├── valida p_usuario_ingresando contra auth.uid()
             ├── exige la acción Crear Producto
             ├── INSERT en productos
             ├── INSERT en bitacora
             └── retorna id_producto
    ↓ Result<int>
    ↓ Guardado → refresco silencioso de la grilla
```

Producto y bitácora se escriben dentro de la misma función de PostgreSQL: si
falla cualquiera de las dos operaciones, no queda un producto sin auditoría ni
una entrada de bitácora huérfana. Los campos opcionales conservan `NULL`; el
repositorio nunca inventa IDs `0`. `UpdateAsync` y `DeleteAsync` conservan sus
actualizaciones actuales y no pasan por esta RPC.

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

> [!info] Campos de producto expuestos
> La consulta paginada incluye `nombre_producto`, `id_estado`, `peso_teorico`,
> `id_tara`, `id_unidad`, `id_categoria`, `contenido`, `id_pais`, `created_at`,
> `updated_at` y `precio_por_kg`. Para tara y unidad se solicita además su
> navegación (`tara(*, unidad_medida(*))`, `unidad_medida(*)`); el modal muestra
> `descripcion_tara` al editar y la lupa de tara (`BuscarTara_Click` +
> `SelectorCatalogoModal`) ya está implementada, no es un marcador visual.

> [!info] Contenido y unidad de medida
> `productos.contenido` se sigue persistiendo como un solo texto (`"500 g"`) por
> compatibilidad con el buscador universal y el picker de Pesaje, que lo leen así.
> Pero desde [[ADR-017 - Catalogo real de unidad_medida con categoria y su uso en Tara]]
> el combo "Unidad" del modal ya no es una lista fija en el XAML: se puebla en
> `OnLoaded` desde la tabla real `unidad_medida` (`CatalogoCache.ObtenerParaComboAsync`,
> sin filtro de categoría — el contenido puede ser masa o volumen), y la selección
> se persiste además como dato estructurado en `productos.id_unidad` (columna que
> ya existía en Supabase con su FK, pero el modelo C# la tenía comentada). Al
> abrir un registro existente se prioriza `id_unidad` para preseleccionar el
> combo; el sufijo de texto al final de `contenido` queda como fallback solo para
> filas viejas sin `id_unidad` (la mayoría de las 505 filas de prueba son texto
> `"Contenido N"` sin unidad reconocible, y se quedan así).
>
> `unidad_medida` ahora tiene categoría (`tipo_unidad`: Masa/Volumen/Conteo) y
> estado (FK a `estado_general`, igual que `fabricante`/`presentacion_producto`).
> `tara.id_unidad` también es un dato real ahora (antes "kg" era solo una
> convención del nombre de columna `peso_tara_envalaje`) — sin combo propio
> todavía porque no existe pantalla para editar `tara` (ver P-036).

> [!danger] Las FK de `productos` son NULLABLE — el modelo debe reflejarlo
> `id_presentacion`, `id_fabricante`, `id_categoria`, `id_pais`, `id_unidad`, `id_tara`,
> `peso_teorico`, `precio_por_kg` y `contenido` **admiten NULL en la base**. Solo
> `id_producto`, `codigo_producto`, `nombre_producto` e `id_estado` son obligatorios.
>
> Declarar cualquiera de esas columnas como value type sin `?` (ej. `int` en vez de
> `int?`) hace que Newtonsoft lance al deserializar una fila con NULL, y **falla la
> consulta entera, no la fila** — la página completa queda sin cargar y la grilla
> se queda mostrando la anterior sin avisar nada. Pasó de verdad el 2026-08-14:
> 175 productos con `id_presentacion` en NULL dejaron muertas las páginas 11 a 14.
> Ver [[Sesión 2026-08-14 - Modelo desalineado del esquema tumbaba paginas enteras]] y P-038.
>
> Al agregar una columna al modelo, verificar siempre su nullabilidad real:
> ```sql
> select column_name, data_type, is_nullable from information_schema.columns
> where table_schema='public' and table_name='productos' order by ordinal_position;
> ```

> [!note] Conteos reales
> `GetConteosAsync` ejecuta dos `Get()` con `Select("id_producto")` — una sin filtro de estado (total) y otra con `id_estado = 1` (activos). Cuenta `Models.Count` en el cliente. No usa `CountType.Exact`. Ver [[Paginación y Búsqueda - Arquitectura Detallada]].

---

## Flujo de datos — Búsqueda con sugerencias

```
TxtBusqueda.TextChanged
    ↓ _vm.Query = text
    ↓ RefrescarSugerenciasAsync() → _buscador.EjecutarAsync(...)
                                    [SuggestionDebouncer: 300ms, cancela el anterior]
    ↓ _repo.BuscarSugerenciasAsync(q, filtros)   ← ILike server-side, Limit 10
    → SuggestItems = results.Select(Map).ToList()   ← una sola señal, ya mapeada
    ↓ binding SuggestItems="{Binding SuggestItems}"
    ↓ SuggestionSearchBox.OnSuggestItemsChanged → SuggestionsPopup.IsOpen = true

Enter / Click
    ↓ SeleccionarSugerencia(ProductoDto p)     ← código de vista, ver abajo
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

> [!note] Desde 2026-08-14 — elegir una sugerencia abre el modal de edición directo
> `ProductosView.SearchBox_ItemSelected` ya no se limita a seleccionar la fila: llama a `AbrirModalEditar(producto)` con el DTO que trae la sugerencia (no espera el salto de página, que sigue corriendo en paralelo solo para dejar la grilla consistente cuando se cierra el modal). Un solo paso — elegir del buscador ya deja al usuario editando.

> [!note] Búsqueda insensible a mayúsculas y tildes (desde 2026-08-14)
> `BuscarSugerenciasAsync` ya no hace `OR` sobre `nombre_producto`/`codigo_producto`: filtra contra la columna generada `productos.busqueda_producto` (ambas columnas concatenadas, ya en minúsculas y sin tildes), normalizando el término con `TextoBusqueda.Normalizar()` antes de mandarlo. Así "azucar" encuentra "AZÚCAR". Decisión completa, alternativas descartadas y qué tablas faltan migrar: [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]].

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

Vive dentro de `SuggestionSearchBox`, no en la vista. Es un `ListBox` con
`SelectedIndex="{Binding HighlightIndex, Mode=OneWay}"` y triggers de estilo
(`IsMouseOver` para hover, `IsSelected` para la selección — el segundo va después
para que gane).

`Mode=OneWay` es deliberado: en `TwoWay`, cualquier cambio de `SelectedIndex`
(incluido el reemplazo del `ItemsSource`) escribiría de vuelta en `HighlightIndex`
del ViewModel, disparando efectos secundarios.

> [!info] El `VisualTreeHelper` manual fue eliminado
> La versión anterior recorría el visual tree desde el code-behind porque el
> control usaba un `ItemsControl` sin selección nativa. Se reemplazó por `ListBox`
> + triggers al resolver **P-005** (2026-05-28): dependía de que el template XAML
> no cambiara ni un `Border`, y fallaba en runtime sin excepción clara.
> Ver [[Sesión 2026-05-28 - Refactor P004-P005]].

---

## Filtros — ComboBox buscables

> [!warning] Esta sección describía el patrón original (`ICollectionView` + handlers `PreviewKeyUp`/`SelectionChanged` copiados por combo). **Ya no es así** — extraído a la clase `ComboFiltro` (ver abajo). Se deja constancia acá porque el patrón viejo puede seguir copiado en otros módulos que no pasaron por el mismo refactor.

Cuatro ComboBox editables con filtrado en memoria: **Proveedor**, **Fabricante** (encadenado a Proveedor), **Categoría** (agregado 2026-08-14) y **País**.

### Patrón actual: `CapaUI/Core/Controls/ComboFiltro.cs`

Toda la mecánica de un combo de filtro (sentinela `"(Todos)"`, `ICollectionView` + filtro en memoria, apertura/cierre, `Escape` cancela, `Enter`/cierre de dropdown confirma) vive en **una sola clase**, instanciada una vez por combo:

```csharp
// Code-behind de la vista — un ComboFiltro por combo
_filtroFabricante = new ComboFiltro(CmbFabricante);
_filtroCategoria  = new ComboFiltro(CmbCategoria);
// ...

_filtroFabricante.SeleccionCambiada += id => { if (_vm != null) _vm.FabricanteIdFiltro = id; };
_filtroCategoria.SeleccionCambiada  += id => { if (_vm != null) _vm.CategoriaIdFiltro  = id; };

// Cuando el VM repuebla la lista (PropertyChanged)
case nameof(ProductosViewModel.Categorias): _filtroCategoria.Poblar(_vm.Categorias); break;
```

El filtrado en memoria (`OnPreviewKeyUp` → `_vista.Filter = ...`) usa `TextoBusqueda.Contiene()`, no `string.Contains(OrdinalIgnoreCase)` — ignora tildes además de mayúsculas, igual que el buscador server-side. Ver [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]].

> [!danger] Trampa real, ya mordió una vez — cascada Proveedor → Fabricante
> `ProveedorIdFiltro` (el setter de la propiedad, no el campo privado) es el **único** lugar que llama `ReacotarFabricantes()`, que reacota `Fabricantes` a los del proveedor elegido. Si algo pisa `_proveedorIdFiltro` directo (el campo, no la propiedad) — como hacía `LimpiarFiltros()` hasta que se corrigió el 2026-08-14 — `Fabricantes` se queda con la lista angosta del proveedor anterior aunque el combo ya muestre "(Todos)": la selección visual se limpia pero las opciones detrás no. **Regla:** cualquier código nuevo que toque `_proveedorIdFiltro` tiene que pasar por la propiedad, o llamar `ReacotarFabricantes()` a mano después.

### Propiedades XAML requeridas (siguen vigentes)

```xml
<ComboBox IsEditable="True"
          IsTextSearchEnabled="False"
          StaysOpenOnEdit="True"
          DisplayMemberPath="Nombre"/>
```

| Propiedad | Por qué es necesaria |
|---|---|
| `IsEditable` | Habilita campo de texto para escribir |
| `IsTextSearchEnabled="False"` | Evita autocompletado nativo por primera letra |
| `StaysOpenOnEdit` | Mantiene dropdown abierto mientras se filtra |
| `DisplayMemberPath` | Muestra `Nombre` de `FiltroItem` en el campo |

Ya no hace falta `PreviewKeyUp`/`SelectionChanged` en el XAML — `ComboFiltro` se suscribe solo en su constructor.

### Barra de filtros responsive (desde 2026-08-14)

Los cuatro combos + las dos pastillas segmentadas (ESTADO, ORDEN) viven dentro de `controls:PanelFiltrosFluido`, no un `Grid`/`WrapPanel` a mano: mantiene todo en una línea mientras entra y reparte en líneas equilibradas cuando no. Diseño y por qué ningún panel nativo de WPF alcanzaba: [[Panel de Filtros Fluido - Barra responsive con prioridad y equilibrado]].

> [!note] Filtrado client-side de cada combo
> No se hace ninguna llamada HTTP al escribir dentro de un combo. Los ítems ya están en memoria (`_todosFabricantes`, `Categorias`, etc.) desde la carga inicial. El `Filter` de `ICollectionView` opera en memoria.

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

### Realtime en las columnas de join (desde 2026-08-14)

Siete columnas de la grilla (`FABRICANTE`, `PROVEEDOR`, `CATEGORÍA`, `PAÍS`, `PRESENTACIÓN`, `TARA`, unidad) **no salen de `productos`** — salen de `SelectPara`, que hace join contra `fabricante`, `proveedores`, `categoria`, `paises`, `presentacion_producto`, `tara(*, unidad_medida(*))` y `unidad_medida`. Un `UPDATE` en esas tablas no toca ninguna fila de `productos`, así que `Observar("productos", …)` solo no alcanza para que esas columnas se actualicen en vivo.

`CargarDatosAsync` suscribe además cada tabla de `TablasDeJoin` (tabla → clave de `CatalogoCache`):

```csharp
foreach (var (tabla, clave) in TablasDeJoin)
    Observar(tabla, _ => OnCambioCatalogo(clave));

private void OnCambioCatalogo(string claveCache)
{
    if (Disposed) return;
    CatalogoCache.Invalidar(claveCache);
    _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: false);
}
```

> [!warning] Requiere que la tabla esté publicada en Realtime
> `Observar` no falla si la tabla no está en `supabase_realtime` — simplemente nunca llega ningún evento, en silencio. Antes de agregar una tabla nueva a `TablasDeJoin`, confirmar que está publicada:
> ```sql
> select tablename from pg_publication_tables where pubname = 'supabase_realtime';
> ```
> Fue justo la causa de que esto no funcionara hasta 2026-08-14: cuatro de las cinco tablas del join no estaban publicadas. Ver [[Sesión 2026-08-14 - Realtime en columnas de join de Productos]] y P-034 en [[Deuda Técnica - Pendientes]].

> [!info] El modal no se beneficia de esto en vivo
> `ProductoModal` recibe el `ProductoDto` que la grilla tenía capturado al abrirse. Si el fabricante cambia mientras el modal está abierto, el modal sigue mostrando el nombre viejo hasta que se cierra y se reabre.

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

> [!tip] Refresco después de guardar (desde 2026-08-13)
> `OnProductoGuardado` llama a `_vm.RefrescarTrasGuardar()`, **no** a `RefrescarDatos()`.
> La diferencia importa: `CargarPaginaAsync` levanta `IsLoading` y la grilla se vacía y
> vuelve — se ve como que el formulario se recarga solo. La vía silenciosa deja las filas
> viejas en pantalla y las reemplaza recién cuando llegan las nuevas. El mismo patrón está
> replicado en los ocho modales de edición.
> Ver [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]].

> [!bug] `ProductoDto.IdProveedor` es el que acota la lupa de fabricantes
> El DTO lleva tanto `Proveedor` (nombre, para mostrar) como `IdProveedor`. Si el modal no
> inicializa `_idProveedor` con él, `Catalogos.Fabricantes(_catalogos, null)` consulta sin
> filtro y la lupa lista **todos** los fabricantes aunque el formulario muestre un proveedor.
> Ese id sale de `Productos.id_Proveedor => Fabricante?.idProveedor`; ya viene en la consulta
> porque el select es `fabricante(*, proveedores(*))`.

> [!tip] Tooltip, recorte "…" y Ctrl+Enter son de todo el modal, no de Productos
> `TextoResponsivo` (recorte/tooltip cuando el campo no entra) y `AtajoGuardar` (Ctrl+Enter
> dispara el Guardar real vía `ButtonAutomationPeer`, sin duplicar su lógica) son
> comportamientos adjuntos cableados en el `Style` compartido `ModalInput`/`ModalCombo` y en la
> raíz de cada modal — cubren los 8 modales de edición, no son específicos de este. Ver
> [[Sesión 2026-08-14 - Tooltip, recorte, orden de tabulacion y atajo Ctrl+Enter en los modales]].

> [!info] Los catálogos de las lupas se revalidan en cada apertura
> `CatalogoCache` devuelve lo cacheado al instante y vuelve a consultar por detrás; solo
> repinta si la lista cambió. No tiene vencimiento por tiempo ni depende de Realtime — las
> tablas de catálogo **no** están publicadas. Ver [[ADR-015 - Cache de catalogos mostrar y revalidar]].

> [!tip] Patrón vigente del buscador (desde 2026-07-28)
> Todo el wiring vive en el ViewModel + un binding. **No hay code-behind de sugerencias.**
>
> ```csharp
> // ViewModel — una sola señal, ya mapeada.
> [ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;
> private readonly SuggestionDebouncer _buscador = new();
>
> private Task RefrescarSugerenciasAsync() => _buscador.EjecutarAsync(
>     _query,
>     async (q, ct) =>
>     {
>         var r = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), ct);
>         return r.Success ? r.Value!.Select(Map).ToList() : null;
>     },
>     items => { SuggestItems = items; HighlightIndex = -1; });
>
> private static SuggestionItemData Map(ProductoDto p) => new() { … };
>
> public void SeleccionarSugerencia(ProductoDto p)
> {
>     _buscador.Cancelar();       // obligatorio: _query es campo, no pasa por el setter
>     _query = "";
>     OnPropertyChanged(nameof(Query));
>     SuggestItems = null;
>     …
> }
>
> protected override void OnDispose() => _buscador.Dispose();
> ```
>
> ```xml
> <controls:SuggestionSearchBox
>     Query="{Binding Query, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
>     SuggestItems="{Binding SuggestItems}"
>     HighlightIndex="{Binding HighlightIndex, Mode=TwoWay}"
>     ItemSelected="SearchBox_ItemSelected"/>
> ```
>
> Lo único que cambia por módulo: **qué repositorio se llama** (la lambda) y **cómo se ve una sugerencia** (`Map`). En el code-behind solo queda `SearchBox_ItemSelected`, porque además hace `SeleccionarEnTabla()` — eso sí es responsabilidad de la vista.
>
> **`null` ≠ lista vacía.** `null` = no hay búsqueda activa (query vacía, error del repositorio, o se acaba de seleccionar) → popup cerrado. Lista **vacía** = se buscó y no se encontró nada → el control abre el popup con el estado **"Sin resultados"**. Nunca normalizar el vacío a `null`: mata ese estado y el usuario no sabe si el buscador llegó a responder.
>
> **Por qué una sola propiedad:** antes había dos (`Suggestions` + `ObservableProperty bool ShowSuggestions`) y la vista las combinaba en un `switch`. Ese `bool` no levanta `PropertyChanged` cuando el valor no cambia, así que con el popup abierto quedaba pegado en `true` y la lista se congelaba en el término anterior. Ver [[Sesión 2026-07-28 - Fix Refresco del Popup de Sugerencias (9 módulos)]] y [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]].

> [!warning] El buscador SIEMPRE es `controls:SuggestionSearchBox`, nunca un `TextBox` plano
> Módulo Usuarios se construyó con un `TextBox` + `TextChanged` en vez del control compartido — mismo look-and-feel roto, sin popup ni navegación por teclado, aunque el ViewModel ya tenía `Suggestions`/`ShowSuggestions`/`HighlightIndex` listos. Se detectó y corrigió el 2026-07-26. Al replicar este módulo, verificar SIEMPRE que la vista use `<controls:SuggestionSearchBox Query="{Binding Query, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" HighlightIndex="{Binding HighlightIndex, Mode=TwoWay}" ItemSelected="SearchBox_ItemSelected"/>` + mapeo `SuggestionItemData` en el code-behind — no un `TextBox` binding directo a `Query`. Ver [[Sesión 2026-07-26 - Fix Buscador Usuarios (SuggestionSearchBox)]].
