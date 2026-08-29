---
title: Sesión 2026-05-28 — Fix Búsqueda Multi-Campo en Módulo Productos
type: sesion
status: vigente
tags:
  - sesion
  - busqueda
  - supabase
  - bug
  - productos
date: 2026-05-28
updated: 2026-05-28
summary: El buscador de sugerencias en Productos ahora encuentra por nombre y por código interno. El placeholder de la UI ya lo prometía — ahora el código lo cumple.
scope:
  - CapaDatos/Repositories/Productos
symbols:
  - BuscarSugerenciasInternal
  - CodigoInterno
  - IProductoRepository
  - ProductoDto
  - ProductosView
  - ProductosViewModel
  - QueryFilter
---

# Sesión 2026-05-28 — Fix Búsqueda Multi-Campo en Módulo Productos

> [!success] Resultado
> El buscador de sugerencias en Productos ahora encuentra por **nombre** y por **código interno**. El placeholder de la UI ya lo prometía — ahora el código lo cumple.

---

## Contexto

El formulario de Productos tiene un buscador con sugerencias (debounce 300ms, popup, highlight de teclado). La UI mostraba el código de cada producto en las sugerencias y el placeholder decía _"Buscar producto por código o nombre…"_, pero `BuscarSugerenciasInternal` solo filtraba por `nombre_producto`.

---

## Diagnóstico previo al fix

### ¿Qué tenía el `ProductoDto`?

El campo `CodigoInterno` existía y se mapeaba correctamente:
```csharp
CodigoInterno = p.codigoProducto ?? string.Empty,  // columna: codigo_producto
```

La UI incluso lo mostraba en azul en el template de sugerencias. El problema era solo en la query al repositorio.

### ¿Qué hacía `BuscarSugerenciasInternal` antes?

```csharp
var resultado = await query
    .Filter("nombre_producto", Op.ILike, $"%{termino}%")
    .Order("nombre_producto", Ord.Ascending)
    .Limit(10)
    .Get();
```

Solo un filtro, solo una columna.

---

## El bug del camino incorrecto — `Filter("or", Op.Equals, "...")`

Al intentar agregar búsqueda multi-columna, se usó el patrón documentado en `Supabase .NET.md` (que estaba **incorrecto**):

```csharp
// INTENTO 1 — con % — no retornaba nada
.Filter("or", Op.Equals, $"(nombre_producto.ilike.%{termino}%,codigo_producto.ilike.%{termino}%)")

// INTENTO 2 — con * — tampoco retornaba nada
.Filter("or", Op.Equals, $"(nombre_producto.ilike.*{termino}*,codigo_producto.ilike.*{termino}*)")
```

**Ambos fallan por la misma razón de raíz.** Ver la nota técnica completa: [[Bug - Filter OR con Op.Equals en postgrest-csharp]].

### Resumen de por qué fallaban

`Op.Equals` serializa como `"eq"` en la URL. El cliente genera:
```
?or=eq.(nombre_producto.ilike.*term*)
```
PostgREST **no reconoce** ese formato y retorna 0 resultados — sin lanzar excepción, sin error en consola.

El wildcard (`%` vs `*`) era irrelevante — el problema era el operador `eq.` inyectado antes del valor.

---

## La solución correcta — `.Or()` con `QueryFilter`

```csharp
var resultado = await query
    .Or(new List<IPostgrestQueryFilter>
    {
        new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
        new QueryFilter("codigo_producto",  Op.ILike, $"%{termino}%"),
    })
    .Order("nombre_producto", Ord.Ascending)
    .Limit(10)
    .Get();
```

Esto genera correctamente:
```
?or=(nombre_producto.ilike.*term*,codigo_producto.ilike.*term*)
```

> [!note] Wildcard correcto
> En el código C# se pasa `%{termino}%` (estilo SQL). La librería convierte `%` → `*` internamente al serializar. Ambos funcionan, pero `%` es más idiomático en C#.

---

## Archivos modificados

### `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`

**Usings añadidos:**
```csharp
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
```

**Método `BuscarSugerenciasInternal` — cambio en el filtro:**
```csharp
// ANTES
.Filter("nombre_producto", Op.ILike, $"%{termino}%")

// DESPUÉS
.Or(new List<IPostgrestQueryFilter>
{
    new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
    new QueryFilter("codigo_producto",  Op.ILike, $"%{termino}%"),
})
```

**Sin cambios en:** contrato `IProductoRepository`, `ProductosViewModel`, `ProductosView`, `ProductoDto`. La arquitectura ya estaba preparada.

---

## Documentación corregida en la bóveda

- [[Supabase .NET.md]] — el ejemplo de OR multi-columna estaba documentado con el patrón incorrecto (`Filter("or", Op.Equals, "...")`). Corregido.
- [[Paginación y Búsqueda - Arquitectura Detallada]] — sección "Búsqueda con sugerencias" actualizada para reflejar búsqueda multi-columna.

---

## Relaciones

- [[Bug - Filter OR con Op.Equals en postgrest-csharp]] — nota técnica completa del bug
- [[Módulo Productos]] — módulo afectado
- [[Paginación y Búsqueda - Arquitectura Detallada]] — arquitectura general de búsqueda
- [[Supabase .NET.md]] — referencia del SDK corregida
