---
title: Sesión 2026-05-24 — Implementación Completa del Gestor Realtime
type: sesion
status: vigente
tags:
  - sesion
  - realtime
  - implementacion
  - productosviewmodel
  - di
date: 2026-05-24
updated: 2026-05-24
summary: "Las 7 fases del Plan de Implementación - Gestor Realtime completadas. 0 errores de compilación en CapaAplicacion, CapaDatos y CapaUI."
scope:
  - CapaDatos/Realtime
  - CapaUI/Formularios/Principal
  - CapaUI/Formularios/Principal/Pantallas/Productos
symbols:
  - Activos
  - CambioRealtime
  - Data
  - EventType
  - GestorNotificaciones
  - GetConteosAsync
  - GetPagedAsync
  - IProductoRepository
  - IRealtimeService
  - Inactivos
---

# Sesión 2026-05-24 — Implementación Completa del Gestor Realtime

> [!success] Resultado
> Las 7 fases del [[Plan de Implementación - Gestor Realtime]] completadas. 0 errores de compilación en CapaAplicacion, CapaDatos y CapaUI.

---

## Contexto

Se retomó desde la sesión anterior donde Fase 1 había sido completada (contratos `CambioRealtime` + `IRealtimeService`) y Fase 2 había sido escrita pero con 3 errores de compilación en `RealtimeService.cs`.

---

## Fase 2 — Corrección de errores en RealtimeService

### Error 1 — CS1744: Constructor `PostgresChangesOptions`

**Causa:** El constructor recibe `(string schema, string table, ListenType eventType)` en ese orden. El código original pasaba `(ListenType.All, schema: "public", table: tabla)` — primer argumento posicional incorrecto.

**Fix:**
```csharp
// Antes (error)
channel.Register(new PostgresChangesOptions(ListenType.All, schema: "public", table: tabla));

// Después (correcto)
channel.Register(new PostgresChangesOptions("public", tabla, ListenType.All));
```

### Error 2 — CS0023: `EventType` es struct no nullable

**Causa:** `change.Payload?.Data?.Type` devuelve `Constants.EventType` (struct, no nullable), por lo que `?.ToString()` en un struct no nullable generaba error `El operador '?' no se puede aplicar`.

**Fix:**
```csharp
// Antes (error)
string operacion = change.Payload?.Data?.Type?.ToString() ?? "UNKNOWN";

// Después (correcto)
string operacion = change.Payload?.Data?.Type.ToString() ?? "UNKNOWN";
```

> [!note] Investigación
> Confirmado vía `GestorNotificaciones.cs` que el patrón correcto es `change.Payload?.Data?.Type == EventType.Insert` — la cadena nullable termina en `Data`, no en `Type`.

### Error 3 — CS0039: `SocketResponsePayload` no es `JObject`

**Causa:** `change.Payload?.Data?.Record` es de tipo `SocketResponsePayload` (un objeto del SDK), no un `JObject`. El `as JObject` o el `is JObject` fallaban en tiempo de compilación.

**Fix:**
```csharp
// Antes (error)
var record = change.Payload?.Data?.Record;
if (record is JObject obj) { ... }

// Después (correcto)
var data = change.Payload?.Data;
var obj = data?.Record != null ? JObject.FromObject(data.Record) : null;
if (obj != null) { ... }
```

`JObject.FromObject()` serializa el objeto del SDK a JObject de Newtonsoft, permitiendo extraer columnas por nombre.

---

## Fase 3 — Registro en DI

**Archivo modificado:** `CapaDatos/DependencyInjection.cs`

```csharp
// Realtime — singleton: una sola conexión WebSocket, canales on-demand
services.AddSingleton<IRealtimeService, RealtimeService>();
```

Se registró en `AddDataLayer()` junto a los demás servicios de infraestructura, siguiendo la convención existente (no en `App.xaml.cs` directamente).

---

## Fase 4 — MainViewModel Dispose lifecycle

**Archivo modificado:** `CapaUI/Formularios/Principal/MainViewModel.cs`

Se usó el hook partial de CommunityToolkit.Mvvm generado por `[ObservableProperty]`:

```csharp
partial void OnVistaActualChanging(object? oldValue)
{
    (oldValue as IDisposable)?.Dispose();
}
```

> [!tip] Decisión de diseño
> En lugar de modificar el método `Navigate()` directamente (como planeaba la Fase 4 original), se usó `OnVistaActualChanging` — el partial hook que el source generator expone para la propiedad `_vistaActual`. Esto es más robusto: cubre **cualquier** asignación a `VistaActual`, no solo la que ocurre en `Navigate()`. También cubre el caso donde `Buscar()` asigna `_searchVm` directamente.

