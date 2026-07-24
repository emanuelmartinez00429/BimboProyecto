---
title: Buscador Universal — Bimbo
tags:
  - bimbo
  - buscador
  - strategy
  - mediator
---

# Buscador Universal — Bimbo

> [!abstract]
> Búsqueda global que encuentra productos, empleados y clientes desde una sola caja de texto. Implementa [[Strategy Pattern]] + [[CQRS + Mediator]].

---

## Archivos

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
    UniversalSearchHandler.cs
  Dtos/
    SearchResultDto.cs

CapaDatos/Repositories/Search/
  SupabaseRepository.cs         → base abstracta
  ProductoSearchRepository.cs   → IRepository<Producto>
  EmpleadoRepository.cs         → IRepository<Empleado>
  ClienteRepository.cs          → stub (tabla no existe aún)
```

---

## Flujo completo

```
SearchBox.TextChanged
    ↓ mediator.Send(new UniversalSearchQuery(term))
UniversalSearchHandler
    ↓ _registry.SearchAllAsync(term, ct)
SearchStrategyRegistry
    ↓ Task.WhenAll(strategies.Select(s => s.SearchAsync(term, ct)))
[ProductoSearchStrategy, EmpleadoSearchStrategy, ClienteSearchStrategy]
    ↓ cada una: _repo.SearchAsync(term, ct)
[ProductoSearchRepository, EmpleadoRepository, ClienteRepository]
    ↓ Filter server-side ILike en Supabase
Results ordenados por Priority → UI
```

---

## Extender con nueva entidad

```csharp
// 1. Crear el repositorio
public class ProveedorRepository : SupabaseRepository<Proveedor, Proveedores>
{
    public override async Task<IEnumerable<Proveedor>> SearchAsync(string term, ct)
    {
        return await client.From<Proveedores>()
            .Filter("nombre_proveedor", Op.ILike, $"%{term}%")
            .Limit(10).Get()
            .Select(MapToDomain);
    }
}

// 2. Crear la estrategia
public class ProveedorSearchStrategy : ISearchStrategy
{
    public string EntityType => "Proveedor";
    public int    Priority   => 4;
    public async Task<IEnumerable<SearchResultDto>> SearchAsync(string term, ct)
    { ... }
}

// 3. Registrar en DI — cero cambios en código existente
services.AddScoped<ISearchStrategy, ProveedorSearchStrategy>();
services.AddScoped<IRepository<Proveedor>, ProveedorRepository>();
```

---

## Relaciones

- [[Strategy Pattern]] — arquitectura del buscador
- [[CQRS + Mediator]] — desacoplamiento con MediatR
- [[Repository Pattern]] — `IRepository<T>` con `SearchAsync`
- [[Caso 02 - Buscador Universal]] — análisis completo del patrón
- [[Caso 05 - CQRS Gestión Empleados]] — CQRS aplicado a empleados
