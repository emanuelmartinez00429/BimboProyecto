---
title: "Supabase — Vistas SQL, RLS y security_invoker"
tags: [referencia, supabase, postgrest, seguridad, rls]
date: 2026-07-26
lifecycle: verified
---

# Supabase — Vistas SQL, RLS y security_invoker

> [!danger] Trampa de seguridad
> Una vista creada en Postgres corre por defecto con los permisos de su **creador** (`security definer`). En Supabase el creador suele ser `postgres` → **la vista se salta las políticas RLS de las tablas base**. Cualquier cliente con la anon key podría leer filas que RLS le prohíbe en la tabla directa.

## La regla (Postgres 15+)

Toda vista expuesta en el schema `public` se crea así:

```sql
create view public.mi_vista
with (security_invoker = true) as
select ...;

-- o para corregir una existente:
alter view public.mi_vista set (security_invoker = true);
```

Con `security_invoker = true` la vista evalúa las políticas RLS de las tablas base **con el rol del que consulta** (`anon`/`authenticated`), igual que si consultara las tablas directo. Verificado en el proyecto Bimbo (Postgres 17).

Fuente: docs oficiales Supabase — *Row Level Security* y *Tables and Data → View security* (consultadas 2026-07-26).

## Postgres < 15

No existe `security_invoker`: proteger revocando acceso a `anon`/`authenticated` sobre la vista, o moverla a un schema no expuesto. (No aplica a Bimbo — estamos en 17.)

## Cómo las expone PostgREST

- Las vistas del schema expuesto aparecen como endpoints REST igual que las tablas → `client.From<ModeloVista>()` funciona con `[Table("nombre_vista")]`.
- Los filtros, `Range`, `Order` y `.Or()` funcionan igual que sobre tablas.
- **Embeds** (`Select("*, roles(*), empleados(*)")`): PostgREST detecta relaciones de la vista a través de las FKs de sus tablas base cuando la vista incluye las columnas FK (`u.*` las incluye). Verificar en runtime al crear cada vista nueva.
- Las vistas son de **solo lectura** en la práctica del proyecto: las escrituras van siempre al modelo de la tabla real.

## Una vista bloquea el `ALTER COLUMN … TYPE` de sus tablas base

Verificado en Bimbo el 2026-09-05 ([[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]]).

Cambiarle el tipo a una columna que **cualquier** vista lee falla, aunque el tipo nuevo sea compatible y ningún dato se trunque:

```
ERROR: 0A000: cannot alter type of a column used by a view or rule
DETAIL: rule _RETURN on view v_mov_productos_resumen depends on column "placa_vehiculo"
```

Pasó al pasar `movimientos.placa_vehiculo` de `varchar` sin longitud a `varchar(20)`. Postgres no analiza si el cambio es seguro: alcanza con que la columna aparezca en el `SELECT` de la vista.

**La salida es soltar la vista y recrearla dentro de la misma migración.** Y ahí está la trampa: `CREATE VIEW` no arrastra nada de lo que la vista tenía alrededor.

> [!danger] Recrear una vista pierde `security_invoker` y los `GRANT`
> Las dos cosas viven **fuera** de la definición. Si se recrea a secas:
> - vuelve al default `security definer` → **se saltea RLS** (el problema del inicio de esta nota, reintroducido sin que nada falle);
> - se queda sin los `GRANT` → `anon`/`authenticated` dejan de poder leerla y la app rompe con un 401/404 de PostgREST.
>
> Ninguna de las dos avisa en el momento: la migración dice `success` igual.

Plantilla de la migración:

```sql
-- 1. Guardar la definición actual ANTES de soltar nada
--    select pg_get_viewdef('public.mi_vista'::regclass, true);
-- 2. Y sus grants:
--    select grantee, privilege_type from information_schema.role_table_grants
--     where table_schema='public' and table_name='mi_vista';
-- 3. Y si es security_invoker:
--    select * from pg_options_to_table((select reloptions from pg_class
--     where oid='public.mi_vista'::regclass));

drop view if exists public.mi_vista;

alter table public.mi_tabla alter column mi_columna type varchar(20);

create view public.mi_vista
with (security_invoker = on) as
  <definición idéntica>;

grant all on table public.mi_vista to anon;
grant all on table public.mi_vista to authenticated;
grant all on table public.mi_vista to service_role;
```

Antes de soltarla, comprobar que nada más dependa de ella (otra vista que la lea también se caería en cascada):

```sql
select c.relname
from pg_depend d
join pg_rewrite rw on rw.oid = d.objid
join pg_class c on c.oid = rw.ev_class
join pg_class t on t.oid = d.refobjid
join pg_namespace n on n.oid = t.relnamespace
where n.nspname = 'public' and t.relname = 'mi_vista'
  and c.relname <> 'mi_vista'
group by c.relname;
```

**Cómo verificar después:** `get_advisors` (MCP de Supabase) debe **no** listar la vista bajo `security_definer_view`. Si aparece ahí, el `security_invoker` se perdió. Los avisos de `pg_graphql_anon_table_exposed` / `pg_graphql_authenticated_table_exposed` son otra cosa — salen por el grant global del proyecto y los tienen casi todos los objetos.

## Checklist para una vista nueva en Bimbo

1. `with (security_invoker = true)` — siempre.
2. Crear vía migración (`apply_migration`), nombre `snake_case` descriptivo.
3. Verificar grants: `select ... from information_schema.role_table_grants where table_name='mi_vista'`.
4. Probar con un rol NO-admin que solo se vean las filas que RLS permite.
5. Modelo C# aparte o retarget del existente — pero **nunca** escribir a través de la vista.
6. Anotar que la vista **congela el tipo** de las columnas que lee: cualquier migración futura que quiera cambiarlas va a tener que soltarla y recrearla (sección de arriba).

## Relaciones

- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] — primer uso (P-021)
- [[Supabase .NET]] — referencia general del SDK
- [[Bug - Filter OR con Op.Equals en postgrest-csharp]] — el otro gotcha de búsqueda
- [[Plan de Seguridad - Roadmap 10-10]] — RLS es la Fase 5 del roadmap
- [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]] — donde apareció el bloqueo del `ALTER TYPE` por una vista
- [[Bug - CREATE OR REPLACE FUNCTION con distinta cantidad de parametros duplica en vez de reemplazar (Postgres)]] — el otro gotcha de DDL en Postgres
