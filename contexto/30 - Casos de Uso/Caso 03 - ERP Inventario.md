---
title: Caso 03 — ERP Inventario con Clean Architecture
type: caso
status: vigente
tags:
  - caso-de-uso
  - erp
  - clean-architecture
  - cqrs
date: 2026-05-21
updated: 2026-05-21
summary: Application/ DTOs/ProductDto.cs Queries/GetProductsQuery.cs Handlers/GetProductsHandler.cs
scope: []
symbols:
  - CreateProductCommand
  - GetProductsQuery
  - IProductRepository
  - IProductoRepository
  - ProductDto
---

# Caso 03 — ERP Inventario con Clean Architecture

> [!example] Caso real
> **Proyecto:** Inventory Order Management System  
> **Stack:** .NET 9, Clean Architecture, CQRS, MediatR, Repository Pattern  
> **Fuente:** [GitHub — go2ismail](https://github.com/go2ismail/Asp.Net-Core-Inventory-Order-Management-System)

---

## Por qué es relevante para Bimbo

| Característica | ERP Inventario | Bimbo |
|---|---|---|
| Catálogo de productos | ✅ | ✅ |
| Paginación server-side | ✅ | ✅ |
| Filtros múltiples | ✅ | ✅ Estado / Fabricante / País |
| CQRS + MediatR | ✅ | ✅ (buscador) |
| Repository Pattern | ✅ | ✅ Dos variantes |
| DTOs separados de entidades | ✅ | ✅ ProductoDto |
| DI con ServiceCollection | ✅ | ✅ |

---

## Estructura de capas del ERP (referencia)

```
Domain/
  Entities/Product.cs
  Interfaces/IProductRepository.cs

Application/
  DTOs/ProductDto.cs
  Queries/GetProductsQuery.cs
  Handlers/GetProductsHandler.cs

Infrastructure/
  Repositories/ProductRepository.cs  ← implementa IProductRepository
  Persistence/AppDbContext.cs

API/
  Controllers/ProductsController.cs
```

**Mapping a Bimbo:**
```
CapaDominio  →  Domain
CapaAplicacion4  →  Application
CapaDatos  →  Infrastructure
CapaUI  →  API / Presentation
```

---

## Lección clave del ERP

> [!tip] Lo que adoptamos de este caso
> El ERP separa `GetProductsQuery` (lectura, devuelve `ProductDto`) de `CreateProductCommand` (escritura, recibe solo los campos necesarios). Esto es exactamente lo que implementamos con `IProductoRepository` (lectura) vs `RepositorioProducto.ingresarProducto` (escritura).

---

## Diferencia: ERP usa EF Core, Bimbo usa Supabase

```csharp
// ERP con EF Core
var products = await _context.Products
    .Where(p => p.IsActive)
    .Skip((page - 1) * size)
    .Take(size)
    .ToListAsync();

// Bimbo con Supabase SDK equivalente
var query = client.From<Productos>()
    .Filter("id_estado", Op.Equals, "1")
    .Range(from, to);
```

El patrón es idéntico — solo cambia el ORM.

---

## Relaciones

- [[Clean Architecture]] — Estructura de capas adoptada de este tipo de proyectos
- [[Repository Pattern]] — `IProductRepository` en ERP ≈ `IProductoRepository` en Bimbo
- [[CQRS + Mediator]] — Handlers de lectura/escritura separados
- [[Caso 01 - CRUD con Paginación]] — Detalle de paginación
