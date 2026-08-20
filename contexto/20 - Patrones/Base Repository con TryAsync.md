---
title: Base Repository con TryAsync
tags:
  - patron
  - manejo-errores
  - repositorio
  - arquitectura
  - dotnet
aliases:
  - TryAsync
  - RepositorioBase
  - Execute Pattern
---

# Base Repository con TryAsync

> [!abstract] El problema central
> Con el [[Result Pattern]] sabemos que los repositorios deben retornar `Result<T>` en vez de lanzar excepciones. Pero si cada método repite su propio `try/catch`, la lógica de captura está dispersa. La solución: una sola clase base donde viva ese `try/catch`, que todos los repos heredan.

---

## La idea

El `try/catch` que convierte excepciones en `Result` no es lógica de negocio — es infraestructura. Vive en **una sola clase base**:

```
Todos los repos
    ↓ heredan
RepositorioBase          ← try/catch vive aquí, una sola vez
    ↓ usa
Result<T> / Result       ← definido en CapaAplicacion.Common
```

---

## Implementación para Bimbo

### `RepositorioBase` — `CapaDatos/Repositories/RepositorioBase.cs`

```csharp
using CapaAplicacion.Common;
using System.Diagnostics;

namespace CapaDatos.Repositories;

public abstract class RepositorioBase
{
    protected static async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> operacion,
        string contexto = "Operación")
    {
        try
        {
            return Result<T>.Ok(await operacion());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[{contexto}] {ex}");
            return Result<T>.Fail($"{contexto}: {ex.Message}");
        }
    }

    protected static async Task<Result> TryAsync(
        Func<Task> operacion,
        string contexto = "Operación")
    {
        try
        {
            await operacion();
            return Result.Ok();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[{contexto}] {ex}");
            return Result.Fail($"{contexto}: {ex.Message}");
        }
    }
}
```

> [!important] Por qué `when (ex is not OperationCanceledException)`
> `OperationCanceledException` (que incluye `TaskCanceledException`) **no es un error** — es señal de cancelación intencional. Si se convierte a `Result.Fail`, el buscador mostraría "Error de red" cada vez que el usuario deja de escribir antes de que termine el debounce. Se re-lanza siempre. Fuente: [Microsoft — Best practices for exceptions](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)

> [!note] Por qué `protected static`
> Los métodos `TryAsync` son `protected static` porque no necesitan estado de instancia. En C#, los miembros `protected static` de una clase base son accesibles desde métodos estáticos de clases derivadas — incluso si los métodos del derivado también son `static`. Esto compila y funciona correctamente.

> [!note] Debug.WriteLine obligatorio
> Sin logging interno, si la UI no chequea `Result.Success` el error desaparece silenciosamente. `Debug.WriteLine` garantiza que siempre quede rastro en la ventana de Output de VS2022, sin agregar dependencias de producción.

---

## Cómo queda un repositorio

**Antes** — `try/catch` repetido en cada método, algunos sin protección:

```csharp
// Escritura: try/catch manual (repetido)
public async Task<Result<int>> CreateAsync(ProductoDto dto, ...)
{
    try { ... return Result<int>.Ok(id); }
    catch (Exception ex) { return Result<int>.Fail(ex.Message); }
}

// Lectura: SIN protección — explota si Supabase falla
public async Task<PagedResult<ProductoDto>> GetPagedAsync(...)
{
    var client = await ConexionSupabase.GetClientAsync();
    // ... query directa, ningún try/catch
}
```

**Después** — hereda `RepositorioBase`, todo usa `TryAsync`:

```csharp
public class ProductoCrudRepository : RepositorioBase, IProductoRepository
{
    // Escritura
    public Task<Result<int>> CreateAsync(ProductoDto dto, ...) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var r = await client.From<Productos>().Insert(MapToModel(dto));
            return r.Models.First().idProducto;
        }, "Crear producto");

    // Lectura — ahora también protegida
    public Task<Result<PagedResult<ProductoDto>>> GetPagedAsync(...) =>
        TryAsync(() => GetPagedInternal(...), "Cargar productos");

    // Lógica interna pura, sin try/catch
    private async Task<PagedResult<ProductoDto>> GetPagedInternal(...)
    {
        var client = await ConexionSupabase.GetClientAsync();
        // ... misma query de antes
    }
}
```

