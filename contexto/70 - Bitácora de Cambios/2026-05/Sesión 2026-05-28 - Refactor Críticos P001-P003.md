---
title: "Sesión 2026-05-28 — Refactor Críticos P-001, P-002, P-003"
type: sesion
status: vigente
tags:
  - sesion
  - refactor
  - deuda-tecnica
  - productos
  - repositorio
date: 2026-05-28
updated: 2026-05-28
summary: Los 3 problemas críticos identificados en la auditoría pre-replicación están resueltos. CapaDatos y CapaUI compilan con 0 errores. El módulo Productos ahora es…
scope:
  - CapaAplicacion4/Common
  - CapaDatos/Repositories/Productos
symbols:
  - Activo
  - AplicarFiltros
  - CargarPaginaAsync
  - CargarPaginaSilenciosamenteAsync
  - GetConteosAsync
  - IPostgrestTable<T>
  - Inactivo
  - ProductoCrudRepository
  - ProductosViewModel
  - RefrescarConteosAsync
---

# Sesión 2026-05-28 — Refactor Críticos P-001, P-002, P-003

> [!success] Resultado
> Los 3 problemas críticos identificados en la auditoría pre-replicación están resueltos.
> `CapaDatos` y `CapaUI` compilan con **0 errores**. El módulo Productos ahora es una plantilla limpia para replicar.

---

## Contexto

Antes de replicar el módulo Productos como base para otros módulos (Empleados, Fabricantes, etc.), se identificaron en auditoría 3 problemas críticos que, de no corregirse, se propagarían en cascada a cada módulo nuevo. Esta sesión los resuelve en orden de dependencia: P-002 → P-001 → P-003.

---

## P-002 — `EstadoRegistro.cs` (nuevo archivo)

**Archivo creado:** `CapaAplicacion4/Common/EstadoRegistro.cs`

```csharp
namespace CapaAplicacion.Common;

public static class EstadoRegistro
{
    public const int Activo   = 1;
    public const int Inactivo = 2;
}
```

Los literales `1` y `2` estaban dispersos en 6 lugares:
- `ProductoCrudRepository.cs` → soft-delete (`.Set(p => p.idEstado, 2)`) y query de activos en `GetConteosAsync` (`.Filter("id_estado", "1")`)
- `ProductosViewModel.cs` → `BuildFiltros()`, `CargarPaginaAsync`, `CargarPaginaSilenciosamenteAsync`, `RefrescarConteosAsync`

Todos reemplazados por `EstadoRegistro.Activo` / `EstadoRegistro.Inactivo`.

En el ViewModel se agregó `using static CapaAplicacion.Common.EstadoRegistro;` para poder usar `Activo` e `Inactivo` directamente sin prefijo.

---

## P-001 — `AplicarFiltros` en `ProductoCrudRepository`

**Archivo modificado:** `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`

### Alias de tipo agregado

```csharp
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Productos.Productos>;
```

> [!note] Tipo correcto
> El tipo que devuelve `.Select()` (y todos los métodos de query encadenables de Supabase) es la interfaz `IPostgrestTable<T>`, **no** la clase concreta `Table<T>`. Intentar usar la clase concreta produce CS1503.

### Método centralizado

```csharp
private static Table AplicarFiltros(Table query, ProductoFiltros filtros)
{
    if (filtros.IdEstado.HasValue)
        query = query.Filter("id_estado",     Op.Equals, filtros.IdEstado.Value.ToString());
    if (filtros.IdFabricante.HasValue)
        query = query.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
    if (filtros.IdPais.HasValue)
        query = query.Filter("id_pais",       Op.Equals, filtros.IdPais.Value.ToString());
    return query;
}
```

### Los 3 métodos que lo usan

**Antes (copiado 3 veces):**
```csharp
if (filtros.IdEstado.HasValue)
    query = query.Filter("id_estado",     Op.Equals, filtros.IdEstado.Value.ToString());
if (filtros.IdFabricante.HasValue)
    query = query.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
if (filtros.IdPais.HasValue)
    query = query.Filter("id_pais",       Op.Equals, filtros.IdPais.Value.ToString());
```

**Después (una línea en cada método):**
```csharp
// GetPagedInternal
var query = AplicarFiltros(client.From<Modelados.Productos.Productos>().Select(Select), filtros);

// BuscarSugerenciasInternal
var query = AplicarFiltros(client.From<Modelados.Productos.Productos>().Select(Select), filtros);

// GetPaginaDeProductoInternal
var query = AplicarFiltros(
    client.From<Modelados.Productos.Productos>()
          .Select("id_producto")
          .Filter("id_producto", Op.LessThan, idProducto.ToString()),
    filtros);
```

---

## P-003 — `ResolverFilteredCount` en `ProductosViewModel`

**Archivo modificado:** `CapaUI/.../Pantallas/Productos/ProductosViewModel.cs`

El switch para calcular cuántos registros aplican al filtro activo estaba copiado en `CargarPaginaAsync`, `CargarPaginaSilenciosamenteAsync` y `RefrescarConteosAsync`.

### Método centralizado

```csharp
private static int ResolverFilteredCount(PagedResult<ProductoDto> pagina, ProductoFiltros filtros) =>
    filtros.IdEstado switch
    {
        Activo   => pagina.Activos,
        Inactivo => pagina.Inactivos,
        _        => pagina.Total
    };
```

Al usar `Activo` e `Inactivo` (de `using static EstadoRegistro`) el switch es legible sin saber qué significa el `1` o el `2`.

Los 3 métodos reemplazaron su switch por:
```csharp
_filteredCount = ResolverFilteredCount(pagina, filtros);
```

---

## Resultado de build

| Proyecto | Errores | Estado |
|---|---|---|
| `CapaAplicacion` | 0 | ✅ |
| `CapaDatos` | 0 | ✅ |
| `CapaUI` | 0 | ✅ |
| `BimboPesaje` | 3 (preexistentes — `ServicioPerfilUsuario`) | ⚠️ No tocar |

---

## Lección técnica — tipo real de la query de Supabase

Al intentar usar `Supabase.Postgrest.Table<T>` como tipo del parámetro de `AplicarFiltros`, el compilador lanzó CS1503 porque `.Select()` y `.Filter()` devuelven `IPostgrestTable<T>` (interfaz), no la clase concreta. El using alias debe apuntar a la interfaz:

```csharp
// Incorrecto
using Table = Supabase.Postgrest.Table<T>;

// Correcto
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<T>;
```

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-001, P-002, P-003 marcados como resueltos
- [[Módulo Productos]] — módulo refactorizado
- [[Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos]] — sesión donde se identificaron estos problemas