---

## Fase 5 — Integración en ProductosViewModel

**Archivo modificado:** `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosViewModel.cs`

### Cambios en la clase

```csharp
public partial class ProductosViewModel : ObservableObject, IDisposable
{
    private readonly IProductoRepository _repo;
    private readonly IRealtimeService    _realtime;
    private bool _disposed;

    public ProductosViewModel(IProductoRepository repo, IRealtimeService realtime)
    {
        _repo     = repo;
        _realtime = realtime;
    }
```

### Suscripción en CargarDatosAsync

```csharp
// Al final de CargarDatosAsync, después de la carga inicial
await _realtime.SuscribirAsync("productos", OnCambioProducto);
```

### Smart handler

```csharp
private void OnCambioProducto(CambioRealtime cambio)
{
    if (_disposed) return;

    bool afectaPaginaActual = cambio.IdRegistro.HasValue
        && PageRows.Any(p => p.Id == cambio.IdRegistro.Value);

    if (cambio.Operacion is "Insert" or "INSERT")
    {
        _ = CargarPaginaAsync();
    }
    else if (cambio.Operacion is "Update" or "UPDATE")
    {
        if (afectaPaginaActual)
            _ = CargarPaginaAsync();
        else
            _ = RefrescarConteosAsync();
    }
}
```

> [!note] Doble check de operación
> Se incluyen tanto `"Insert"` como `"INSERT"` porque `EventType.Insert.ToString()` podría devolver cualquiera según la versión del SDK. Se verificó que el enum usa PascalCase internamente pero se protege con ambas variantes por robustez.

### IDisposable

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

---

## Fase 6 — RefrescarConteosAsync (simplificada)

Se decidió **NO agregar un nuevo método al contrato `IProductoRepository`** ni una query separada de conteos. En su lugar, `RefrescarConteosAsync` reutiliza `GetPagedAsync` (que ya devuelve `Total`, `Activos`, `Inactivos` en el `PagedResult`) y solo actualiza los indicadores, más lógica de retroceso de página si quedó vacía:

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

    // Si la página actual quedó vacía, retroceder
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

> [!info] Razón de la simplificación
> Agregar `GetConteosAsync` al contrato habría requerido: nuevo método en `IProductoRepository`, implementación en `ProductoCrudRepository`, y nueva query a Supabase. Dado que `GetPagedAsync` ya retorna los conteos junto con los items, el overhead es mínimo y se evita complejidad innecesaria en el contrato. Si en el futuro el volumen de datos justifica una query más ligera, se puede agregar entonces.

---

## Fase 7 — GestorRealtime marcado como Obsoleto

**Archivo modificado:** `CapaDatos/Realtime/GestorRealtime.cs`

```csharp
[Obsolete("Usar IRealtimeService (CapaAplicacion.Realtime) en su lugar. GestorRealtime será eliminado en una versión futura.")]
public static class GestorRealtime { ... }
```

No se elimina porque `BimboPesaje` y `GestorNotificaciones` lo siguen referenciando. Se mantiene hasta la descontinuación formal de BimboPesaje.

---

## Resultado del build final

| Proyecto | Errores | Advertencias |
|---|---|---|
| CapaAplicacion | 0 | 0 |
| CapaDatos | 0 | 35 (preexistentes, CS8618 nullable) |
| CapaUI | 0 | existentes |
| BimboPesaje | 3 (preexistentes, no tocar) | — |

---

## Archivos modificados en esta sesión

| Archivo | Tipo de cambio |
|---|---|
| `CapaDatos/Realtime/RealtimeService.cs` | Fix 3 errores de compilación |
| `CapaDatos/DependencyInjection.cs` | Registro Singleton IRealtimeService |
| `CapaUI/Formularios/Principal/MainViewModel.cs` | OnVistaActualChanging → Dispose |
| `CapaUI/.../Productos/ProductosViewModel.cs` | IDisposable + smart handler Realtime |
| `CapaDatos/Realtime/GestorRealtime.cs` | `[Obsolete]` attr |

---

## Relaciones

- [[Plan de Implementación - Gestor Realtime]] — Plan completo con todas las fases
- [[Gestor Realtime - Diseño Arquitectónico]] — Diseño con diagramas y casos de uso
- [[Módulo Productos]] — Módulo donde se integró el Realtime primero
- [[Observer Pattern]] — Patrón base de la suscripción
