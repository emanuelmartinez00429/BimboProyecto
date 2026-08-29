---
title: Paginación y Búsqueda — Arquitectura Detallada
type: estado
status: vigente
tags:
  - patron
  - paginacion
  - busqueda
  - realtime
  - arquitectura
date: 2026-05-26
updated: 2026-08-14
summary: "Cómo funcionan la paginación server-side, la búsqueda con sugerencias y su integración con Realtime. Productos es la plantilla de referencia."
summary_fijo: true
scope:
  - CapaAplicacion4/Productos/Queries
symbols:
  - CanExecute
  - CargarPaginaAsync
  - CargarPaginaSilenciosamenteAsync
  - Command
  - Desuscribir
  - GetPagedAsync
  - GetPaginaDeProductoAsync
  - HaySeleccionado
  - IdRegistro
  - ItemsControl
---

# Paginación y Búsqueda — Arquitectura Detallada

> [!abstract]
> Documento de referencia para entender cómo funciona la paginación server-side, la búsqueda con sugerencias, la navegación cross-page y la integración con Realtime en el módulo Productos.
> Primer módulo que implementa este patrón completo — sirve como plantilla para nuevos módulos.

---

## Nodos de arquitectura involucrados

```
┌─────────────────────────────────────────────────────────────────┐
│  CapaUI                                                         │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ ProductosView.xaml          (XAML bindings)              │   │
│  │ ProductosView.xaml.cs       (code-behind: UI pura)       │   │
│  │ ProductosViewModel.cs       (estado + comandos)          │   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────┬──────────────────────────────────────────┘
                       │ IProductoRepository  IRealtimeService
┌──────────────────────▼──────────────────────────────────────────┐
│  CapaAplicacion4                                                │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ IProductoRepository.cs      (contrato completo)          │   │
│  │ PagedResult<T>.cs           (DTO de respuesta paginada)  │   │
│  │ ProductoFiltros.cs          (filtros inmutables)         │   │
│  │ IRealtimeService.cs         (contrato Realtime)          │   │
│  │ CambioRealtime.cs           (DTO de evento)              │   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────┬──────────────────────────────────────────┘
                       │ implementa
┌──────────────────────▼──────────────────────────────────────────┐
│  CapaDatos                                                      │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ ProductoCrudRepository.cs   (paginación, búsqueda, CRUD) │   │
│  │ RealtimeService.cs          (WebSocket, canales)         │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                  │
│                   Supabase.Client (singleton)                   │
└──────────────────────┬──────────────────────────────────────────┘
                       │ REST (PostgREST) + WebSocket
┌──────────────────────▼──────────────────────────────────────────┐
│  Supabase / PostgreSQL                                          │
│  tabla: productos, fabricante, paises, categoria, presentacion  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 1. Paginación Server-Side

### Contrato

```csharp
// IProductoRepository.cs
Task<Result<PagedResult<ProductoDto>>> GetPagedAsync(
    int page, int size, ProductoFiltros filtros, CancellationToken ct = default);
```

### Implementación (CapaDatos)

```csharp
// ProductoCrudRepository.GetPagedInternal()
int from = (page - 1) * size;   // ej. página 3 → from = 100
int to   = from + size - 1;      //                   to  = 149

var pageTask    = query.Order("id_producto", Ascending).Range(from, to).Get();
var conteosTask = GetConteosAsync(filtros, client);   // paralelo
await Task.WhenAll(pageTask, conteosTask);

return new PagedResult<ProductoDto>
{
    Items     = pageTask.Result.Models.Select(Map).ToList(),
    Total     = conteosTask.Result.total,
    Activos   = conteosTask.Result.activos,
    Inactivos = conteosTask.Result.inactivos,
};
```

### Conteos paralelos

```csharp
// GetConteosAsync — 2 queries paralelas (sin CountType.Exact)
var qTotal   = client.From<Productos>().Select("id_producto");  // todos
var qActivos = client.From<Productos>().Select("id_producto")
                     .Filter("id_estado", Equals, "1");         // solo activos

await Task.WhenAll(qTotal.Get(), qActivos.Get());

