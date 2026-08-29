---
title: Clean Architecture
type: arquitectura
status: vigente
tags:
  - arquitectura
  - clean-architecture
  - dotnet
date: 2026-05-21
updated: 2026-05-21
summary: Arquitectura propuesta por Robert C. Martin. Organiza el código en capas concéntricas donde las dependencias solo apuntan hacia adentro. El dominio no sabe nada…
scope:
  - CapaAplicacion/Productos/Interfaces
symbols:
  - IProductoRepository
  - Producto
  - ProductoCrudRepository
  - ProductoDto
  - ProductoSearchRepository
  - Productos
aliases:
  - CA
  - Arquitectura Limpia
---

# Clean Architecture

> [!abstract] Concepto central
> Arquitectura propuesta por Robert C. Martin. Organiza el código en capas concéntricas donde **las dependencias solo apuntan hacia adentro**. El dominio no sabe nada del exterior.

---

## Las capas (de adentro hacia afuera)

```
┌─────────────────────────────────────┐
│           CapaUI / Presentación     │  ← Sabe de Application
│  ┌──────────────────────────────┐   │
│  │     CapaAplicacion            │   │  ← Sabe de Domain
│  │  ┌───────────────────────┐   │   │
│  │  │    CapaDatos / Infra   │   │   │  ← Implementa contratos de Application
│  │  └───────────────────────┘   │   │
│  │  ┌───────────────────────┐   │   │
│  │  │    CapaDominio         │   │   │  ← No sabe nada del exterior
│  │  └───────────────────────┘   │   │
│  └──────────────────────────────┘   │
└─────────────────────────────────────┘
```

| Capa | Responsabilidad | Puede depender de |
|---|---|---|
| Domain | Entidades, reglas de negocio, interfaces genéricas | Nadie |
| Application | DTOs, interfaces específicas, casos de uso | Domain |
| Infrastructure/Data | Implementaciones de repositorios, BD | Domain + Application |
| Presentation/UI | ViewModels, Views | Application + (DI wiring → Infrastructure) |

---

## La regla de dependencia

> [!warning] Regla fundamental
> El código en una capa interna **no puede mencionar** nada de una capa externa. Si Application define `IProductoRepository`, Infrastructure lo implementa — nunca al revés.

En el proyecto Bimbo:
```
CapaDatos implementa IProductoRepository (definida en CapaAplicacion)
CapaAplicacion NO referencia CapaDatos
```

Ver ejemplo concreto en [[Arquitectura Actual]].

---

## Mejores prácticas en C# / .NET 8

### 1. Contratos en Application, no en Infrastructure
```csharp
// ✅ CapaAplicacion/Productos/Interfaces/IProductoRepository.cs
public interface IProductoRepository
{
    Task<PagedResult<ProductoDto>> GetPagedAsync(...);
}

// ✅ CapaDatos implementa ese contrato
public class ProductoCrudRepository : IProductoRepository { ... }
```

### 2. DTOs en Application, entidades en Domain
- **Domain:** `Producto` — entidad limpia, sin FKs expuestas, sin atributos de BD
- **Application:** `ProductoDto` — incluye FKs, propiedades de display, para UI
- **Infrastructure:** `Productos` (modelo Supabase) — solo vive en CapaDatos

### 3. DI como puente entre capas
```csharp
// App.xaml.cs — único lugar donde CapaDatos y CapaAplicacion se encuentran
services.AddScoped<IProductoRepository, ProductoCrudRepository>();
```

### 4. No lógica de negocio en ViewModels
El ViewModel orquesta pero no decide. Las reglas van en Domain o Application.

### 5. Una sola responsabilidad por clase (SRP)
- `ProductoSearchRepository` → solo búsqueda para buscador universal
- `ProductoCrudRepository` → solo CRUD/paginación para formulario

---

## Beneficios concretos

| Beneficio | Cómo se manifiesta |
|---|---|
| **Testeable** | Puedes testear Application sin tocar Supabase |
| **Intercambiable** | Cambiar Supabase por SQL Server: solo cambia Infrastructure |
| **Escalable** | Agregar módulo nuevo no rompe los existentes |
| **Seguro** | La BD nunca se expone directamente a la UI |

---

## Relaciones

- [[SOLID]] — Los principios que hacen esto posible
- [[Repository Pattern]] — Implementación del patrón de acceso a datos
- [[CQRS + Mediator]] — Extensión natural de CA para separar lecturas de escrituras
- [[Arquitectura Actual]] — Cómo CA se aplica en Bimbo hoy
- [[Caso 03 - ERP Inventario]] — Caso real similar
- [[Caso 04 - WPF MVVM Clean Architecture]] — Caso real en desktop

---

## Fuentes
- [Clean Architecture in .NET — Code Maze](https://code-maze.com/dotnet-clean-architecture/)
- [Next-Level Clean Architecture Boilerplate — ISE Dev Blog](https://devblogs.microsoft.com/ise/next-level-clean-architecture-boilerplate/)
- [Clean Architecture in C# — Medium](https://medium.com/@hashirkhanps/clean-architecture-in-c-building-maintainable-scalable-applications-db0f4c2b38f6)
