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

> [!danger] Corregido 2026-07-28 — este patrón SÍ puede tirar excepción
> Esta nota decía que el patrón "se ejecuta sin excepciones, pero siempre retorna 0 resultados". **Es incorrecto** para la versión de PostgREST de este proyecto: reproducido en el buscador global (`ProductoSearchRepository.SearchAsync`), el mismo `Filter("or", Op.Equals, "(...)")` lanzó una `PostgrestException` real:
>
> ```
> Supabase.Postgrest.Exceptions.PostgrestException: '{"code":"PGRST100",
> "details":"unexpected \"e\" expecting \"(\"","hint":null,
> "message":"\"failed to parse logic tree (eq.(nombre_producto.ilike.%dede%,
> codigo_producto.ilike.%dede%,contenido.ilike.%dede%))\" (line 1, column 3)"}'
> ```
>
> PostgREST **sí** valida la gramática de `or` y la rechaza con `PGRST100` cuando no arranca con `(` — no la ignora en silencio como se afirmaba antes. Ver sección "Actualización 2026-07-28" más abajo. El patrón sigue siendo incorrecto y hay que corregirlo igual — solo cambia cuál es el síntoma que vas a ver.

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

> [!tip] Por qué a veces no hay excepción (y a veces sí)
> Esta sección describía la teoría original: PostgREST ignoraría el query parameter no reconocido, devolvería HTTP 200 y el resultado sería silenciosamente incorrecto. **Reproducido en la práctica el 2026-07-28, no fue lo que pasó** — ver "Actualización 2026-07-28" abajo: PostgREST parseó `or` como logic tree, no lo reconoció como bien formado, y devolvió un error `PGRST100` real que el SDK convirtió en `PostgrestException`. Puede que el comportamiento "ignora y sigue" aplique a otras versiones/configuraciones de PostgREST, pero no asumas que es lo único que puede pasar: probá siempre con un término real antes de dar un buscador por andando.

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

---

## Actualización 2026-07-28 — el bug seguía vivo en el buscador global, y sí lanza excepción

Esta nota decía en sus relaciones que el bug "se detectó y corrigió" en el Módulo Productos. Eso es cierto **solo para el buscador por formulario** (`ProductoCrudRepository`, CRUD con sugerencias). El buscador global/universal (`CapaDatos/Repositories/Search/`) nunca se tocó y tenía el mismo antipatrón en **dos** repositorios:

- `ProductoSearchRepository.cs:33-34`
- `EmpleadoRepository.cs:28-29`

Reportado por el usuario con una captura de Visual Studio: excepción no controlada justo en el `.Get()` de `ProductoSearchRepository.SearchAsync`, buscando el término `dede`. Mensaje completo capturado del debugger:

```
Supabase.Postgrest.Exceptions.PostgrestException: '{"code":"PGRST100",
"details":"unexpected \"e\" expecting \"(\"","hint":null,
"message":"\"failed to parse logic tree (eq.(nombre_producto.ilike.%dede%,
codigo_producto.ilike.%dede%,contenido.ilike.%dede%))\" (line 1, column 3)"}'
```

Esto **contradice directamente** lo que dice el resto de esta nota: PostgREST no ignoró el parámetro `or` mal formado — lo parseó como "logic tree", esperó que arrancara con `(`, encontró `e` (de `eq.`) y lo rechazó con el código `PGRST100`. El SDK (`postgrest-csharp`) convierte esa respuesta de error en una `PostgrestException` que se lanza desde `.Get()`.

**Por qué llegó sin capturar hasta el debugger:** todo el pipeline del buscador global (`UniversalSearchViewModel` → MediatR → `UniversalSearchHandler` → `ISearchStrategy` → repositorio) solo tenía un `catch (OperationCanceledException)` en el ViewModel — ninguna otra excepción se capturaba en ningún punto, y no hay manejador global (`DispatcherUnhandledException`) en `CapaUI/App.xaml.cs`.

**Arreglado:**
1. Los dos repositorios migrados a `.Or(new List<IPostgrestQueryFilter>{...})`, igual que el resto del proyecto.
2. `UniversalSearchHandler` ahora aísla el fallo por estrategia (`Task.WhenAll` ya no tumba las 3 entidades si una falla).
3. `UniversalSearchViewModel` con un `catch (Exception)` genérico como backstop final.

Ver [[Sesión 2026-07-28 - Fix Buscador Global (PostgrestException PGRST100)]].

## Relaciones

- [[Supabase .NET.md]] — referencia del SDK (corregida con el patrón correcto)
- [[Módulo Productos]] — primer módulo donde se detectó y corrigió (buscador por formulario)
- [[Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos]] — sesión donde se investigó y resolvió (buscador por formulario)
- [[Sesión 2026-07-28 - Fix Buscador Global (PostgrestException PGRST100)]] — el mismo bug en el buscador global (Productos y Empleados), con excepción real reproducida
- [[Paginación y Búsqueda - Arquitectura Detallada]] — arquitectura de búsqueda actualizada
- [[Base Repository con TryAsync]] — el buscador global no usa Result Pattern; ítem pendiente relacionado