int total   = totalTask.Result.Models.Count;
int activos = activosTask.Result.Models.Count;
int inact   = total - activos;
```

> [!warning] No usa CountType.Exact
> Se descarga solo la columna `id_producto` de todos los registros. Para tablas pequeñas (< 10,000 filas) esto es aceptable. Si la tabla crece, migrar a `Count(Exact)` con POST al endpoint `/rpc`.

### Orden de asignación en el ViewModel (crítico)

```csharp
// CargarPaginaAsync() — ORDEN OBLIGATORIO:
_filteredCount = ...;               // 1. Primero filteredCount
PageRows = new ObservableCollection<ProductoDto>(pagina.Items);  // 2. Luego PageRows

OnPropertyChanged(nameof(TotalPages));   // 3. Derivadas
OnPropertyChanged(nameof(PageInfo));
```

> [!danger] Si se invierte el orden (PageRows antes que _filteredCount)
> El `PropertyChanged` de `PageRows` dispara `RefrescarPaginacion()` que lee `TotalPages`.
> `TotalPages = Math.Ceiling(_filteredCount / PageSize)`. Si `_filteredCount = 0` (aún no asignado),
> `TotalPages = 1` → la paginación muestra solo `< 1 >` aunque haya 10 páginas.

### Race condition — _loadGeneration

```csharp
private int _loadGeneration;

private async Task CargarPaginaAsync()
{
    int myGen = ++_loadGeneration;   // cada llamada toma un número único
    // ...
    await task;

    if (myGen != _loadGeneration) return;  // llegó respuesta de carga obsoleta → descartar
    // aplicar resultado...
}
```

Protege de respuestas tardías: si el usuario cambia de página rápido (3 clics), solo la última carga actualiza la UI.

### Timeout

```csharp
var task = _repo.GetPagedAsync(_page, PageSize, filtros);
if (await Task.WhenAny(task, Task.Delay(10_000)) != task)
{
    ErrorCarga = "La carga tardó demasiado. Intente de nuevo.";
    IsLoading  = false;
    return;
}
```

---

## 2. Filtros

### Objeto de filtros (inmutable)

```csharp
// ProductoFiltros.cs
public class ProductoFiltros
{
    public int? IdEstado     { get; init; }   // 1=activos, 2=inactivos, null=todos
    public int? IdFabricante { get; init; }
    public int? IdPais       { get; init; }
}
```

### Mapeado desde ViewModel

```csharp
// ProductosViewModel.BuildFiltros()
private ProductoFiltros BuildFiltros() => new()
{
    IdEstado = _estadoFiltro switch
    {
        EstadoFilter.Habilitados    => 1,
        EstadoFilter.Deshabilitados => 2,
        _                           => null     // Todos → sin filtro
    },
    IdFabricante = _fabricanteIdFiltro,
    IdPais       = _paisIdFiltro,
};
```

### Cambio de filtro → reset a página 1

```csharp
public EstadoFilter EstadoFiltro
{
    set
    {
        _estadoFiltro = value;
        _page = 1;             // siempre vuelve a página 1 al filtrar
        _ = CargarPaginaAsync();
    }
}
```

---

## 3. Controles de paginación

### CanExecute automático vía IsLoading

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(PrimeraPaginaCommand))]
[NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
[NotifyCanExecuteChangedFor(nameof(PaginaSiguienteCommand))]
[NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
private bool _isLoading;

private bool PuedePaginaAnterior()  => !IsLoading && _page > 1;
private bool PuedePaginaSiguiente() => !IsLoading && _page < TotalPages;
```

Al asignar `IsLoading = true/false`, los 4 botones se habilitan/deshabilitan automáticamente via CommunityToolkit.

### NotifyPaginationCanExecuteChanged (manual)

```csharp
// Se llama explícitamente porque TotalPages es una computed property,
// no un campo [ObservableProperty], así que no dispara automático.
private void NotifyPaginationCanExecuteChanged()
{
    PrimeraPaginaCommand.NotifyCanExecuteChanged();
    PaginaAnteriorCommand.NotifyCanExecuteChanged();
    PaginaSiguienteCommand.NotifyCanExecuteChanged();
    UltimaPaginaCommand.NotifyCanExecuteChanged();
}
```

### PageInfo (texto visible)

```csharp
public string PageInfo
{
    get
    {
        if (_filteredCount == 0) return "Sin resultados";
        int from = (_page - 1) * PageSize + 1;
        int to   = Math.Min(_page * PageSize, _filteredCount);
        return $"Mostrando {from}–{to} de {_filteredCount} productos";
    }
}
```

