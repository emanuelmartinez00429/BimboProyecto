---
title: Repository Pattern
tags:
  - patron
  - datos
  - dotnet
aliases:
  - Patrón Repositorio
---

# Repository Pattern

> [!abstract] Definición
> Abstrae el acceso a datos detrás de una interfaz. El resto de la app no sabe si los datos vienen de Supabase, SQL Server, un archivo o un mock de tests.

---

## Dos variantes en Bimbo

> [!important] Decisión de diseño
> Se separaron en dos repositorios distintos porque tienen contratos diferentes y sirven a capas diferentes.

### Variante 1 — Search (Buscador Universal)

```
IRepository<T>  →  CapaDominio  (contrato genérico)
    ↑
ProductoSearchRepository  →  CapaDatos  (implementación)
```

```csharp
// Contrato genérico — Domain Layer
public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> SearchAsync(string term, CancellationToken ct = default);
}

// Implementación — Infrastructure Layer
public class ProductoSearchRepository : SupabaseRepository<Producto, Productos>
{
    public override async Task<IEnumerable<Producto>> SearchAsync(string term, ct)
    {
        // Filtro server-side con ILike en Supabase
        .Filter("or", Operator.Equals, "(nombre_producto.ilike.%term%,...)")
    }
}
```

Retorna: **entidad de dominio** `Producto` (sin FKs, sin atributos de BD)

---

### Variante 2 — Crud (Formulario de Productos)

```
IProductoRepository  →  CapaAplicacion  (contrato específico)
    ↑
ProductoCrudRepository  →  CapaDatos  (implementación)
```

```csharp
// Contrato específico — Application Layer
public interface IProductoRepository
{
    Task<PagedResult<ProductoDto>>   GetPagedAsync(int page, int size, ProductoFiltros filtros, ct);
    Task<IReadOnlyList<ProductoDto>> BuscarSugerenciasAsync(string termino, ProductoFiltros filtros, ct);
    Task<IReadOnlyList<FiltroItem>>  GetFabricantesAsync(ct);
    Task<IReadOnlyList<FiltroItem>>  GetPaisesAsync(ct);
}
```

Retorna: **DTO** `ProductoDto` (con FKs, para UI y formularios)

---

## Clase base abstracta

```csharp
// SupabaseRepository<TDomain, TSupabase>
// Provee: GetClientAsync(), MapToDomain() abstracto, SelectStatement
public abstract class SupabaseRepository<TDomain, TSupabase> : IRepository<TDomain>
    where TSupabase : BaseModel, new()
{
    protected abstract TDomain MapToDomain(TSupabase model);
    public abstract Task<IEnumerable<TDomain>> SearchAsync(string term, ct);
    protected async Task<Supabase.Client> GetClientAsync() => ...;
}
```

---

## Beneficios concretos

| Beneficio | Ejemplo |
|---|---|
| **Testeable** | Mockear `IProductoRepository` en tests del ViewModel |
| **Intercambiable** | Cambiar Supabase por EF Core: solo cambias la implementación |
| **DRY** | `SupabaseRepository` centraliza el boilerplate de conexión |
| **Seguro** | La UI nunca toca la BD directamente |

---

## Anti-patrones a evitar

> [!bug] No hagas esto
> ```csharp
> // ❌ Repositorio genérico con Expression<Func<T, bool>>
> Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
> // Problema: no se puede traducir al REST API de Supabase → filtra en memoria
> ```

> [!success] Haz esto
> ```csharp
> // ✅ Método específico que se traduce a filtro server-side
> Task<IEnumerable<T>> SearchAsync(string term, ct);
> // O mejor aún — método con parámetros explícitos:
> Task<PagedResult<ProductoDto>> GetPagedAsync(int page, int size, ProductoFiltros filtros, ct);
> ```

---

## Relaciones

- [[Clean Architecture]] — El repositorio es el puente entre Application e Infrastructure
- [[SOLID]] — DIP: ViewModel depende de IProductoRepository, no de ProductoCrudRepository
- [[Strategy Pattern]] — Cada estrategia de búsqueda inyecta su IRepository<T>
- [[Caso 01 - CRUD con Paginación]] — Caso real con paginación
- [[Caso 03 - ERP Inventario]] — Caso real empresarial
