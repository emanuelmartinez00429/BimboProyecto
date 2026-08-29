---
title: Plan de Implementación — Gestor Realtime
type: plan
status: vigente
tags:
  - plan
  - realtime
  - implementacion
  - sprint
date: 2026-05-24
updated: 2026-05-24
summary: Leer Gestor Realtime - Diseño Arquitectónico antes de iniciar cualquier fase.
scope:
  - CapaAplicacion4/Productos/Interfaces
  - CapaAplicacion4/Realtime
  - CapaDatos/Realtime
  - CapaDatos/Repositories/Productos
  - CapaUI/Formularios/Principal
  - CapaUI/Formularios/Principal/Pantallas/Productos
symbols:
  - BaseModel
  - CambioRealtime
  - ConstructionVM
  - Desuscribir
  - Event
  - GestorRealtime
  - GetConteosAsync
  - GetPagedAsync
  - IDisposable
  - IProductoRepository
---

# Plan de Implementación — Gestor Realtime

> [!info] Prerrequisito
> Leer [[Gestor Realtime - Diseño Arquitectónico]] antes de iniciar cualquier fase.

---

## Resumen del plan

> [!success] COMPLETADO — 2026-05-24
> Todas las fases implementadas. 0 errores en CapaAplicacion, CapaDatos y CapaUI.
> Ver [[Sesión 2026-05-24 - Implementación Gestor Realtime]] para detalles de cada fase.

| Fase | Descripción | Archivos | Estado |
|---|---|---|---|
| 1 | Contrato + DTO en CapaAplicacion4 | 2 nuevos | ✅ Completada (2026-05-23) |
| 2 | RealtimeService en CapaDatos | 1 nuevo, 3 errores corregidos | ✅ Completada (2026-05-24) |
| 3 | Registro en DI | 1 modificado | ✅ Completada (2026-05-24) |
| 4 | MainViewModel lifecycle (Dispose) | 1 modificado | ✅ Completada (2026-05-24) |
| 5 | Integración en ProductosViewModel | 1 modificado | ✅ Completada (2026-05-24) |
| 6 | RefrescarConteosAsync (simplificado) | Sin cambios al contrato | ✅ Completada (2026-05-24) |
| 7 | GestorRealtime [Obsolete] | 1 modificado | ✅ Completada (2026-05-24) |

---

## Fase 1 — Contrato + DTO en CapaAplicacion4

> [!tip] Sin dependencias externas — puede implementarse primero sin afectar nada

### 1.1 Crear `CambioRealtime` (DTO)

**Archivo:** `CapaAplicacion4/Realtime/CambioRealtime.cs` (NUEVO)

```csharp
namespace CapaAplicacion.Realtime;

/// <summary>
/// DTO inmutable que representa un cambio detectado por Supabase Realtime.
/// El RealtimeService lo construye a partir del PostgresChangesResponse crudo.
/// </summary>
public record CambioRealtime(
    string Operacion,   // "INSERT" | "UPDATE"
    long?  IdRegistro,  // PK del registro afectado
    int?   NuevoEstado  // id_estado del registro nuevo (null si no aplica)
);
```

### 1.2 Crear `IRealtimeService` (Contrato)

**Archivo:** `CapaAplicacion4/Realtime/IRealtimeService.cs` (NUEVO)

```csharp
namespace CapaAplicacion.Realtime;

/// <summary>
/// Facade sobre Supabase Realtime. Gestiona suscripciones por tabla
/// con lifecycle automático de canales y Dispatcher marshaling.
/// </summary>
public interface IRealtimeService
{
    /// <summary>
    /// Suscribe un handler a cambios en la tabla especificada.
    /// Si es el primer suscriptor, abre el canal de Supabase.
    /// El handler se invoca siempre en el UI thread.
    /// </summary>
    Task SuscribirAsync(string tabla, Action<CambioRealtime> handler);

    /// <summary>
    /// Desuscribe un handler. Si era el último suscriptor,
    /// cierra el canal de Supabase automáticamente.
    /// </summary>
    void Desuscribir(string tabla, Action<CambioRealtime> handler);
}
```

### Verificación Fase 1
- [x] Ambos archivos compilan sin errores
- [x] No afectan ningún componente existente (sin dependencias hacia ellos aún)

---