---

## 4. Búsqueda con sugerencias

### Flujo completo

```
Usuario escribe en TxtBusqueda
    ↓ TextChanged → _vm.Query = text
    ↓ _buscador.EjecutarAsync(...)      ← SuggestionDebouncer compartido
         ↓ cancela la búsqueda anterior
         ↓ await Task.Delay(300ms, ct)  ← debounce
    ↓ _repo.BuscarSugerenciasAsync(q, BuildFiltros(), ct)
         ↓ Supabase: Filter("nombre_producto", ILike, "%q%").Limit(10)
    → SuggestItems = results.Select(Map).ToList()    ← UNA señal, ya mapeada
    ↓ binding SuggestItems="{Binding SuggestItems}"
    → SuggestionSearchBox.OnSuggestItemsChanged → Popup visible
```

> [!danger] Una sola señal, nunca un `bool` de "mostrar"
> El diseño original tenía **dos** propiedades (`Suggestions` + `ShowSuggestions`) para un solo hecho, y la vista las combinaba a mano en el `switch` de `OnVmPropertyChanged`. `ShowSuggestions` era un `bool` con `[ObservableProperty]`, que **no levanta `PropertyChanged` cuando el valor no cambia**: con el popup abierto quedaba pegado en `true`, la búsqueda corría, `Suggestions` se reemplazaba… y moría ahí. La lista visible se congelaba en el término anterior y solo se recuperaba borrando todo el texto.
>
> Desde 2026-07-28 el ViewModel expone **`SuggestItems` y nada más** (ya mapeada; `null` o vacía = popup cerrado) y el control se bindea directo. No hay `switch` que olvidar. Ver [[Sesión 2026-07-28 - Fix Refresco del Popup de Sugerencias (9 módulos)]] y [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]].

### Implementación de sugerencias (CapaDatos)

```csharp
// BuscarSugerenciasInternal — busca en nombre Y código
var resultado = await query
    .Or(new List<IPostgrestQueryFilter>
    {
        new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
        new QueryFilter("codigo_producto",  Op.ILike, $"%{termino}%"),
    })
    .Order("nombre_producto", Ascending)
    .Limit(10)
    .Get();
```

Respeta los filtros activos (estado, fabricante, país). Busca en `nombre_producto` **y** `codigo_producto`.

> [!warning] No usar `Filter("or", Op.Equals, "...")`
> Ese patrón genera `?or=eq.(...)` — URL inválida para PostgREST, retorna 0 resultados silenciosamente.
> Ver [[Bug - Filter OR con Op.Equals en postgrest-csharp]].

---

## 5. Navegación cross-page desde sugerencias

Esta es la funcionalidad más compleja de la búsqueda.

### Problema

Las sugerencias devuelven productos de **cualquier página**. Si el usuario selecciona una sugerencia que está en la página 7 y la vista muestra la página 1, necesitamos:
1. Calcular en qué página está ese producto
2. Navegar a esa página
3. Seleccionar el producto una vez que la página cargue

### Solución: `_pendingSelectionId` + `GetPaginaDeProductoAsync`

```csharp
// ProductosViewModel.SeleccionarSugerencia()
public void SeleccionarSugerencia(ProductoDto p)
{
    // Obligatorio: se asigna al CAMPO _query, no a la propiedad, así que no se
    // pasa por el setter y nadie más cancelaría el debounce en vuelo. Sin esto,
    // una búsqueda en curso termina después de la selección y reabre el popup
    // con la caja de texto ya vacía.
    _buscador.Cancelar();

    _query = "";
    SuggestItems = null;

    var enPagina = PageRows.FirstOrDefault(x => x.Id == p.Id);
    if (enPagina is not null)
    {
        Seleccionado = enPagina;   // CASO A: ya está en pantalla
        return;
    }

    // CASO B: está en otra página
    _pendingSelectionId = p.Id;
    _ = NavegarAPaginaDeProductoAsync(p.Id);
}

private async Task NavegarAPaginaDeProductoAsync(int idProducto)
{
    var r = await _repo.GetPaginaDeProductoAsync(idProducto, PageSize, BuildFiltros());
    if (!r.Success) { _pendingSelectionId = null; ErrorCarga = r.Error; return; }

    // Asigna _page directamente (no la propiedad Page) para evitar
    // disparar CargarPaginaAsync() dos veces
    _page = r.Value;
    OnPropertyChanged(nameof(Page));
    OnPropertyChanged(nameof(PageInfo));
    OnPropertyChanged(nameof(TotalPages));
    NotifyPaginationCanExecuteChanged();

    await CargarPaginaAsync();
    // CargarPaginaAsync al finalizar consume _pendingSelectionId:
    // Seleccionado = PageRows.FirstOrDefault(x => x.Id == _pendingSelectionId.Value);
    // _pendingSelectionId = null;
}
```

