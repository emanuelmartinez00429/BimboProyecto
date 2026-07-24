---
title: Strategy Pattern
tags:
  - patron
  - comportamiento
  - dotnet
aliases:
  - Patrón Estrategia
---

# Strategy Pattern

> [!abstract] Definición
> Define una familia de algoritmos, encapsula cada uno y los hace intercambiables. El cliente elige qué estrategia usar en tiempo de ejecución, sin cambiar el código que los usa.

---

## Estructura

```
ISearchStrategy
    ├── ProductoSearchStrategy
    ├── EmpleadoSearchStrategy
    └── ClienteSearchStrategy

SearchStrategyRegistry  ← orquesta todas las estrategias
```

---

## Implementación en Bimbo

```csharp
// Contrato (CapaAplicacion)
public interface ISearchStrategy
{
    string EntityType { get; }
    int    Priority   { get; }
    Task<IEnumerable<SearchResultDto>> SearchAsync(string term, CancellationToken ct);
}

// Estrategia concreta
public class ProductoSearchStrategy : ISearchStrategy
{
    private readonly IRepository<Producto> _repo;
    public string EntityType => "Producto";
    public int    Priority   => 1;

    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string term, CancellationToken ct)
    {
        var productos = await _repo.SearchAsync(term, ct);
        return productos.Select(p => new SearchResultDto { ... });
    }
}
```

Registro en DI:
```csharp
// Múltiples implementaciones del mismo contrato
services.AddScoped<ISearchStrategy, ProductoSearchStrategy>();
services.AddScoped<ISearchStrategy, EmpleadoSearchStrategy>();
services.AddScoped<ISearchStrategy, ClienteSearchStrategy>();
```

---

## Por qué es correcto aquí

> [!success] OCP en acción
> Para agregar búsqueda de **Proveedores**, solo creas `ProveedorSearchStrategy` y la registras en DI. **Cero cambios** en código existente.

| Sin Strategy | Con Strategy |
|---|---|
| `if (tipo == "producto") ...` | Cada estrategia sabe cómo buscarse |
| Modificar clase existente al agregar entidad | Solo agregar nueva clase |
| Lógica mezclada | Lógica separada y testeable |

---

## Relación con otros patrones

- [[CQRS + Mediator]] — El `UniversalSearchHandler` usa el `SearchStrategyRegistry` que agrega todas las estrategias. Strategy + Mediator = buscador desacoplado.
- [[Repository Pattern]] — Cada estrategia inyecta su `IRepository<T>`
- [[Caso 02 - Buscador Universal]] — Caso de uso real completo

---

## Dónde está en el código

```
CapaAplicacion4/Search/
  Strategies/
    ISearchStrategy.cs
    ProductoSearchStrategy.cs
    EmpleadoSearchStrategy.cs
    ClienteSearchStrategy.cs
  Registry/
    SearchStrategyRegistry.cs
  Handlers/
    UniversalSearchHandler.cs  ← MediatR handler
```