## Fase 2 — RealtimeService en CapaDatos

> [!warning] Esta es la fase más compleja — implementa Facade + Mediator + Observer

### 2.1 Crear `RealtimeService`

**Archivo:** `CapaDatos/Realtime/RealtimeService.cs` (NUEVO)

Responsabilidades:
1. Mantener `Dictionary<string, RealtimeChannel> _canales` — canales abiertos
2. Mantener `Dictionary<string, List<Action<CambioRealtime>>> _suscriptores` — handlers por tabla
3. En `SuscribirAsync`: si no existe canal para la tabla → crearlo via `client.From<T>().On(...)`
4. En `Desuscribir`: quitar handler, si lista queda vacía → `channel.Unsubscribe()` y eliminar del diccionario
5. Al recibir `PostgresChangesResponse`:
   - Extraer `Operacion` del campo `Event` (mapear a "INSERT"/"UPDATE")
   - Extraer `IdRegistro` del record payload
   - Extraer `NuevoEstado` del campo `id_estado` del record (si existe)
   - Construir `CambioRealtime`
   - `Application.Current.Dispatcher.InvokeAsync(...)` → invocar cada handler en UI thread
6. Thread safety con `SemaphoreSlim` para operaciones sobre los diccionarios

### Decisiones técnicas a resolver en esta fase

| Decisión | Opciones | Recomendación |
|---|---|---|
| Cómo mapear `string tabla` a `BaseModel` genérico para `client.From<T>()` | A) Dictionary de factories por tabla, B) Reflection, C) Overload con tipo | A) Dictionary — explícito y sin magia |
| Cómo extraer `IdRegistro` del payload | A) Parse JSON dinámico, B) Convención `id_[tabla]` | A) JSON — más robusto, funciona con cualquier PK |
| Qué hacer si el WebSocket se desconecta | A) Reconectar automático, B) Notificar al ViewModel | A) Supabase client ya reconecta automáticamente (`AutoConnectRealtime = true`) |

### 2.2 Mantener GestorRealtime original (temporal)

No eliminar `GestorRealtime.cs` en esta fase — BimboPesaje aún lo referencia. Marcarlo con `[Obsolete]`:

```csharp
[Obsolete("Usar IRealtimeService vía DI. Este gestor será eliminado cuando BimboPesaje sea descontinuado.")]
public static class GestorRealtime { ... }
```

### Verificación Fase 2
- [x] `RealtimeService` compila e implementa `IRealtimeService`
- [x] `GestorRealtime` sigue compilando para BimboPesaje
- [x] No hay errores en CapaDatos

> [!note] Errores corregidos
> 1. `PostgresChangesOptions(ListenType.All, schema:"public", table:tabla)` → `("public", tabla, ListenType.All)` (orden correcto)
> 2. `Type?.ToString()` → `Type.ToString()` (EventType es struct, no nullable)
> 3. `record is JObject` → `JObject.FromObject(data.Record)` (Record es SocketResponsePayload, no JObject)

---

## Fase 3 — Registro en DI

### 3.1 Registrar en `App.xaml.cs`

**Archivo:** `CapaUI/App.xaml.cs` (MODIFICAR)

```csharp
// En ConfigureServices()
services.AddSingleton<IRealtimeService, RealtimeService>();
```

Requiere:
- `CapaUI.csproj` ya referencia `CapaAplicacion4` y `CapaDatos` → no necesita cambios de proyecto
- Import: `using CapaAplicacion.Realtime;` y `using CapaDatos.Realtime;`

### 3.2 Registrar en `DependencyInjection.cs` de CapaDatos (alternativa)

Si el proyecto usa extensiones `AddDataLayer()`:

**Archivo:** `CapaDatos/DependencyInjection.cs` (MODIFICAR)

```csharp
services.AddSingleton<IRealtimeService, RealtimeService>();
```

### Verificación Fase 3
- [x] La app arranca sin errores de DI
- [x] `IRealtimeService` es resolvible desde el contenedor

> [!note] Se registró en `CapaDatos/DependencyInjection.cs` dentro de `AddDataLayer()`, siguiendo la convención existente.

---

## Fase 4 — MainViewModel lifecycle (Dispose Pattern)

### 4.1 Modificar `Navigate()` para dispose

**Archivo:** `CapaUI/Formularios/Principal/MainViewModel.cs` (MODIFICAR)

