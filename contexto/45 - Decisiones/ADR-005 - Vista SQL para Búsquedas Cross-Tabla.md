---
title: ADR-005 — Vista SQL para Búsquedas Cross-Tabla
type: adr
status: vigente
tags:
  - adr
  - supabase
  - postgrest
  - busqueda
date: 2026-07-26
updated: 2026-07-26
summary: "P-021: el buscador de Usuarios solo filtraba por aliasusuario. El nombre del empleado vive en la tabla empleados (JOIN por idempleado). PostgREST no permite un…"
scope: []
symbols:
  - Usuarios
estado: aceptado
---

# ADR-005 — Vista SQL para Búsquedas Cross-Tabla

## Contexto

P-021: el buscador de Usuarios solo filtraba por `alias_usuario`. El nombre del empleado vive en la tabla `empleados` (JOIN por `id_empleado`). **PostgREST no permite un OR que mezcle columnas de la tabla padre con columnas de una tabla embebida** — el patrón `.Or()` de los otros 5 módulos ([[Bug - Filter OR con Op.Equals en postgrest-csharp]]) solo funciona entre columnas de la misma tabla.

Es la **primera búsqueda cross-tabla del sistema**; el mismo problema aparecerá en Movimientos (buscar por producto/proveedor) y Empleados.

## Decisión

Crear una **vista SQL** que aplane la(s) columna(s) joineada(s) como columnas propias, y apuntar el modelo C# de lectura a la vista:

```sql
create or replace view public.vista_usuarios_busqueda
with (security_invoker = true) as
select
  u.*,
  trim(coalesce(e.nombre_empleado, '') || ' ' || coalesce(e.apellido_empleado, '')) as nombre_completo
from public.usuarios u
left join public.empleados e using (id_empleado);
```

Con la columna aplanada, el OR vuelve a ser el patrón trivial de siempre:

```csharp
query.Or(new List<IPostgrestQueryFilter>
{
    new QueryFilter("alias_usuario",   Op.ILike, $"%{busqueda}%"),
    new QueryFilter("nombre_completo", Op.ILike, $"%{busqueda}%"),
});
```

**Regla no negociable:** toda vista expuesta lleva `with (security_invoker = true)` — sin eso la vista corre con permisos del creador (`postgres`) y **se salta las políticas RLS** de las tablas base. Ver [[Supabase - Vistas SQL, RLS y security_invoker]].

## Alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| Doble query + merge en C# | Rompe paginación y conteos server-side; más código, más lento |
| RPC (función Postgres) | Válida, pero pierde el pipeline `.Filter()/.Range()/.Order()` de postgrest-csharp; más difícil de mantener para CRUD paginado |
| `empleados!inner` con filtro embebido | Solo filtra por la tabla embebida, no permite el OR contra columnas del padre |

## Consecuencias

- ✅ Los repositorios de lectura no cambian de forma — solo el `[Table]` del modelo y una columna más.
- ✅ RLS intacto (`security_invoker`), verificado con grants + Postgres 17.
- ⚠️ Las **escrituras siguen yendo a la tabla real** (`usuarios` vía modelo `Usuarios`); la vista es solo lectura por diseño.
- ⚠️ Si se agregan columnas a `usuarios`, la vista `u.*` las hereda automáticamente; si se renombra una columna del JOIN, hay que migrar la vista.
- Aplicada como migración `crear_vista_usuarios_busqueda` (2026-07-26).

## Relaciones

- [[Bug - Filter OR con Op.Equals en postgrest-csharp]]
- [[Supabase - Vistas SQL, RLS y security_invoker]]
- [[Paginación y Búsqueda - Arquitectura Detallada]]
- [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]]
