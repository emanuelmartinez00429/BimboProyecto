---
title: SOLID
tags:
  - arquitectura
  - principios
  - dotnet
---

# Principios SOLID

> [!abstract]
> Los 5 principios de diseño orientado a objetos que hacen posible la [[Clean Architecture]].

---

## S — Single Responsibility (SRP)

**Una clase = una razón para cambiar.**

```csharp
// ❌ Mal: ProductosViewModel hace paginación Y guarda en BD
// ✅ Bien: ProductoCrudRepository solo accede a datos
//          ProductosViewModel solo orquesta la UI
```

En Bimbo:
- `ProductoSearchRepository` → solo búsqueda para buscador
- `ProductoCrudRepository` → solo CRUD para formulario
- `ProductosViewModel` → solo estado de UI

---

## O — Open/Closed (OCP)

**Abierto para extensión, cerrado para modificación.**

```csharp
// Agregar EmpleadoSearchStrategy NO modifica ProductoSearchStrategy
// Solo registras la nueva estrategia en DI
services.AddScoped<ISearchStrategy, EmpleadoSearchStrategy>();
```

Ver: [[Strategy Pattern]], [[Caso 02 - Buscador Universal]]

---

## L — Liskov Substitution (LSP)

**Los subtipos deben ser intercambiables con sus tipos base.**

```csharp
// IRepository<T>.SearchAsync() debe comportarse igual
// sin importar si es ProductoSearchRepository o EmpleadoRepository
IRepository<Producto> repo = new ProductoSearchRepository();
```

---

## I — Interface Segregation (ISP)

**Interfaces pequeñas y específicas, no interfaces gordas.**

```csharp
// ❌ Mal: IRepository<T> con 10 métodos que no todos usan
// ✅ Bien:
IRepository<T>        → SearchAsync(term)         // buscador universal
IProductoRepository   → GetPagedAsync, BuscarSugerenciasAsync, GetFabricantesAsync...
```

---

## D — Dependency Inversion (DIP)

**Depende de abstracciones, no de implementaciones.**

```csharp
// ✅ ViewModel depende de IProductoRepository (abstracción)
public ProductosViewModel(IProductoRepository repo) { _repo = repo; }

// ✅ CapaDatos implementa la abstracción definida en CapaAplicacion
public class ProductoCrudRepository : IProductoRepository { ... }
```

> [!tip] La clave de Clean Architecture
> DIP es el principio que permite que las capas internas no dependan de las externas. La dirección de dependencia va contra la dirección del flujo de control.

---

## Relaciones
- [[Clean Architecture]] — SOLID es la base teórica
- [[Repository Pattern]] — OCP + DIP en acción
- [[Strategy Pattern]] — OCP en acción (agregar estrategias sin modificar el sistema)