```csharp
[RelayCommand]
private void Navigate(string? routeId)
{
    if (string.IsNullOrEmpty(routeId)) return;
    if (!_routes.TryGetValue(routeId, out var factory)) return;

    // Dispose Pattern — limpia suscripciones del VM anterior
    if (VistaActual is IDisposable anterior)
        anterior.Dispose();

    VistaActual = factory();
}
```

### 4.2 Verificar Buscar también

```csharp
[RelayCommand]
private void Buscar(string? term)
{
    if (VistaActual is IDisposable anterior)
        anterior.Dispose();

    VistaActual = _searchVm;
    if (!string.IsNullOrWhiteSpace(term))
        _searchVm.TriggerSearch(term);
}
```

> [!caution] `_searchVm` no debe ser disposed
> El `UniversalSearchViewModel` se reutiliza (no se crea uno nuevo cada vez). NO implementar `IDisposable` en él, o si se implementa, debe soportar re-uso.

### Verificación Fase 4
- [x] Al navegar entre módulos, el VM anterior se dispone
- [x] `WelcomeVM` y `ConstructionVM` no implementan IDisposable → no crash
- [x] `_searchVm` no se dispone al navegar hacia búsqueda

> [!tip] Implementación elegida
> Se usó `partial void OnVistaActualChanging(object? oldValue)` en lugar de modificar `Navigate()` directamente. Esto cubre cualquier asignación a `VistaActual`, incluyendo `Buscar()`. Más robusto que el plan original.

---

## Fase 5 — Integración en ProductosViewModel

> [!important] Primera pantalla real con Realtime

### 5.1 Modificar constructor y agregar IDisposable

**Archivo:** `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosViewModel.cs` (MODIFICAR)

Cambios:
1. Agregar `IRealtimeService` al constructor
2. Implementar `IDisposable`
3. Suscribir en `CargarDatosAsync()`
4. Desuscribir en `Dispose()`
5. Agregar método `OnCambioProductos(CambioRealtime)`

```csharp
public partial class ProductosViewModel : ObservableObject, IDisposable
{
    private readonly IProductoRepository _repo;
    private readonly IRealtimeService    _rt;
    private bool _disposed;

    public ProductosViewModel(IProductoRepository repo, IRealtimeService rt)
    {
        _repo = repo;
        _rt   = rt;
    }

    public async Task CargarDatosAsync()
    {
        // ... carga existente de fabricantes, paises, página ...
        await _rt.SuscribirAsync("productos", OnCambioProductos);
    }

    private void OnCambioProductos(CambioRealtime cambio)
    {
        if (cambio.Operacion == "INSERT")
        {
            _ = CargarPaginaAsync();
            return;
        }

        // UPDATE
        var enPagina = PageRows.FirstOrDefault(p => p.Id == cambio.IdRegistro);

        if (enPagina is not null)
        {
            bool estadoCambio    = cambio.NuevoEstado != enPagina.IdEstado;
            bool filtroPorEstado = _estadoFiltro != EstadoFilter.Todos;

            if (estadoCambio && filtroPorEstado)
                _ = CargarPaginaAsync();   // desaparece — BD llena hueco
            else
                _ = CargarPaginaAsync();   // actualiza datos visibles
        }
        else
        {
            if (cambio.NuevoEstado.HasValue)
                _ = RefrescarConteosAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rt.Desuscribir("productos", OnCambioProductos);
    }
}
```

### 5.2 Actualizar DI registration del VM

**Archivo:** `CapaUI/App.xaml.cs`

El `ProductosViewModel` ahora requiere `IRealtimeService` en su constructor. Como está registrado como `AddTransient<ProductosViewModel>()` y `IRealtimeService` es singleton, DI lo resuelve automáticamente.

### 5.3 Manejar página vacía en CargarPaginaAsync

Agregar al final de `CargarPaginaAsync()`:

```csharp
// Página vacía tras soft-delete en última página
if (PageRows.Count == 0 && _page > 1)
{
    _page--;
    await CargarPaginaAsync();
    return;
}
```

### Verificación Fase 5
- [x] Al abrir Productos → suscribe a "productos"
- [x] Al navegar fuera → MainVM dispone → desuscribe
- [x] Al recibir UPDATE de producto visible → recarga página
- [x] Al recibir UPDATE de producto no visible → solo conteos
- [x] Al deshabilitar último producto de última página → retrocede a página anterior

