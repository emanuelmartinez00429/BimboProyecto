---
title: "Supabase — Vistas SQL, RLS y security_invoker"
type: referencia
status: vigente
tags:
  - referencia
  - supabase
  - postgrest
  - seguridad
  - rls
date: 2026-07-26
updated: 2026-07-26
summary: "Toda vista expuesta en el schema public se crea así:"
scope: []
symbols:
  - Order
  - Range
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

## Checklist para una vista nueva en Bimbo

1. `with (security_invoker = true)` — siempre.
2. Crear vía migración (`apply_migration`), nombre `snake_case` descriptivo.
3. Verificar grants: `select ... from information_schema.role_table_grants where table_name='mi_vista'`.
4. Probar con un rol NO-admin que solo se vean las filas que RLS permite.
5. Modelo C# aparte o retarget del existente — pero **nunca** escribir a través de la vista.

## Relaciones

- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] — primer uso (P-021)
- [[Supabase .NET]] — referencia general del SDK
- [[Bug - Filter OR con Op.Equals en postgrest-csharp]] — el otro gotcha de búsqueda
- [[Plan de Seguridad - Roadmap 10-10]] — RLS es la Fase 5 del roadmap