### Cálculo de página (CapaDatos)

```csharp
// GetPaginaDeProductoInternal
var query = client.From<Productos>()
    .Select("id_producto")
    .Filter("id_producto", Op.LessThan, idProducto.ToString());
// + filtros activos (estado, fabricante, país)

var result  = await query.Get();
int previos = result.Models.Count;           // registros con id < idProducto
return (previos / size) + 1;                 // página 1-based
```

> [!important] Supuesto de ordenación
> Este cálculo solo es correcto si los datos están ordenados por `id_producto ASC` (que es el orden de `GetPagedAsync`). Si el orden cambia, `GetPaginaDeProductoAsync` también debe cambiar.

### Diagrama de estados

```
[Sugerencia seleccionada]
        │
        ▼
¿p.Id en PageRows?
    ├── SÍ → Seleccionado = enPagina   (fin, sin carga)
    │
    └── NO → _pendingSelectionId = p.Id
             GetPaginaDeProductoAsync(p.Id)
                    │
                    ▼
             _page = página calculada
             CargarPaginaAsync()
                    │
                    ▼
             ¿_pendingSelectionId.HasValue?
             └── SÍ → Seleccionado = PageRows.First(x.Id == _pendingSelectionId)
                       _pendingSelectionId = null
```

---

## 6. Integración Realtime

> [!warning] Desactualizado hasta 2026-08-14
> Esta sección tenía pseudocódigo de una versión anterior de `OnCambioProducto` (INSERT siempre recargaba con spinner vía `CargarPaginaAsync`). Desde la sesión "Realtime Silent Refresh Productos" (2026-05-26) el INSERT usa `CargarPaginaSilenciosamenteAsync` (sin spinner) con una heurística que decide si tocar `PageRows` o no. Lo de abajo refleja el código real.

### Flujo de evento

```
PostgreSQL (INSERT o UPDATE en tabla "productos")
    ↓ Supabase Realtime WebSocket
    ↓ RealtimeService.OnCambioRecibido()
         ExtraerCambio():
           operacion = change.Payload.Data.Type.ToString()  → "Insert" / "Update"
           id        = obj["id_producto"].Value<long?>()
           estado    = obj["id_estado"].Value<int?>()
         → CambioRealtime { Operacion, IdRegistro, NuevoEstado }
    ↓ SynchronizationContext.Post → UI thread
    ↓ ProductosViewModel.OnCambioProducto(cambio)
```

### Handler en ViewModel

```csharp
private void OnCambioProducto(CambioRealtime cambio)
{
    if (Disposed) return;
    bool afectaPaginaActual = cambio.IdRegistro.HasValue
        && PageRows.Any(p => p.Id == cambio.IdRegistro.Value);

    if (string.Equals(cambio.Operacion, "INSERT", StringComparison.OrdinalIgnoreCase))
        _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: true);
    else if (string.Equals(cambio.Operacion, "UPDATE", StringComparison.OrdinalIgnoreCase))
    {
        if (afectaPaginaActual || !cambio.IdRegistro.HasValue)
            _ = CargarPaginaSilenciosamenteAsync(actualizarFilas: true, esInsert: false);
        else
            _ = RefrescarConteosAsync();
    }
}
```

No hay `case` para `DELETE` — queda sin manejar en este handler.

### `CargarPaginaSilenciosamenteAsync` — el método clave

