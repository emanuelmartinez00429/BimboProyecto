---
title: CQRS + Mediator
type: patron
status: vigente
tags:
  - patron
  - arquitectura
  - dotnet
date: 2026-05-21
updated: 2026-05-21
summary: "CQRS (Command Query Responsibility Segregation): separa las operaciones de lectura (queries) de las de escritura (commands) en modelos distintos. Mediator: un…"
scope: []
symbols:
  - IProductoRepository
  - ProductoDto
  - ProductosInsertar
aliases:
  - CQRS
  - Mediator Pattern
  - MediatR
---

# CQRS + Mediator Pattern

> [!abstract] Definición
> **CQRS** (Command Query Responsibility Segregation): separa las operaciones de **lectura** (queries) de las de **escritura** (commands) en modelos distintos.
> **Mediator**: un objeto central que recibe solicitudes y las enruta al handler correcto, sin que el emisor conozca al receptor.

---

## Cómo se usa en Bimbo

```
UI (SearchBox)
    ↓ envía UniversalSearchQuery
MediatR (Mediator)
    ↓ enruta al handler
UniversalSearchHandler
    ↓ usa
SearchStrategyRegistry
    ↓ ejecuta todas las estrategias en paralelo
[ProductoSearchStrategy, EmpleadoSearchStrategy, ClienteSearchStrategy]
    ↓ cada una usa su IRepository<T>
Supabase (server-side filter)
```

---

## Implementación

```csharp
// Query (lo que se pregunta)
public record UniversalSearchQuery(string Term) : IRequest<IEnumerable<SearchResultDto>>;

// Handler (quien responde)
public class UniversalSearchHandler : IRequestHandler<UniversalSearchQuery, IEnumerable<SearchResultDto>>
{
    private readonly SearchStrategyRegistry _registry;

    public async Task<IEnumerable<SearchResultDto>> Handle(
        UniversalSearchQuery request, CancellationToken ct)
    {
        var results = await _registry.SearchAllAsync(request.Term, ct);
        return results.OrderBy(r => r.EntityType);
    }
}
```

---

## CQRS: dos rutas, dos modelos

| Ruta | Propósito | Modelo | Repositorio |
|---|---|---|---|
| **Query** | Leer / buscar / mostrar | `ProductoDto` | `IProductoRepository` |
| **Command** | Crear / editar / eliminar | `ProductosInsertar` | `RepositorioProducto.ingresarProducto` |

> [!tip] Beneficio de separar
> El modelo de lectura (`ProductoDto`) puede tener campos calculados, joins y display names. El modelo de escritura solo tiene los campos que la BD necesita. Nunca se mezclan.

---

## MediatR vs Custom Dispatcher

> [!warning] MediatR fue comercial en julio 2025
> Si el proyecto escala, considerar un dispatcher custom que benchmarks 4.4x más rápido.

```csharp
// Dispatcher custom simple (sin dependencia externa)
public class SearchDispatcher
{
    private readonly IEnumerable<ISearchStrategy> _strategies;
    public SearchDispatcher(IEnumerable<ISearchStrategy> strategies)
        => _strategies = strategies.OrderBy(s => s.Priority);

    public async Task<IEnumerable<SearchResultDto>> DispatchAsync(string term, CancellationToken ct)
    {
        var tasks = _strategies.Select(s => s.SearchAsync(term, ct));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r);
    }
}
```

---

## Relaciones

- [[Strategy Pattern]] — El handler usa el registry que agrupa las estrategias
- [[Repository Pattern]] — Cada strategy usa su repositorio
- [[Clean Architecture]] — CQRS es una extensión natural de CA
- [[Caso 02 - Buscador Universal]] — Caso completo implementado
- [[Caso 05 - CQRS Gestión Empleados]] — Caso real de CQRS en HR

---

## Fuentes
- [CQRS with MediatR in ASP.NET Core — codewithmukesh](https://codewithmukesh.com/blog/cqrs-and-mediatr-in-aspnet-core/)
- [CQRS Pattern with MediatR — Milan Jovanovic](https://www.milanjovanovic.tech/blog/cqrs-pattern-with-mediatr)
