---
title: Result Pattern
type: patron
status: vigente
tags:
  - patron
  - manejo-errores
  - dotnet
date: 2026-05-21
updated: 2026-05-21
summary: "En lugar de lanzar excepciones para errores esperados, los métodos retornan un objeto Result<T> que puede ser éxito o fallo. El llamador maneja ambos casos…"
scope:
  - CapaAplicacion4/Common
symbols:
  - ErrorMessage
  - ProductoCrudRepository
  - RepositorioProducto
  - RepositorioUsuario
  - Result
  - Result<T>
aliases:
  - Result<T>
  - Railway Oriented Programming
---

# Result Pattern

> [!abstract] Definición
> En lugar de lanzar excepciones para errores esperados, los métodos retornan un objeto `Result<T>` que puede ser éxito o fallo. El llamador maneja ambos casos explícitamente.

---

## El problema actual en Bimbo

```csharp
// ❌ Excepciones como flujo de control
public async Task<PagedResult<ProductoDto>> GetPagedAsync(...)
{
    try { ... }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        throw; // ← se propaga sin contexto hasta la UI
    }
}
```

Si Supabase falla, el ViewModel explota sin poder mostrar un mensaje amigable.

---

## La solución: Result Pattern

```csharp
// El tipo Result
public class Result<T>
{
    public bool    IsSuccess { get; }
    public T?      Value     { get; }
    public string  Error     { get; }

    private Result(T value)              { IsSuccess = true;  Value = value; }
    private Result(string error)         { IsSuccess = false; Error = error; }

    public static Result<T> Ok(T value)      => new(value);
    public static Result<T> Fail(string err) => new(err);
}
```

### Repositorio con Result

```csharp
public async Task<Result<PagedResult<ProductoDto>>> GetPagedAsync(
    int page, int size, ProductoFiltros filtros, CancellationToken ct = default)
{
    try
    {
        var pagina = await /* query supabase */;
        return Result<PagedResult<ProductoDto>>.Ok(pagina);
    }
    catch (Exception ex)
    {
        return Result<PagedResult<ProductoDto>>.Fail($"Error al cargar productos: {ex.Message}");
    }
}
```

### ViewModel con Result

```csharp
private async Task CargarPaginaAsync()
{
    IsLoading = true;
    var result = await _repo.GetPagedAsync(_page, PageSize, BuildFiltros());

    if (!result.IsSuccess)
    {
        ErrorMessage = result.Error;  // se muestra en la UI
        IsLoading = false;
        return;
    }

    PageRows = new ObservableCollection<ProductoDto>(result.Value!.Items);
    // ...
}
```

---

## Beneficios

| Sin Result | Con Result |
|---|---|
| Excepciones sin contexto | Error con mensaje descriptivo |
| try/catch en cada capa | Manejo explícito en el ViewModel |
| UI se rompe silenciosamente | UI muestra mensaje al usuario |
| Difícil de testear | `result.IsSuccess` es fácil de testear |

---

## Estado en el proyecto

> [!success] Parcialmente implementado
> - ✅ `Result.cs` y `Result<T>.cs` creados en `CapaAplicacion4/Common/`
> - ✅ Métodos de **escritura** de `ProductoCrudRepository` retornan `Result`
> - ✅ `ProductoModal.BtnGuardar_Click` maneja `Result` correctamente
> - ❌ Métodos de **lectura** aún no retornan `Result<T>`
> - ❌ Repos legacy (`RepositorioUsuario`, `RepositorioProducto`, etc.) aún usan `throw`
> - ❌ `ProductoModal.OnLoaded` tiene `catch { }` vacíos (peligroso)

> [!tip] Próximo paso
> Ver [[Base Repository con TryAsync]] — centraliza el try/catch en una sola clase base que todos los repos heredan.

---

## Relaciones

- [[Base Repository con TryAsync]] — implementación centralizada del try/catch para todos los repos
- [[Clean Architecture]] — Result fluye entre capas sin acoplar el manejo de errores
- [[Repository Pattern]] — Los repositorios retornan Result<T>
- [[Observer Pattern]] — ViewModel expone `ErrorMessage` que la View observa
- [[SOLID]] — SRP: cada capa maneja sus propios errores