---

## Contrato `IProductoRepository` — lectura también con `Result<T>`

Los métodos de lectura también pueden fallar (sin conexión, timeout). El contrato refleja esa realidad:

```csharp
public interface IProductoRepository
{
    // Lectura — con Result<T>
    Task<Result<PagedResult<ProductoDto>>>   GetPagedAsync(...);
    Task<Result<IReadOnlyList<ProductoDto>>> BuscarSugerenciasAsync(...);
    Task<Result<IReadOnlyList<FiltroItem>>>  GetFabricantesAsync(...);
    Task<Result<IReadOnlyList<FiltroItem>>>  GetPaisesAsync(...);
    Task<Result<IReadOnlyList<FiltroItem>>>  GetCategoriasAsync(...);

    // Escritura — igual que antes
    Task<Result<int>> CreateAsync(ProductoDto dto, ...);
    Task<Result>      UpdateAsync(ProductoDto dto, ...);
    Task<Result>      DeleteAsync(int id, ...);
}
```

---

## Cómo lo consume el ViewModel

Con solo 4 llamadas a `Result<T>` en el ViewModel, el patrón `if (!r.Success)` explícito es más legible que cualquier helper. Un helper (`ref bool ok`, `UnwrapOrFail`, etc.) solo tiene sentido con 10+ llamadas encadenadas.

```csharp
// CargarDatosAsync
var rFab = await _repo.GetFabricantesAsync();
if (!rFab.Success) { ErrorCarga = rFab.Error; IsLoading = false; return; }
Fabricantes = rFab.Value!.ToList();

var rPaises = await _repo.GetPaisesAsync();
if (!rPaises.Success) { ErrorCarga = rPaises.Error; IsLoading = false; return; }
Paises = rPaises.Value!.ToList();

// CargarPaginaAsync
var r = await _repo.GetPagedAsync(_page, PageSize, filtros);
if (!r.Success) { ErrorCarga = r.Error; IsLoading = false; return; }
PageRows = new ObservableCollection<ProductoDto>(r.Value!.Items);

// RefrescarSugerenciasAsync — OperationCanceledException se re-lanza sola
var rSug = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), token);
if (!rSug.Success) { ShowSuggestions = false; return; }
Suggestions = new ObservableCollection<ProductoDto>(rSug.Value!);
```

El ViewModel necesita una propiedad observable de error:
```csharp
[ObservableProperty] private string _errorCarga = "";
```

---

## `ProductoModal.OnLoaded` — eliminar `catch { }` vacíos

```csharp
// ❌ Antes — catch vacío: ComboBox vacío sin notificar al usuario
try { var fab = await _repo.GetFabricantesAsync(); ... }
catch { }

// ✅ Después — Result<T>, el error es explícito
var rFab = await _repo.GetFabricantesAsync();
if (!rFab.Success)
{
    TxtErrorModal.Text   = "No se pudieron cargar los fabricantes.";
    BtnGuardar.IsEnabled = false;
    return;
}
foreach (var f in rFab.Value!)
    CmbFabricanteModal.Items.Add(new ComboBoxItem { Content = f.Nombre, Tag = f.Id });
```

---

## Jerarquía de herencia final

```
RepositorioBase
│   TryAsync<T>()
│   TryAsync()
│
├── ProductoCrudRepository      → IProductoRepository  (CRUD Productos)
│
└── SupabaseRepository<TD,TS>   → IRepository<T>       (Buscador Universal)
    ├── ProductoSearchRepository
    ├── EmpleadoRepository
    └── ClienteRepository
```

---

## Repos legacy — por qué NO migran ahora

