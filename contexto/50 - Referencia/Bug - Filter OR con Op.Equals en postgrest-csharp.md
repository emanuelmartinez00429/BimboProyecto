---
title: "Bug — Filter OR con Op.Equals en postgrest-csharp"
tags:
  - bug
  - supabase
  - postgrest
  - referencia
  - trampa
date: 2026-05-28
---

# Bug — `Filter("or", Op.Equals, "...")` en postgrest-csharp

> [!danger] Trampa silenciosa
> Este patrón compila sin errores, se ejecuta sin excepciones, pero **siempre retorna 0 resultados**. No hay advertencia ni log de error. Es uno de los bugs más difíciles de diagnosticar porque no falla — simplemente no funciona.

---

## El patrón incorrecto

```csharp
// ❌ MAL — compila pero retorna 0 resultados siempre
query.Filter("or", Op.Equals,
    "(nombre_producto.ilike.%termino%,codigo_producto.ilike.%termino%)")

// ❌ TAMBIÉN MAL — mismo problema, wildcard diferente no cambia nada
query.Filter("or", Op.Equals,
    "(nombre_producto.ilike.*termino*,codigo_producto.ilike.*termino%)")
```

---

## Por qué falla — la causa raíz

### Cómo serializa `Filter(column, operator, value)`

El método `Filter` del cliente postgrest-csharp construye un query parameter de la forma:

```
?{column}={operatorString}.{value}
```

Cuando `operator = Op.Equals`, el `operatorString` es `"eq"`. Por tanto:

```csharp
.Filter("or", Op.Equals, "(col.ilike.%t%)")
// genera en URL:
?or=eq.(col.ilike.%t%)
```

### Qué espera PostgREST

PostgREST espera el formato:

```
?or=(col1.ilike.*term*,col2.ilike.*term*)
```

Sin ningún operador antes del paréntesis. La presencia de `eq.` hace que PostgREST no reconozca el parámetro `or` y lo ignore completamente — retornando todos los registros... o ninguno, dependiendo de los demás filtros activos.

En este caso, como el `or` mal formado se ignora, la query ejecuta **sin ningún filtro de texto** sobre la tabla completa (potencialmente miles de registros) pero el `.Limit(10)` retorna los primeros 10 — que no coinciden con el término buscado.

> [!tip] Por qué no hay excepción
> PostgREST simplemente ignora query parameters que no reconoce. El HTTP 200 llega normalmente. El cliente C# mapea los modelos sin error. El resultado es silenciosamente incorrecto.

---

## La causa secundaria — el wildcard `%` en URLs

Incluso si se usara un operador correcto, el `%` dentro del valor del parámetro `or` causa problemas adicionales:

- `%` es el carácter de escape en URLs (`%20` = espacio, `%0A` = newline, etc.)
- `%t%` en una URL podría interpretarse como `%t` (secuencia de escape inválida) + `%`
- La librería no URL-encodes automáticamente el `%` dentro de strings pasados a `Filter("or", ...)`

La solución (`.Or()` con `QueryFilter`) evita este problema también porque la librería construye la URL internamente y hace el encoding correcto.

---

## El patrón correcto — `.Or()` con `QueryFilter`

```csharp
// ✅ CORRECTO
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op = Supabase.Postgrest.Constants.Operator;

query.Or(new List<IPostgrestQueryFilter>
{
    new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
    new QueryFilter("codigo_producto",  Op.ILike, $"%{termino}%"),
})
```

### URL generada

```
?or=(nombre_producto.ilike.*termino*,codigo_producto.ilike.*termino*)
```

El `%` del código C# se convierte a `*` internamente por la librería. PostgREST recibe el formato correcto.

---

## Wildcard: `%` vs `*` en el código C#

| Wildcard en código C# | URL generada | ¿Funciona? |
|---|---|---|
| `%{termino}%` | `*termino*` | ✅ Sí — la librería convierte `%` → `*` |
| `*{termino}*` | `*termino*` | ✅ Sí — se pasa directo |

**Recomendación:** usar `%` en el código C# porque es el estilo SQL estándar. Es más legible para cualquier desarrollador con experiencia en SQL.

---

## Implicaciones para el proyecto

### Documentación previa incorrecta

El archivo `Supabase .NET.md` tenía documentado el patrón incorrecto como si fuera válido:

```csharp
// Lo que decía Supabase .NET.md (INCORRECTO)
query = query.Filter("or", Operator.Equals,
    "(nombre_producto.ilike.%t%,codigo_producto.ilike.%t%,contenido.ilike.%t%)");
```

Este ejemplo **nunca funcionó**. Si se copia en un nuevo módulo, el buscador parecerá funcionar (no lanza errores) pero en realidad no filtrará nada.

### Señales de alerta para detectarlo

Si un buscador basado en Supabase:
- Compila sin errores ✅
- No lanza excepciones ✅  
- Retorna siempre 0 resultados ❌
- O retorna resultados que no coinciden con el término ❌

Revisar inmediatamente si se usa `Filter("or", Op.Equals, "...")`.

---

## Regla para nuevos módulos

> **Nunca usar `Filter("or", Op.Xxx, string)` para filtros OR multi-columna.**
> Siempre usar `.Or(new List<IPostgrestQueryFilter> { ... })`.

```csharp
// Plantilla para cualquier módulo con búsqueda multi-columna:
query.Or(new List<IPostgrestQueryFilter>
{
    new QueryFilter("columna_1", Op.ILike, $"%{termino}%"),
    new QueryFilter("columna_2", Op.ILike, $"%{termino}%"),
    // agregar más columnas sin cambiar el contrato ni el ViewModel
})
```

---

## Relaciones

- [[Supabase .NET.md]] — referencia del SDK (corregida con el patrón correcto)
- [[Módulo Productos]] — primer módulo donde se detectó y corrigió
- [[Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos]] — sesión donde se investigó y resolvió
- [[Paginación y Búsqueda - Arquitectura Detallada]] — arquitectura de búsqueda actualizada
