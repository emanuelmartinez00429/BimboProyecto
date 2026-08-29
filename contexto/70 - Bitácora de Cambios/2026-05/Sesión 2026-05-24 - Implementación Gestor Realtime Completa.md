---
title: Sesión 2026-05-24 — Implementación Gestor Realtime (Fases 2–7)
type: sesion
status: vigente
tags:
  - sesion
  - realtime
  - implementacion
  - build-fix
  - di
  - disposable
date: 2026-05-24
updated: 2026-05-24
summary: "Las 7 fases del nuevo Gestor Realtime están completadas. CapaUI, CapaDatos y CapaAplicacion compilan con 0 errores."
scope:
  - CapaDatos/Realtime
  - CapaUI/Formularios/Principal
  - CapaUI/Formularios/Principal/Pantallas/Productos
symbols:
  - ConstructionVM
  - GetPagedAsync
  - IDisposable
  - IProductoRepository
  - IRealtimeService
  - JObject
  - OnVistaActualChanging
  - PageRows
  - PostgresChangesOptions
  - ProductoCrudRepository
---

# Sesión 2026-05-24 — Implementación Gestor Realtime (Fases 2–7)

> [!success] Resultado
> Las 7 fases del nuevo Gestor Realtime están completadas. `CapaUI`, `CapaDatos` y `CapaAplicacion` compilan con **0 errores**.

---

## Contexto

La sesión anterior (2026-05-23) había completado la **Fase 1** (contratos + DTO) y había escrito el `RealtimeService.cs` de la **Fase 2**, pero éste tenía 3 errores de compilación derivados de la API real de `Supabase.Realtime 7.0.2`. Esta sesión completa todas las fases restantes.

---

## Fase 2 — Fix de errores de compilación en RealtimeService

**Archivo afectado:** `CapaDatos/Realtime/RealtimeService.cs`

### Error 1 — CS1744: Constructor `PostgresChangesOptions` en orden incorrecto

El constructor real tiene la firma:
```csharp
PostgresChangesOptions(string schema, string table, ListenType eventType, ...)
```

El código erróneo usaba:
```csharp
new PostgresChangesOptions(ListenType.All, schema: "public", table: tabla)
// ListenType.All en posición de "schema" → error
```

**Fix aplicado:**
```csharp
new PostgresChangesOptions("public", tabla, ListenType.All)
```

---

### Error 2 — CS0023: `Type` es `Constants.EventType` (enum), no string

`change.Payload?.Data?.Type` devuelve `Constants.EventType` (struct, no nullable).
Usar `?.` en un tipo valor no nullable es inválido.

**Fix aplicado:**
```csharp
// Antes (error):
string operacion = change.Payload?.Data?.Type ?? "UNKNOWN";

// Después:
string operacion = change.Payload?.Data?.Type.ToString() ?? "UNKNOWN";
```

> [!note] Valores producidos
> `EventType.Insert.ToString()` → `"Insert"` (PascalCase, no `"INSERT"`).
> El smart handler en `ProductosViewModel` usa `is "Insert" or "INSERT"` para compatibilidad.

---

### Error 3 — CS0039: `Record` es `SocketResponsePayload`, no `JObject`

`change.Payload?.Data?.Record` es de tipo `SocketResponsePayload` (objeto complejo de la librería), no un `JObject` casteable directamente.

**Fix aplicado:** usar `JObject.FromObject()` para serializar el objeto:
```csharp
// Antes (error):
var record = change.Payload?.Data?.Record;
if (record is JObject obj)  // CS8121 — tipo incompatible

// Después:
var data = change.Payload?.Data;
var obj  = data?.Record != null ? JObject.FromObject(data.Record) : null;
if (obj != null)
```

> [!tip] Por qué funciona
> `JObject.FromObject()` serializa cualquier objeto usando Newtonsoft.Json. El `SocketResponsePayload` tiene los campos de la fila (`id_producto`, `id_estado`, etc.) como propiedades, que `JObject.FromObject()` convierte a claves JSON accesibles con `obj.Value<T>("columna")`.

