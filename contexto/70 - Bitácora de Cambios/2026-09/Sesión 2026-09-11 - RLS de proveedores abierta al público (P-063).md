---
title: "Sesión 2026-09-11 — RLS de proveedores abierta al público (P-063)"
date: 2026-09-11
tags:
  - bitacora
  - sesion
  - seguridad
  - rls
  - supabase
  - security-review
aliases:
  - P-063
  - RLS proveedores publico
---

# Sesión 2026-09-11 — RLS de proveedores abierta al público (P-063)

## Resumen

`/security-review` sobre el módulo de Productos (metodología de 3 fases: subagente identificador → subagente verificador de falsos positivos con acceso en vivo a Supabase → solo se reportan hallazgos con confianza ≥8) encontró y confirmó una fuga real de PII de proveedores: la política RLS `select_Proveedores` sobre `public.proveedores` tenía `roles={public}`, `qual=true` — cualquiera, sin sesión iniciada ni ningún permiso, podía leer RTN, teléfono, correo y dirección de todos los proveedores. Corregido con una migración aplicada en vivo (`fix_select_proveedores_rls_publico`, versión `20260911094715`).

## Cómo se disparó

El rediseño del módulo Productos de esta rama cambió `ProductoCrudRepository.SelectPara()` de `fabricante(*)` a `fabricante(*, proveedores(*))`, embebiendo el proveedor completo en cada consulta de listado, paginación y sugerencias de Productos. La política ya estaba abierta desde antes (no aparece en ninguna migración trackeada del repo, así que no se pudo determinar cuándo se creó), pero el embed nuevo fue lo que la convirtió en una fuga alcanzable simplemente usando la pantalla de Productos con normalidad — no hacía falta ningún filtro por proveedor ni acción especial.

## Verificación previa a corregir

Consulta directa a `pg_policies` en el proyecto `bzmmrifjgzlvsphctais`:

```sql
SELECT schemaname, tablename, policyname, roles, cmd, qual
FROM pg_policies
WHERE tablename IN ('proveedores','fabricante','productos','usuarios');
```

```
proveedores | select_Proveedores | roles={public}       | cmd=SELECT | qual=true
fabricante  | select_Fabricante  | roles={authenticated} | cmd=SELECT | qual=true
productos   | select_Productos   | roles={authenticated} | cmd=SELECT | qual=true
usuarios    | select_Usuarios    | roles={public}        | cmd=SELECT | qual=(uuid_usuario = auth.uid()) OR usuario_tiene_permiso_codigo('USUARIOS_CONSULTAR') OR usuario_tiene_permiso_codigo('USUARIOS_VER')
```

`select_Usuarios` (arreglada antes, P-051) fue la referencia del patrón correcto a seguir.

## Por qué la solución no es "solo `PROVEEDORES_CONSULTAR`"

El selector de catálogo (lupa) de `ProductoModal` y `FabricanteModal` llama a `Catalogos.Proveedores(_catalogos)` → `ICatalogoRepository.GetProveedoresAsync` → `CatalogoRepository.GetProveedoresAsync` (`CapaDatos/Repositories/Catalogos/CatalogoRepository.cs:126-136`), que lee esta misma tabla `proveedores` **incluyendo `rtn_proveedor`** (se usa hasta como campo de búsqueda server-side y se mapea a `FiltroItem.Descripcion`). Eso lo necesita cualquiera con permiso de crear o modificar productos o fabricantes, no solo quien tiene `PROVEEDORES_CONSULTAR`. Restringir la política solo a ese permiso le habría roto el selector de proveedor a la mitad del equipo.

## Solución aplicada

Migración `supabase/migrations/20260911094715_fix_select_proveedores_rls_publico.sql`:

```sql
DROP POLICY IF EXISTS "select_Proveedores" ON public.proveedores;

CREATE POLICY "select_Proveedores" ON public.proveedores
FOR SELECT
TO public
USING (
  private.usuario_tiene_permiso_codigo('PROVEEDORES_CONSULTAR'::text)
  OR private.usuario_tiene_permiso_codigo('PRODUCTOS_CREAR'::text)
  OR private.usuario_tiene_permiso_codigo('PRODUCTOS_MODIFICAR'::text)
  OR private.usuario_tiene_permiso_codigo('FABRICANTES_CREAR'::text)
  OR private.usuario_tiene_permiso_codigo('FABRICANTES_MODIFICAR'::text)
);
```

`OR`, no `AND` — alcanza con cualquiera de los 5 permisos. Un usuario sin ninguno de los 5 ya no ve ninguna fila de `proveedores`.

## Verificación posterior

- Aplicada vía `apply_migration` (requirió confirmación explícita de Fernando — el modo automático de la sesión bloqueó el intento inicial por tratarse de un cambio a producción en vivo).
- `list_migrations` confirma `fix_select_proveedores_rls_publico` (versión `20260911094715`) como la última migración aplicada.
- Releído `pg_policies` tras aplicar: `qual` coincide exactamente con lo diseñado.
- `get_advisors` (security) corrido después de aplicar: sin ninguna advertencia nueva relacionada a `proveedores`.
- Migración también agregada como archivo local (`supabase/migrations/`) para que quede versionada en el repo, igual que el resto.

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-063
- [[Sesión 2026-09-10 - Auditoría y refactorización técnica del módulo de Productos]] — sesión donde se introdujo el embed `proveedores(*)`
- [[Plan de Seguridad - Roadmap 10-10]]