```csharp
int nuevoFilteredCount = ResolverFilteredCount(pagina, filtros);
int nuevoTotalPages    = Math.Max(1, (int)Math.Ceiling(nuevoFilteredCount / (double)PageSize));

// A propósito: un INSERT que crea una página nueva mientras el usuario está
// parado en la vieja última página NO reasigna PageRows — no le saca de abajo
// las filas que está mirando.
bool debeActualizarFilas = actualizarFilas && (!esInsert || _page == nuevoTotalPages);

TotalCount     = pagina.Total;
ActivosCount   = pagina.Activos;
InactivosCount = pagina.Inactivos;
_filteredCount = nuevoFilteredCount;

if (debeActualizarFilas)
{
    int? idSeleccionadoAntes = Seleccionado?.Id;
    PageRows = new ObservableCollection<ProductoDto>(pagina.Items);
    if (idSeleccionadoAntes.HasValue)
        Seleccionado = PageRows.FirstOrDefault(x => x.Id == idSeleccionadoAntes.Value);
}

OnPropertyChanged(nameof(TotalPages));
OnPropertyChanged(nameof(PageInfo));
OnPropertyChanged(nameof(NoResults));
NotifyPaginationCanExecuteChanged();
```

`RefrescarConteosAsync` (UPDATE fuera de la página visible) sigue el mismo patrón — actualiza conteos y notifica `TotalPages`/`PageInfo`/`NoResults`/`CanExecute`, pero nunca reasigna `PageRows` salvo el caso especial de retroceder una página si la actual quedó vacía por soft-delete.

> [!danger] El gap que esto dejaba: los botones numerados de página
> `TotalPages` **sí** se recalcula y notifica correctamente en el ViewModel en el caso de arriba (`debeActualizarFilas = false`). Los botones `«/‹/›/»` (bindeados a `Command`, con `CanExecute` leyendo `TotalPages` directo) también quedan correctos.
>
> Pero el `ItemsControl` de números de página (`PaginacionPanel` en el XAML, poblado a mano por `RefrescarPaginacion()` en el code-behind) **solo se reconstruía cuando cambiaba `PageRows`** — el `switch` de `OnVmPropertyChanged` no tenía ningún `case` para `TotalPages`. Resultado: un INSERT que crecía el total de páginas mientras el usuario estaba en la última página vieja dejaba los botones numerados con el árbol viejo (ej. `1…10,11`) hasta cerrar y reabrir el módulo — aunque tocar "siguiente" internamente ya llevara a una página que el ViewModel sabía que existía.
>
> **Corregido el 2026-08-14** agregando `case nameof(XxxViewModel.TotalPages): RefrescarPaginacion(); break;` junto al `case` de `PageRows` en los 6 módulos que comparten este patrón (Productos, Categorías, Fabricantes, Proveedores, ContactosFabricantes, ContactosProveedores). Ver [[Sesión 2026-08-14 - Fix boton de paginacion desincronizado de Realtime]].

### RefrescarConteosAsync (solo chips, sin tocar tabla)

```csharp
private async Task RefrescarConteosAsync()
{
    var r = await _repo.GetPagedAsync(_page, PageSize, BuildFiltros());
    if (!r.Success) return;

    var pagina = r.Value!;
    TotalCount     = pagina.Total;
    ActivosCount   = pagina.Activos;
    InactivosCount = pagina.Inactivos;
    _filteredCount = ResolverFilteredCount(pagina, filtros);

    // Si la página actual quedó vacía (soft-delete del último elemento)
    if (PageRows.Count > 0 && pagina.Items.Count == 0 && _page > 1)
    {
        Page = _page - 1;   // retrocede automáticamente
        return;
    }

    OnPropertyChanged(nameof(TotalPages));
    OnPropertyChanged(nameof(PageInfo));
    OnPropertyChanged(nameof(NoResults));
    NotifyPaginationCanExecuteChanged();
}
```

### Tabla de decisiones Realtime

| Evento | `IdRegistro` en `PageRows` | Acción |
|---|---|---|
| INSERT | — | `CargarPaginaAsync()` siempre |
| UPDATE | Sí (fila visible) | `CargarPaginaAsync()` |
| UPDATE | No (otra página) | `RefrescarConteosAsync()` |
| UPDATE | null (error payload) | `CargarPaginaAsync()` (seguro) |

---

## 7. Lifecycle del canal Realtime

