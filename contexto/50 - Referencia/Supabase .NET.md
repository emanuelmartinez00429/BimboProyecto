---
title: Supabase .NET SDK
tags:
  - referencia
  - supabase
  - datos
aliases:
  - Supabase
  - postgrest-csharp
---

# Supabase .NET SDK

> [!info] Versión usada: 1.1.1

---

## Conexión

```csharp
// Singleton lazy — siempre await
var client = await ConexionSupabase.GetClientAsync();
```

---

## Select con joins

```csharp
client.From<Productos>()
    .Select("*, presentacion_producto(*), fabricante(*), categoria(*), paises(*)")
    .Get();
```

---

## Filtros server-side

```csharp
using Op = Supabase.Postgrest.Constants.Operator;

// Igualdad
query = query.Filter("id_estado", Op.Equals, "1");

// ILike (case-insensitive, columna única)
query = query.Filter("nombre_producto", Op.ILike, $"%{termino}%");
```

---

## OR multi-columna (ILike)

> [!danger] Trampa conocida
> `Filter("or", Op.Equals, "...")` genera `?or=eq.(...)` — PostgREST lo ignora y retorna 0 resultados sin lanzar excepción. Ver [[Bug - Filter OR con Op.Equals en postgrest-csharp]].

```csharp
// ✅ CORRECTO — usar .Or() con QueryFilter
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;

query = query.Or(new List<IPostgrestQueryFilter>
{
    new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
    new QueryFilter("codigo_producto",  Op.ILike, $"%{termino}%"),
});
// URL generada: ?or=(nombre_producto.ilike.*term*,codigo_producto.ilike.*term*)
```

El `%` en C# se convierte a `*` internamente por la librería. Usar `%` es más idiomático (estilo SQL).

---

## Paginación

```csharp
using Ord = Supabase.Postgrest.Constants.Ordering;

int from = (page - 1) * size;
int to   = from + size - 1;

var resultado = await query
    .Order("id_producto", Ord.Ascending)
    .Range(from, to)
    .Get();
```

---

## Limit

```csharp
query = query.Limit(10).Get();
```

---

## Insert

```csharp
await client.From<ProductosInsertar>().Insert(datos);
```

---

## Update

```csharp
await client.From<ProductosInsertar>()
    .Where(p => p.idProducto == id)
    .Set(p => p.nombreProducto!, nuevoNombre)
    .Update();
```

---

## Modelos Supabase

```csharp
[Table("productos")]
public class Productos : BaseModel
{
    [PrimaryKey("id_producto")]   public int    idProducto      { get; set; }
    [Column("nombre_producto")]   public string nombreProducto  { get; set; }
    [Column("id_fabricante")]     public int    idFabricante    { get; set; }
    // Navegación (no son columnas — vienen del join)
    public Fabricante? Fabricante { get; set; }
}
```

> [!bug] Fabricante no hereda BaseModel
> Nunca `client.From<Fabricante>()`. Solo se accede vía join.

---

## Relaciones

- [[Repository Pattern]] — Los repositorios abstraen estas llamadas
- [[Módulo Productos]] — Uso real de paginación y filtros
- [[Bug - Filter OR con Op.Equals en postgrest-csharp]] — Trampa silenciosa del OR filter
