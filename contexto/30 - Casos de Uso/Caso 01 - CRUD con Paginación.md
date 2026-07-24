---
title: "Caso 01 — CRUD con Paginación Server-Side"
tags:
  - caso-de-uso
  - crud
  - paginacion
  - dotnet
---

# Caso 01 — CRUD con Paginación Server-Side

> [!example] Caso real similar
> **Proyecto:** ASP.NET Core Inventory Management con Clean Architecture, CQRS y paginación server-side  
> **Fuente:** [GitHub — go2ismail/Asp.Net-Core-Inventory-Order-Management-System](https://github.com/go2ismail/Asp.Net-Core-Inventory-Order-Management-System)

---

## El problema que resuelve

Aplicaciones con catálogos grandes (productos, empleados, clientes) no pueden cargar todos los registros en memoria. Se necesita:
- Paginación real en la BD (no en memoria)
- Filtros aplicados server-side
- Conteos independientes del subset actual

---

## Patrón usado en ese proyecto

```
Query Handler (CQRS)
    ↓
IProductRepository.GetPagedAsync(page, size, filters)
    ↓
Specification Pattern → traduce filtros a SQL
    ↓
EF Core / Dapper → ejecuta en BD
    ↓
PagedResult<ProductDto> { Items, Total, Page, Size }
```

---

## Cómo Bimbo lo implementa

```csharp
// ProductoCrudRepository
public async Task<PagedResult<ProductoDto>> GetPagedAsync(
    int page, int size, ProductoFiltros filtros, CancellationToken ct)
{
    var query = client.From<Productos>().Select(Select);

    // Filtros server-side — NO se carga todo
    if (filtros.IdEstado.HasValue)
        query = query.Filter("id_estado", Op.Equals, filtros.IdEstado.Value.ToString());

    // Paginación server-side — solo viajan 50 registros por llamada
    int from = (page - 1) * size;
    query = query.Range(from, from + size - 1);

    var resultado = await query.Order("id_producto", Ord.Ascending).Get();
    var conteos   = await GetConteosAsync(filtros, client);

    return new PagedResult<ProductoDto>
    {
        Items     = resultado.Models.Select(Map).ToList(),
        Total     = conteos.total,
        Activos   = conteos.activos,
        Inactivos = conteos.inactivos,
    };
}
```

---

## Diferencia clave: en memoria vs server-side

| Antes (en memoria) | Ahora (server-side) |
|---|---|
| Descarga 10,000 productos | Descarga 50 productos |
| Filtra con LINQ en RAM | Filtra con SQL en Supabase |
| Lento al abrir | Rápido independientemente del volumen |
| Memory pressure alta | Memory footprint mínimo |

---

## Relaciones

- [[Repository Pattern]] — `IProductoRepository.GetPagedAsync`
- [[CQRS + Mediator]] — En el caso ERP, hay un Query Handler explícito
- [[Clean Architecture]] — PagedResult<T> es DTO de Application, no modelo de BD
- [[Caso 03 - ERP Inventario]] — Caso ERP más completo
- [[Módulo Productos]] — Implementación real en Bimbo