---

## Fase 3 — Registro Singleton en DI

**Archivo afectado:** `CapaDatos/DependencyInjection.cs`

```csharp
// Usando añadidos:
using CapaAplicacion.Realtime;
using CapaDatos.Realtime;

// En AddDataLayer():
services.AddSingleton<IRealtimeService, RealtimeService>();
```

**Decisión de ubicación:** Se registró en `DependencyInjection.cs` de `CapaDatos` (junto a los demás servicios de infraestructura) en lugar de `App.xaml.cs`, manteniendo la convención del proyecto (`AddDataLayer()` agrupa todo lo de infraestructura).

**Razón Singleton:** Una sola conexión WebSocket para toda la vida de la app. Los canales se abren y cierran on-demand dentro de esa conexión. Los ViewModels son Transient pero comparten el mismo `IRealtimeService`.

---

## Fase 4 — Dispose Pattern en MainViewModel

**Archivo afectado:** `CapaUI/Formularios/Principal/MainViewModel.cs`

En lugar de modificar el método `Navigate()` directamente, se usa el hook de CommunityToolkit.Mvvm para interceptar el cambio de `VistaActual`:

```csharp
/// <summary>
/// Dispone el ViewModel anterior al cambiar de vista,
/// permitiendo que los VMs liberen suscripciones Realtime.
/// </summary>
partial void OnVistaActualChanging(object? oldValue)
{
    (oldValue as IDisposable)?.Dispose();
}
```

**Por qué `OnVistaActualChanging` y no modificar `Navigate()`:**
- `VistaActual` también cambia desde `Buscar()` y `OnResultadoBusquedaSeleccionado()` — cubrir todos los puntos de cambio con un solo hook es más robusto
- `WelcomeVM` y `ConstructionVM` no implementan `IDisposable` → el cast falla silenciosamente (no crash)
- `_searchVm` tampoco implementa `IDisposable` → seguro de usar en `Buscar()`

---

## Fase 5 — Integración Realtime en ProductosViewModel

**Archivo afectado:** `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosViewModel.cs`

### Cambios al contrato de la clase

```csharp
// Antes:
public partial class ProductosViewModel : ObservableObject

// Después:
public partial class ProductosViewModel : ObservableObject, IDisposable
```

### Inyección de dependencia

```csharp
// Antes:
public ProductosViewModel(IProductoRepository repo)

// Después:
public ProductosViewModel(IProductoRepository repo, IRealtimeService realtime)
```

DI resuelve `IRealtimeService` automáticamente (Singleton) al crear el VM (Transient).

### Suscripción en CargarDatosAsync

```csharp
// Al final de CargarDatosAsync(), después de carga inicial:
await _realtime.SuscribirAsync("productos", OnCambioProducto);
```

Se suscribe **después** de la carga inicial para evitar procesar cambios cuando aún no hay datos en `PageRows`.

### Smart Handler

```csharp
private void OnCambioProducto(CambioRealtime cambio)
{
    if (_disposed) return;

    bool afectaPaginaActual = cambio.IdRegistro.HasValue
        && PageRows.Any(p => p.Id == cambio.IdRegistro.Value);

    if (cambio.Operacion is "Insert" or "INSERT")
    {
        // INSERT: recargar — no se sabe en qué página caerá el nuevo registro
        _ = CargarPaginaAsync();
    }
    else if (cambio.Operacion is "Update" or "UPDATE")
    {
        if (afectaPaginaActual)
            _ = CargarPaginaAsync();   // el cambio afecta una fila visible
        else
            _ = RefrescarConteosAsync(); // el cambio es en otra página → solo conteos
    }
}
```

### RefrescarConteosAsync (simplificación respecto al plan original)

El plan original proponía agregar `GetConteosAsync()` al contrato `IProductoRepository`. Se optó por **reutilizar `GetPagedAsync`** con los mismos parámetros, actualizando solo los conteos y sin tocar la colección `PageRows`:

```csharp
private async Task RefrescarConteosAsync()
{
    var filtros = BuildFiltros();
    var r = await _repo.GetPagedAsync(_page, PageSize, filtros);
    if (!r.Success) return;

    var pagina     = r.Value!;
    TotalCount     = pagina.Total;
    ActivosCount   = pagina.Activos;
    InactivosCount = pagina.Inactivos;
    _filteredCount = filtros.IdEstado switch { ... };

    // Si la página quedó vacía (soft-delete del último elemento), retroceder
    if (PageRows.Count > 0 && pagina.Items.Count == 0 && _page > 1)
    {
        Page = _page - 1;
        return;
    }

    OnPropertyChanged(nameof(TotalPages));
    OnPropertyChanged(nameof(PageInfo));
    OnPropertyChanged(nameof(NoResults));
    NotifyPaginationCanExecuteChanged();
}
```

**Por qué no se agregó `GetConteosAsync()` al contrato:** la query de paginación ya devuelve los conteos, agregar un endpoint solo para conteos habría requerido duplicar lógica en el repositorio. Se puede optimizar en el futuro si el overhead de `GetPagedAsync` resulta medible.

### Dispose

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    _realtime.Desuscribir("productos", OnCambioProducto);
    _searchCts?.Cancel();
    _searchCts?.Dispose();
}
```

Aprovecha el dispose para también cancelar cualquier debounce de búsqueda pendiente.

---

## Fase 6 — RefrescarConteosAsync (simplificada, integrada en Fase 5)

Ver sección Fase 5 arriba. No se modificó `IProductoRepository` ni `ProductoCrudRepository` — se reutilizó `GetPagedAsync` existente.

---

## Fase 7 — GestorRealtime marcado como [Obsolete]

**Archivo afectado:** `CapaDatos/Realtime/GestorRealtime.cs`

```csharp
[Obsolete("Usar IRealtimeService (CapaAplicacion.Realtime) en su lugar. GestorRealtime será eliminado en una versión futura.")]
public static class GestorRealtime { ... }
```

No se elimina porque `BimboPesaje` aún lo referencia. La advertencia informa a futuros desarrolladores de no usarlo en código nuevo.

---

## Resultado final de build

| Proyecto | Errores | Estado |
|---|---|---|
| `CapaAplicacion` | 0 | ✅ |
| `CapaDatos` | 0 | ✅ |
| `CapaUI` | 0 | ✅ |
| `BimboPesaje` | 3 (preexistentes) | ⚠️ No tocar — errores anteriores a esta sesión |

---

## Lecciones aprendidas

### Supabase.Realtime 7.0.2 — API real vs documentación

| Supuesto | Realidad |
|---|---|
| Constructor `PostgresChangesOptions(ListenType, string, string)` | Constructor `(string schema, string table, ListenType eventType)` |
| `Type` devuelve `string?` | `Type` devuelve `Constants.EventType` (struct enum) |
| `Record` es `JObject` | `Record` es `SocketResponsePayload` — requiere `JObject.FromObject()` |
| `EventType.ToString()` devuelve `"INSERT"` | Devuelve `"Insert"` (PascalCase) |

### `OnVistaActualChanging` vs modificar `Navigate()`

El hook de CommunityToolkit `partial void OnXxxChanging(T oldValue)` cubre todos los puntos de asignación a `VistaActual` de forma centralizada y sin repetición.

---

## Relaciones

- [[Gestor Realtime - Diseño Arquitectónico]] — Diseño previo implementado en esta sesión
- [[Plan de Implementación - Gestor Realtime]] — Plan actualizado (todas las fases completadas)
- [[Módulo Productos]] — Primer módulo con Realtime integrado
- [[Sesión 2026-05-23 - Fix LimpiarFiltros Productos]] — Sesión previa que integró eventos en ProductosViewModel