---

## Fase 6 — RefrescarConteosAsync en repositorio

### 6.1 Agregar método al contrato

**Archivo:** `CapaAplicacion4/Productos/Interfaces/IProductoRepository.cs` (MODIFICAR)

```csharp
Task<Result<(int Total, int Activos, int Inactivos)>> GetConteosAsync(
    ProductoFiltros filtros, CancellationToken ct = default);
```

### 6.2 Implementar en repositorio

**Archivo:** `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs` (MODIFICAR)

Extraer la lógica de conteos que ya existe en `GetPagedAsync` a un método separado reutilizable.

### 6.3 Agregar RefrescarConteosAsync en ViewModel

**Archivo:** `ProductosViewModel.cs` (MODIFICAR)

```csharp
private async Task RefrescarConteosAsync()
{
    var r = await _repo.GetConteosAsync(BuildFiltros());
    if (!r.Success) return;

    TotalCount     = r.Value.Total;
    ActivosCount   = r.Value.Activos;
    InactivosCount = r.Value.Inactivos;

    _filteredCount = _estadoFiltro switch
    {
        EstadoFilter.Habilitados    => r.Value.Activos,
        EstadoFilter.Deshabilitados => r.Value.Inactivos,
        _                           => r.Value.Total
    };

    OnPropertyChanged(nameof(TotalPages));
    OnPropertyChanged(nameof(PageInfo));
}
```

### Verificación Fase 6
- [x] `RefrescarConteosAsync` actualiza indicadores sin recargar la tabla
- [x] Reutiliza `GetPagedAsync` — sin nuevo método en el contrato

> [!note] Simplificación
> Se decidió no agregar `GetConteosAsync` a `IProductoRepository`. `GetPagedAsync` ya devuelve `Total/Activos/Inactivos` en `PagedResult`, suficiente para actualizar indicadores. Se evita complejidad innecesaria en el contrato.

---

## Fase 7 — Verificación final y limpieza

### 7.1 Build completo ✅

```
CapaAplicacion  → 0 errores
CapaDatos       → 0 errores
CapaUI          → 0 errores
BimboPesaje     → 3 errores preexistentes (no tocar)
```

### 7.2 Test manual (pendiente verificación en runtime)

| Escenario | Verificar |
|---|---|
| Abrir Productos | Canal "productos" abierto en debug output |
| Navegar a otra pantalla | Canal "productos" cerrado, nuevo canal abierto |
| Modificar producto desde otra sesión | Fila se actualiza en tiempo real |
| Deshabilitar producto visible (filtro Habilitados) | Producto desaparece, hueco se llena |
| Deshabilitar producto en otra página | Solo conteos cambian |
| Crear producto nuevo | Página se recarga, conteos actualizados |

### 7.3 Marcar GestorRealtime como obsoleto ✅

```csharp
[Obsolete("Usar IRealtimeService vía DI")]
public static class GestorRealtime { ... }
```

No eliminar hasta que BimboPesaje sea descontinuado formalmente.

---

## Orden de ejecución recomendado

```
Fase 1 ──→ Fase 2 ──→ Fase 3 ──→ Fase 4 ──→ Fase 5 ──→ Fase 6 ──→ Fase 7
 (DTO)    (Service)    (DI)     (Navigate)   (ProdVM)   (Conteos)  (Cleanup)
  └─ sin riesgo ─┘     └─ sin riesgo ─┘     └─ riesgo medio ─┘    └─ bajo ─┘
```

> [!success] Cada fase es compilable y desplegable independientemente
> No se rompe nada entre fases. La funcionalidad Realtime se activa en Fase 5 cuando el ViewModel se conecta por primera vez.

---

## Relaciones

- [[Gestor Realtime - Diseño Arquitectónico]] — Diseño completo con diagramas y casos de uso
- [[Observer Pattern]] — Patrón central de suscripción
- [[Repository Pattern]] — Coexiste con Realtime (queries vs notificaciones)
- [[Caso 01 - CRUD con Paginación]] — Lógica de página vacía y paginación server-side
- [[Clean Architecture]] — Separación de contrato (Application) e implementación (Infrastructure)