```
CargarDatosAsync()
    └── await _realtime.SuscribirAsync("productos", OnCambioProducto)
            ↓ (primer suscriptor) → AbrirCanalAsync("productos")
                 channel = client.Realtime.Channel("rt-productos")
                 channel.Register(PostgresChangesOptions("public", "productos", All))
                 await channel.Subscribe()

Navegar fuera de Productos
    ↓ MainViewModel.OnVistaActualChanging(oldValue)
    ↓ (oldValue as IDisposable)?.Dispose()
    ↓ ProductosViewModel.Dispose()
            _realtime.Desuscribir("productos", OnCambioProducto)
                ↓ (último suscriptor) → channel.Unsubscribe()
                ↓ _canales.Remove("productos")
```

> [!tip] Thread safety en RealtimeService
> `SuscribirAsync` y `Desuscribir` usan `SemaphoreSlim(1,1)` para proteger los diccionarios `_canales` y `_suscriptores`. `Desuscribir` usa `_lock.Wait()` (sync) porque se llama desde `Dispose()`.

---

## 8. PagedResult — DTO de respuesta

```csharp
// CapaAplicacion4/Productos/Queries/PagedResult.cs
public class PagedResult<T>
{
    public IReadOnlyList<T> Items     { get; init; } = [];
    public int              Total     { get; init; }   // total en BD (con filtros fab/país)
    public int              Activos   { get; init; }   // id_estado = 1
    public int              Inactivos { get; init; }   // id_estado = 2
}
```

`Total` es el total respetando filtros de fabricante/país pero **sin** filtrar por estado. Esto permite mostrar los 3 chips (Total / Activos / Inactivos) con una sola consulta.

---

## 9. Propiedades derivadas del ViewModel

| Propiedad | Cómo se calcula | Cuándo se notifica |
|---|---|---|
| `TotalPages` | `Math.Ceiling(_filteredCount / PageSize)` | Manualmente tras cambiar `_filteredCount` |
| `PageInfo` | `"Mostrando X–Y de Z"` | Manualmente (mismo punto) |
| `NoResults` | `!IsLoading && _filteredCount == 0 && TotalCount > 0` | Manualmente |
| `HaySeleccionado` | `Seleccionado is not null` | `[NotifyPropertyChangedFor]` automático |
| `TextoSeleccionado` | `$"{Codigo} · {Nombre}"` | `[NotifyPropertyChangedFor]` automático |

---

## 10. Variante: búsqueda cross-tabla (vista SQL)

El patrón `.Or()` de las secciones anteriores solo funciona entre **columnas de la misma tabla**. Cuando el término de búsqueda vive en una tabla joineada (ej. Usuarios busca por `alias_usuario` propio O `nombre_empleado` de `empleados`), PostgREST **no permite** ese OR mixto.

**Solución estándar del proyecto** (primer uso: P-021, módulo Usuarios):

1. Vista SQL con `security_invoker = true` que aplana la(s) columna(s) del JOIN:
   ```sql
   create or replace view public.vista_usuarios_busqueda
   with (security_invoker = true) as
   select u.*, trim(coalesce(e.nombre_empleado,'') || ' ' || coalesce(e.apellido_empleado,'')) as nombre_completo
   from public.usuarios u
   left join public.empleados e using (id_empleado);
   ```
2. El modelo C# de lectura apunta a la vista (`[Table("vista_usuarios_busqueda")]`) y agrega la columna aplanada.
3. El OR vuelve a ser el patrón trivial de 2 columnas — página y conteos usan el **mismo** filtro para que coincidan.
4. Las escrituras siguen yendo al modelo de la tabla real; la vista es solo lectura.

Detalles, trade-offs y checklist de seguridad: [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] y [[Supabase - Vistas SQL, RLS y security_invoker]]. Aplicable a futuros buscadores de Movimientos/Empleados.

---

## Relaciones

- [[Módulo Productos]] — Vista general del módulo
- [[Gestor Realtime - Diseño Arquitectónico]] — Diseño del servicio Realtime
- [[Repository Pattern]] — IProductoRepository como contrato de capa
- [[Result Pattern]] — Result<T> en todas las operaciones
- [[Sesión 2026-05-24 - Implementación Gestor Realtime Completa]] — Implementación del Realtime
- [[Bug - Filter OR con Op.Equals en postgrest-csharp]] — Trampa silenciosa, patrón correcto para OR ILike
- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] — Variante cross-tabla (sección 10)
- [[Supabase - Vistas SQL, RLS y security_invoker]] — Seguridad de vistas expuestas