> [!warning] Los repos en `CapaDatos/Repositorios/` (legacy) quedan intactos
> Aunque técnicamente pueden heredar `RepositorioBase` (los miembros `protected static` son accesibles desde métodos `static` en clases derivadas), para aprovechar `TryAsync` también necesitarían cambiar sus tipos de retorno de `List<T>` → `Result<T>`. Ese cambio rompe los formularios WinForms que los llaman hoy.
>
> **Estrategia:** Se migran cuando el formulario WinForms correspondiente se convierte a WPF. En ese momento, el formulario WPF nuevo define su interfaz en `CapaAplicacion`, el repositorio la implementa heredando `RepositorioBase`, y el ViewModel maneja `Result<T>`.

---

## Archivos a modificar (plan de implementación)

| # | Archivo | Cambio |
|---|---|---|
| 1 | `CapaDatos/Repositories/RepositorioBase.cs` | **NUEVO** — TryAsync con OperationCanceledException + Debug.WriteLine |
| 2 | `CapaAplicacion4/Productos/Interfaces/IProductoRepository.cs` | Lectura → `Result<T>` |
| 3 | `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs` | Hereda base, usa TryAsync en todos los métodos |
| 4 | `CapaUI/.../Productos/ProductosViewModel.cs` | Maneja `Result<T>`, agrega `ErrorCarga` |
| 5 | `CapaUI/.../Productos/ProductoModal.xaml.cs` | Elimina `catch { }` vacíos |
| 6 | `CapaDatos/Repositories/Search/SupabaseRepository.cs` | Hereda `RepositorioBase` |

---

## Análisis de alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| **`ref bool ok` helper en VM** | No es idiomático en C#. Stateful y confuso. Solo útil con 10+ llamadas encadenadas. |
| **`out T value` helper (TryGetValue style)** | Más limpio que ref, pero innecesario a esta escala (4 calls). |
| **Decorator pattern** | Clase extra por cada repo + más DI. Overkill. |
| **MediatR/Pipeline** | Dependencia grande para un beneficio que TryAsync da gratis. |
| **Global exception handler** | Solo actúa en UI, no protege el flujo entre capas. |

---

## Cronometraje incorporado (2026-08-20)

`TryAsync` es el único punto por el que pasa **toda** llamada de **todo** repositorio — eso lo hace también el lugar correcto para medir cuánto tarda cada round trip real a Supabase, sin instrumentar cada repo por separado.

Ambas sobrecargas envuelven `await operacion()` con un `Stopwatch` y loguean a nivel `Debug` vía Serilog: `"[Repo] {Contexto} — {Ms} ms ({Resultado})"`. El camino de fail-fast (`SinConexion`) también loguea, con 0 ms, para poder distinguir "no había red" de "la red tardó".

> [!important] Por qué a nivel Debug y no Information
> El log de producción corre en `Warning` (`CapaUI/App.config` → `LOG_LEVEL`, default `Warning`). El cronometraje solo se ve si alguien sube el nivel a `Debug` a propósito — así el archivo de log de un usuario normal no se llena con una línea por cada llamada al backend, pero medir la latencia real ante una queja de lentitud es cambiar un valor en `App.config`, no recompilar.

Nació al diagnosticar que ["Seguir pesando" se sentía colgado](../70%20-%20Bitácora%20de%20Cambios/2026-08/Sesión%202026-08-20%20-%20Guardado%20de%20pesajes%20sin%20refetch.md) — el número real de ms por llamada confirmó que el costo era de red (varios round trips secuenciales), no de la BD.

---

## Relaciones

- [[Result Pattern]] — el tipo de retorno que hace posible este patrón
- [[Repository Pattern]] — el patrón que TryAsync protege
- [[Clean Architecture]] — CapaDatos absorbe errores, CapaAplicacion define contratos limpios
- [[Módulo Productos]] — primera implementación real
- [[Guardado sin Refetch - Aplicar en memoria la respuesta del servidor]] — el patrón que usó el cronometraje de acá para confirmar el diagnóstico
- [[Sesión 2026-08-20 - Guardado de pesajes sin refetch]]
