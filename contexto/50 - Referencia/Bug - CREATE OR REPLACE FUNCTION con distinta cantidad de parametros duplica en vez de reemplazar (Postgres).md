---
title: "Bug — CREATE OR REPLACE FUNCTION con distinta cantidad de parámetros duplica en vez de reemplazar (Postgres)"
tags:
  - bug
  - postgres
  - supabase
  - referencia
  - trampa
date: 2026-08-14
---

# Bug — `CREATE OR REPLACE FUNCTION` con distinta cantidad de parámetros crea una sobrecarga nueva, no reemplaza

## El patrón incorrecto

```sql
-- Función original: 4 parámetros
CREATE OR REPLACE FUNCTION public.contar_productos(
    p_estado integer DEFAULT NULL,
    p_fab    integer DEFAULT NULL,
    p_pais   integer DEFAULT NULL,
    p_prov   integer DEFAULT NULL
) RETURNS TABLE(total bigint, activos bigint, inactivos bigint)
LANGUAGE sql STABLE AS $$ ... $$;

-- ❌ Migración que "agrega un filtro más" con el mismo CREATE OR REPLACE
CREATE OR REPLACE FUNCTION public.contar_productos(
    p_estado integer DEFAULT NULL,
    p_fab    integer DEFAULT NULL,
    p_pais   integer DEFAULT NULL,
    p_prov   integer DEFAULT NULL,
    p_cat    integer DEFAULT NULL   -- ← parámetro nuevo
) RETURNS TABLE(total bigint, activos bigint, inactivos bigint)
LANGUAGE sql STABLE AS $$ ... $$;
```

Compila sin error. La migración se aplica sin error. Y sin embargo rompe la app en producción.

---

## Por qué falla — la causa raíz

En Postgres, **la firma de una función incluye la cantidad y tipos de sus parámetros** (`pg_proc.proargtypes`), no solo el nombre. `CREATE OR REPLACE FUNCTION` reemplaza una función existente solo si coincide **exactamente** en nombre + tipos de parámetros. Agregar un parámetro nuevo — aunque tenga `DEFAULT NULL` — cambia la firma, así que Postgres no encuentra una función que reemplazar y **crea una segunda función**, dejando la vieja intacta:

```sql
select oid, pg_get_function_identity_arguments(oid)
from pg_proc where proname = 'contar_productos';
```
```
oid   | args
------+--------------------------------------------------------
58362 | p_estado integer, p_fab integer, p_pais integer, p_prov integer
58501 | p_estado integer, p_fab integer, p_pais integer, p_prov integer, p_cat integer
```

Dos sobrecargas vivas. Y como **todos** los parámetros de las dos tienen `DEFAULT NULL`, cualquier llamada con un subconjunto de argumentos con nombre encaja en ambas — PostgREST/el driver no puede elegir:

```sql
select * from contar_productos(p_estado := 1);
```
```
ERROR: 42725: function contar_productos(p_estado => integer) is not unique
HINT: Could not choose a best candidate function. You might need to add explicit type casts.
```

Esto rompe **cualquier** llamada a la RPC que no pase los 5 argumentos exactos — que es exactamente cómo la llama el código C#, armando el diccionario solo con los filtros activos (`CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`, método `GetConteosRpcAsync`).

### Por qué el síntoma en la app fue "la tabla queda vacía", no un error de filtros

`GetPagedInternal` corre `Task.WhenAll(pageTask, conteosTask)` — la query de filas y la RPC de conteos en paralelo. Cuando la RPC revienta con la ambigüedad de arriba, el `Task.WhenAll` propaga la excepción, `TryAsync` la captura como `Result.Fail`, y `GetPagedAsync` falla **entero** — se pierde también la query de filas que sí había funcionado. El listado completo queda vacío, sin ninguna pista de que el problema real es un filtro de categoría nuevo.

---

## Cómo se detectó

Una prueba SQL directa con `p_cat := 1` (categoría inexistente) devolvió `(0, 0, 0)` y se dio por válida — casualmente esa combinación de argumentos con nombre no chocó con la ambigüedad porque, en el momento de la prueba, no se probó **sin** `p_cat`, que es el caso normal que usa la app en cada carga de página. El reporte real vino del usuario: "ya no carga productos, queda vacía la tabla".

---

## Cómo se corrigió

```sql
-- Elimina la sobrecarga vieja, dejando una sola función viva.
-- Hace falta la firma completa de la vieja para que DROP sepa cuál borrar.
DROP FUNCTION IF EXISTS public.contar_productos(integer, integer, integer, integer);
```

Verificado con `pg_get_function_identity_arguments` (una sola fila) y probando las 4 combinaciones reales de argumentos que usa la app (sin args, solo estado, solo categoría, proveedor+categoría) antes de dar el fix por bueno.

---

## Regla para futuras migraciones

> **Agregar un parámetro a una función existente NUNCA es un `CREATE OR REPLACE` de una sola sentencia si todos los parámetros tienen `DEFAULT`.** Elegir una de estas dos:
>
> 1. `DROP FUNCTION IF EXISTS nombre(tipos_viejos);` seguido de `CREATE OR REPLACE FUNCTION nombre(tipos_nuevos)` — dos sentencias, en el mismo `apply_migration`.
> 2. Si de verdad hace falta mantener las dos firmas coexistiendo (overloading intencional), verificar explícitamente después con `pg_get_function_identity_arguments` que las llamadas reales de la app (con el subconjunto de argumentos con nombre que de verdad usan) sigan siendo únicas — no asumir.

Después de **cualquier** migración de una función con parámetros opcionales, correr:

```sql
select pg_get_function_identity_arguments(oid)
from pg_proc where proname = 'nombre_de_la_funcion';
```

Si devuelve más de una fila, hay una sobrecarga sin querer.

---

## Relaciones

- [[Módulo Productos]] — `contar_productos`, usada en `GetConteosRpcAsync`
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] — otra migración de la misma sesión (2026-08-14), tabla distinta
- [[Bug - Filter OR con Op.Equals en postgrest-csharp]] — otro bug de Supabase/Postgres que "compila y falla en silencio (o con excepción tardía)"
