---
title: "Plan de Migración de Presentaciones a RPC segura"
tags:
  - plan
  - supabase
  - rpc
  - migracion
  - catalogos
  - rbac
date: 2026-09-03
estado: aplicado (BD + capa de datos) — UI pendiente de build/prueba
autor_cambios: Claude Fernando
---

# Plan de Migración de Presentaciones a RPC segura

> [!success] Aplicado 2026-09-03
> Migración `presentaciones_rpc_segura` aplicada en Supabase (`bzmmrifjgzlvsphctais`) y `CapaDatos`/`CapaAplicacion4` actualizados (compilan 0/0, 243/243 tests). Falta compilar y probar `PresentacionModal` (bloqueado por la app en ejecución en el momento del cambio) y el `DROP` de la función legacy. Ver [[Sesión 2026-09-03 - Presentaciones migrado a RPC segura]].

> [!warning] Estado documental original
> Plan propuesto — el SQL y el detalle de abajo son la referencia de lo que se aplicó. Complementa a [[Plan de Migración de Mutaciones Directas a RPC]]. Proyecto Supabase: `bzmmrifjgzlvsphctais` (`Bimbo_Pesaje`).

## Motivo — el hueco encontrado en las RPC

Al auditar las RPC de catálogos se confirmó que **Presentaciones es el único catálogo que nunca se migró a la familia `_seguro`**. Los demás ya están completos:

| Catálogo | Crear | Actualizar | Cambiar estado |
|---|---|---|---|
| Categoría | `crear_categoria_seguro` | `actualizar_categoria_seguro` | `cambiar_estado_categoria_seguro` |
| Proveedor | `crear_proveedor_seguro` | `actualizar_proveedor_seguro` | `cambiar_estado_proveedor_seguro` |
| Fabricante | `crear_fabricante_seguro` | `actualizar_fabricante_seguro` | `cambiar_estado_fabricante_seguro` |
| **Presentación** | ❌ `ingresar_presentacion_tabla_bitacora` (legacy INVOKER) | ❌ `.Update()` directo en C# | ❌ `.Update()` directo en C# |

`CapaDatos/Repositories/Presentaciones/PresentacionCrudRepository.cs`:
- `CreateAsync` → `client.Rpc("ingresar_presentacion_tabla_bitacora", …)` (línea ~104)
- `UpdateAsync` → `client.From<PresentacionCrud>()…Update()` (línea ~130)
- `DeleteAsync` → `client.From<PresentacionCrud>()…Set(id_estado, Inactivo).Update()` (línea ~140)

### Defectos concretos

1. **`ingresar_presentacion_tabla_bitacora` es `SECURITY INVOKER`.** Todas las `_seguro` son `SECURITY DEFINER`. Corre con privilegios del llamador (`authenticated`). Solo sigue funcionando porque `presentacion_producto` es **la única tabla de catálogo que aún tiene `INSERT/UPDATE/DELETE/TRUNCATE` directo para `authenticated` y `anon`**; en `categoria`, `proveedores`, `fabricante` y `productos` ese permiso ya se revocó.
2. **RBAC equivocado.** La función valida `id_accion = 1` (`PRODUCTOS_CREAR`) porque **no existe ninguna acción `PRESENTACIONES_*`** en el catálogo `acciones`. Escribe la bitácora como `id_accion = 1, id_modulo = 1` → toda creación de presentación queda auditada como "Crear Producto". Quien puede crear productos puede crear presentaciones; quien tiene un permiso granular de catálogo pero no productos, no puede.
3. **Doble bitácora + conflicto de permiso en el UPDATE.** La tabla tiene `trg_upd_presentacion → log_upd_presentacion()`, un trigger `AFTER UPDATE` que inserta su **propia** fila de bitácora y exige `id_accion = 2` (`PRODUCTOS_MODIFICAR`). Si se agrega `actualizar_presentacion_seguro` que ya audita por su cuenta:
   - cada edición genera **2 filas** de bitácora;
   - un usuario con `PRESENTACIONES_MODIFICAR` pero sin `PRODUCTOS_MODIFICAR` haría que el trigger lance `42501` y **revierta toda la transacción**.
   > El mismo patrón está **latente** en `categoria`/`proveedor`/`fabricante` (`log_upd_categoria` no tiene guard). Se registra como deuda aparte — ver [[Deuda Técnica - Pendientes]] P-052.
4. **Sin `p_id_solicitud`** → un reintento por timeout duplica; solo lo frena el `UNIQUE` de nombre, que devuelve error en vez de replay idempotente.
5. **`p_usuario_ingresando` viaja desde el cliente** y solo se contrasta con `auth.uid()`. Las `_seguro` derivan identidad 100 % server-side vía `private.usuario_tiene_permiso_codigo()`.
6. **`presentacion_producto` y `bitacora` con DML directo abierto a `authenticated` y `anon`** → hoy cualquier cliente puede escribir/borrar filas de esas tablas saltándose las RPC. Rompe "bitácora autoritativa".

## Decisiones tomadas (2026-09-03, Fernando)

- **Permisos:** set dedicado `PRESENTACIONES_*`, espejo exacto de Categorías (acciones 38–42), bajo `id_modulo = 1` ("Gestión de Inventario").
- **Alcance:** solo Presentaciones. El doble-log/conflicto latente en los otros 3 catálogos queda como P-052, no se toca en esta tanda.
- **Naming:** seguir la convención real en BD `crear_/actualizar_/cambiar_estado_<entidad>_seguro`. La tabla de [[Plan de Migración de Mutaciones Directas a RPC]] usa `_v2`; es nomenclatura desactualizada, se corrige ahí.

## Contrato de las RPC nuevas

Idéntico al de `actualizar_categoria_seguro` / `crear_categoria_seguro`:

- `LANGUAGE plpgsql`, `SECURITY DEFINER`, `SET search_path TO 'pg_catalog', 'public', 'private', 'auth'`.
- Permiso vía `private.usuario_tiene_permiso_codigo('PRESENTACIONES_*')`.
- Idempotencia vía `private.preparar_solicitud_rpc(p_id_solicitud, '<fn>', '<CODIGO>', v_params)` → si `es_reintento` devuelve `v_ctx->'resultado'`.
- `SELECT … FOR UPDATE` para lock de fila en update/estado.
- No-op check (`IS NOT DISTINCT FROM`) → `hubo_cambios: false`.
- `private.registrar_auditoria_rbac(…, p_id_solicitud)` para la bitácora.
- `private.crear_notificacion(…)`.
- `private.completar_solicitud_rpc(p_id_solicitud, v_resultado)`.
- Retorno `jsonb` con `id_presentacion` y `hubo_cambios`.

## Migración SQL (un solo `apply_migration`)

> `presentacion_producto`: `id_presentacion` serial PK, `nombre_presentacion varchar NOT NULL`, `descripcion_presentacion varchar NULL`, `id_estado int NOT NULL` (1 activo / 2 inactivo), `created_at`, `updated_at`. Trigger conservado: `trg_presentacion_updated_at` (BEFORE UPDATE → `actualizar_updated_at()`).

```sql
-- ============================================================
-- 1. Acciones PRESENTACIONES_* (espejo de CATEGORIAS_*, modulo 1)
-- ============================================================
-- id_accion es GENERATED ALWAYS AS IDENTITY -> no se especifica.
-- codigo_accion NO tiene indice unico propio (solo UNIQUE(nombre_accion, id_modulo))
-- -> la idempotencia se hace con NOT EXISTS, no con ON CONFLICT.
insert into public.acciones (codigo_accion, nombre_accion, id_modulo)
select v.codigo_accion, v.nombre_accion, v.id_modulo
from (values
    ('PRESENTACIONES_CREAR',      'Crear Presentación',      1),
    ('PRESENTACIONES_MODIFICAR',  'Modificar Presentación',  1),
    ('PRESENTACIONES_DESACTIVAR', 'Desactivar Presentación', 1),
    ('PRESENTACIONES_ACTIVAR',    'Activar Presentación',    1),
    ('PRESENTACIONES_CONSULTAR',  'Consultar Presentación',  1)
) as v(codigo_accion, nombre_accion, id_modulo)
where not exists (
    select 1 from public.acciones a where a.codigo_accion = v.codigo_accion
);

-- NO hace falta sembrar acciones_roles a mano: el trigger
-- private.asignar_accion_nueva_administrador sobre public.acciones
-- (AFTER INSERT FOR EACH ROW, SECURITY DEFINER, migracion 20260902032735)
-- ya inserta la fila activa (id_estado = 1) para todo rol es_sistema
-- (hoy Administrador, id_rol = 1). Confirmado en BD el 2026-09-03. Ver ADR-024.
-- Operador / Supervisor / Consulta NO reciben estas acciones: si deben tenerlas,
-- se asignan por el flujo auditado normal (RolModal / reemplazar_permisos_rol_seguro).

-- ============================================================
-- 2. crear_presentacion_seguro
-- ============================================================
create or replace function public.crear_presentacion_seguro(
    p_nombre_presentacion      varchar,
    p_descripcion_presentacion varchar,
    p_id_solicitud             uuid)
returns jsonb
language plpgsql
security definer
set search_path to 'pg_catalog', 'public', 'private', 'auth'
as $function$
declare
    v_ctx           jsonb;
    v_id            int;
    v_id_estado     int := 1;
    v_params        jsonb;
    v_resultado     jsonb;
    v_estado_actual jsonb;
begin
    if not private.usuario_tiene_permiso_codigo('PRESENTACIONES_CREAR') then
        raise exception 'No tienes permiso para crear presentaciones.' using errcode = '42501';
    end if;

    v_params := jsonb_build_object(
        'nombre', p_nombre_presentacion,
        'descripcion', p_descripcion_presentacion);
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_presentacion_seguro', 'PRESENTACIONES_CREAR', v_params);
    if (v_ctx->>'es_reintento')::boolean then
        return v_ctx->'resultado';
    end if;

    insert into public.presentacion_producto (nombre_presentacion, descripcion_presentacion, id_estado)
    values (trim(p_nombre_presentacion), nullif(trim(p_descripcion_presentacion), ''), v_id_estado)
    returning id_presentacion into v_id;

    v_estado_actual := jsonb_build_object(
        'id_presentacion', v_id,
        'nombre_presentacion', trim(p_nombre_presentacion),
        'id_estado', v_id_estado);

    perform private.registrar_auditoria_rbac(
        'PRESENTACIONES_CREAR', '{}', v_estado_actual::text,
        'Presentación nueva', 'presentacion_producto', v_id,
        'Registro creado mediante RPC segura', p_id_solicitud);

    perform private.crear_notificacion(
        'PRESENTACION_CREADA', 'Nueva presentación registrada',
        'Se ha registrado la presentación ' || trim(p_nombre_presentacion),
        'informativa',
        (v_ctx->>'id_modulo')::integer, (v_ctx->>'id_accion')::integer,
        'presentacion_producto', v_id, p_id_solicitud, null, v_estado_actual);

    v_resultado := jsonb_build_object('id_presentacion', v_id, 'hubo_cambios', true);
    perform private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    return v_resultado;
end;
$function$;

-- ============================================================
-- 3. actualizar_presentacion_seguro
-- ============================================================
create or replace function public.actualizar_presentacion_seguro(
    p_id_presentacion          int,
    p_nombre_presentacion      varchar,
    p_descripcion_presentacion varchar,
    p_id_solicitud             uuid)
returns jsonb
language plpgsql
security definer
set search_path to 'pg_catalog', 'public', 'private', 'auth'
as $function$
declare
    v_ctx             jsonb;
    v_registro_actual record;
    v_params          jsonb;
    v_resultado       jsonb;
    v_estado_anterior jsonb;
    v_estado_actual   jsonb;
begin
    if not private.usuario_tiene_permiso_codigo('PRESENTACIONES_MODIFICAR') then
        raise exception 'No tienes permiso para modificar presentaciones.' using errcode = '42501';
    end if;

    v_params := jsonb_build_object(
        'id_presentacion', p_id_presentacion,
        'nombre', p_nombre_presentacion,
        'descripcion', p_descripcion_presentacion);
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_presentacion_seguro', 'PRESENTACIONES_MODIFICAR', v_params);
    if (v_ctx->>'es_reintento')::boolean then
        return v_ctx->'resultado';
    end if;

    select * into v_registro_actual from public.presentacion_producto
     where id_presentacion = p_id_presentacion for update;
    if not found then
        raise exception 'La presentación no existe.' using errcode = '22023';
    end if;

    if v_registro_actual.nombre_presentacion is not distinct from trim(p_nombre_presentacion) and
       v_registro_actual.descripcion_presentacion is not distinct from nullif(trim(p_descripcion_presentacion), '') then
        v_resultado := jsonb_build_object('id_presentacion', p_id_presentacion, 'hubo_cambios', false);
        perform private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        return v_resultado;
    end if;

    v_estado_anterior := to_jsonb(v_registro_actual);

    update public.presentacion_producto set
        nombre_presentacion = trim(p_nombre_presentacion),
        descripcion_presentacion = nullif(trim(p_descripcion_presentacion), '')
    where id_presentacion = p_id_presentacion;

    v_estado_actual := jsonb_build_object(
        'id_presentacion', p_id_presentacion,
        'nombre_presentacion', trim(p_nombre_presentacion),
        'id_estado', v_registro_actual.id_estado);

    perform private.registrar_auditoria_rbac(
        'PRESENTACIONES_MODIFICAR', v_estado_anterior::text, v_estado_actual::text,
        'Datos de la presentación', 'presentacion_producto', p_id_presentacion,
        'Actualización mediante RPC segura', p_id_solicitud);

    perform private.crear_notificacion(
        'PRESENTACION_MODIFICADA', 'Presentación modificada',
        'Se han actualizado los datos de la presentación ' || trim(p_nombre_presentacion),
        'informativa',
        (v_ctx->>'id_modulo')::integer, (v_ctx->>'id_accion')::integer,
        'presentacion_producto', p_id_presentacion, p_id_solicitud, null, v_estado_actual);

    v_resultado := jsonb_build_object('id_presentacion', p_id_presentacion, 'hubo_cambios', true);
    perform private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    return v_resultado;
end;
$function$;

-- ============================================================
-- 4. cambiar_estado_presentacion_seguro  (id_estado 1 activo / 2 inactivo)
-- ============================================================
create or replace function public.cambiar_estado_presentacion_seguro(
    p_id_presentacion int,
    p_id_estado       int,
    p_id_solicitud    uuid)
returns jsonb
language plpgsql
security definer
set search_path to 'pg_catalog', 'public', 'private', 'auth'
as $function$
declare
    v_ctx             jsonb;
    v_registro_actual record;
    v_codigo_accion   text;
    v_codigo_notif    text;
    v_titulo_notif    text;
    v_params          jsonb;
    v_resultado       jsonb;
    v_estado_anterior jsonb;
    v_estado_actual   jsonb;
begin
    if p_id_estado = 2 then
        v_codigo_accion := 'PRESENTACIONES_DESACTIVAR';
        v_codigo_notif  := 'PRESENTACION_DESACTIVADA';
        v_titulo_notif  := 'Presentación desactivada';
    else
        v_codigo_accion := 'PRESENTACIONES_ACTIVAR';
        v_codigo_notif  := 'PRESENTACION_ACTIVADA';
        v_titulo_notif  := 'Presentación activada';
    end if;

    if not private.usuario_tiene_permiso_codigo(v_codigo_accion) then
        raise exception 'No tienes permiso para cambiar el estado de presentaciones.' using errcode = '42501';
    end if;

    v_params := jsonb_build_object('id_presentacion', p_id_presentacion, 'id_estado', p_id_estado);
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_presentacion_seguro', v_codigo_accion, v_params);
    if (v_ctx->>'es_reintento')::boolean then
        return v_ctx->'resultado';
    end if;

    select * into v_registro_actual from public.presentacion_producto
     where id_presentacion = p_id_presentacion for update;
    if not found then
        raise exception 'La presentación no existe.' using errcode = '22023';
    end if;

    if v_registro_actual.id_estado = p_id_estado then
        v_resultado := jsonb_build_object('id_presentacion', p_id_presentacion, 'hubo_cambios', false);
        perform private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        return v_resultado;
    end if;

    v_estado_anterior := to_jsonb(v_registro_actual);
    update public.presentacion_producto set id_estado = p_id_estado where id_presentacion = p_id_presentacion;
    v_estado_actual := jsonb_build_object('id_presentacion', p_id_presentacion, 'id_estado', p_id_estado);

    perform private.registrar_auditoria_rbac(
        v_codigo_accion, v_estado_anterior::text, v_estado_actual::text,
        'Estado de la presentación', 'presentacion_producto', p_id_presentacion,
        'Cambio de estado mediante RPC segura', p_id_solicitud);

    perform private.crear_notificacion(
        v_codigo_notif, v_titulo_notif,
        'La presentación ' || v_registro_actual.nombre_presentacion ||
        case when p_id_estado = 2 then ' ha sido desactivada' else ' ha sido activada' end,
        'informativa',
        (v_ctx->>'id_modulo')::integer, (v_ctx->>'id_accion')::integer,
        'presentacion_producto', p_id_presentacion, p_id_solicitud, null, v_estado_actual);

    v_resultado := jsonb_build_object('id_presentacion', p_id_presentacion, 'hubo_cambios', true);
    perform private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    return v_resultado;
end;
$function$;

-- ============================================================
-- 5. Retirar el trigger legacy de auditoría (ya audita la RPC)
-- ============================================================
drop trigger if exists trg_upd_presentacion on public.presentacion_producto;
-- conservar trg_presentacion_updated_at.
-- Dejar log_upd_presentacion() en el esquema por si hay que revertir; sin trigger no corre.

-- ============================================================
-- 6. Cerrar el DML directo sobre presentacion_producto
-- ============================================================
revoke insert, update, delete, truncate on public.presentacion_producto from authenticated, anon;

-- ============================================================
-- 7. Permisos de ejecución de las nuevas RPC
-- ============================================================
grant execute on function public.crear_presentacion_seguro(varchar, varchar, uuid)          to authenticated;
grant execute on function public.actualizar_presentacion_seguro(int, varchar, varchar, uuid) to authenticated;
grant execute on function public.cambiar_estado_presentacion_seguro(int, int, uuid)          to authenticated;
revoke execute on function public.crear_presentacion_seguro(varchar, varchar, uuid)          from anon, public;
revoke execute on function public.actualizar_presentacion_seguro(int, varchar, varchar, uuid) from anon, public;
revoke execute on function public.cambiar_estado_presentacion_seguro(int, int, uuid)          from anon, public;

-- ============================================================
-- 8. (opcional, recomendado — decisión aparte) bitácora no editable por el cliente
-- ============================================================
-- revoke insert, update, delete, truncate on public.bitacora from authenticated, anon;
```

### Verificaciones previas a aplicar

- ~~Confirmar si **ADR-024** auto-propaga acciones nuevas al Administrador.~~ **Confirmado 2026-09-03:** el trigger `private.asignar_accion_nueva_administrador` sobre `public.acciones` (AFTER INSERT, SECURITY DEFINER) lo hace. El `INSERT INTO public.acciones` del paso 1 es suficiente; solo el Administrador (`id_rol = 1`) queda cubierto.
- Confirmar la firma exacta de `public.acciones` (¿`codigo_accion` tiene `UNIQUE`? ¿hay más columnas `NOT NULL` como `id_estado`?). Si `codigo_accion` no tiene índice único, el `on conflict (codigo_accion)` del paso 1 falla — reemplazar por `where not exists (select 1 from public.acciones where codigo_accion = ...)`.
- Confirmar que no exista un trigger `BEFORE/AFTER INSERT` de auditoría sobre `presentacion_producto` (solo se vio `trg_upd_*`).
- Tras aplicar: el Administrador debe **cerrar sesión y volver a entrar** para que su sesión y el caché de catálogo tomen las acciones `PRESENTACIONES_*` (ADR-024, sección Riesgos).

## Cambios en C#

`CapaDatos/Repositories/Presentaciones/PresentacionCrudRepository.cs` — usar `CategoriaCrudRepository` como plantilla exacta:

| Método | Antes | Después |
|---|---|---|
| `CreateAsync` | `Rpc("ingresar_presentacion_tabla_bitacora", {p_nombre, p_descripcion, p_id_estado, p_usuario_ingresando})` | `Rpc("crear_presentacion_seguro", {p_nombre_presentacion, p_descripcion_presentacion, p_id_solicitud})` |
| `UpdateAsync` | `From<PresentacionCrud>()…Update()` | `Rpc("actualizar_presentacion_seguro", {p_id_presentacion, p_nombre_presentacion, p_descripcion_presentacion, p_id_solicitud})` |
| `DeleteAsync` | `From<PresentacionCrud>()…Set(id_estado, Inactivo).Update()` | `Rpc("cambiar_estado_presentacion_seguro", {p_id_presentacion, p_id_estado: 2, p_id_solicitud})` |

- `p_id_solicitud`: `Guid.NewGuid()` generado en la capa que origina el comando (no dentro del repositorio), alineado con el contrato de [[Plan de Migración de Mutaciones Directas a RPC]].
- Parsear el `jsonb` de respuesta (`id_presentacion`, `hubo_cambios`) igual que `CategoriaCrudRepository`.
- Quitar el uso de `IUsuarioSesionService.SesionActual.IdUsuario` para creación (ya no se envía).
- Búsqueda estática: **0** `.Update()` / `.Insert()` directos en el repositorio.

## Verificación

- `dotnet build BimboProyecto.sln` → 0 errores, 0 advertencias.
- `dotnet test BimboProyecto.sln` → verde.
- Prueba remota real contra Supabase con sesión válida (las sesiones [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]] y [[Sesión 2026-08-16 - Creación auditada de catálogos y contactos mediante RPC]] nunca se probaron contra la base — por eso Presentaciones llegó a este estado):
  - crear / editar / desactivar / reactivar una presentación con un rol que tenga solo `PRESENTACIONES_*` (sin `PRODUCTOS_*`) → debe funcionar y auditar como Presentación.
  - reintento con el mismo `p_id_solicitud` → replay idempotente, sin fila duplicada.
  - rol sin permiso → `42501`, sin cambios y sin fila de bitácora.
  - una sola fila de bitácora por operación (no dos).

## Rollout y reversión

1. Aplicar la migración SQL (funciones + acciones + revoke + drop trigger) sin tocar el cliente.
2. Verificar en Supabase: ejecución autorizada, bitácora única, idempotencia.
3. Desplegar el C# apuntando a las `_seguro`.
4. Ante falla: recrear `trg_upd_presentacion`, re-`grant` el DML directo sobre `presentacion_producto`, y volver el C# a la ruta heredada. `ingresar_presentacion_tabla_bitacora` se deja en el esquema durante la transición; se hace `DROP` recién cuando ningún cliente la llama.
5. No hacer dual-write en ningún momento.

## Pendientes / decisiones abiertas

- ~~¿ADR-024 auto-propaga acciones nuevas al Administrador?~~ **Sí, confirmado 2026-09-03** (trigger `asignar_accion_nueva_administrador`, migración `20260902032735`).
- ¿Se aplica también el paso 8 (revocar DML de `bitacora` a `authenticated`/`anon`)? Recomendado, pero es un cambio transversal — decisión de Fernando.
- Renombrar en [[Plan de Migración de Mutaciones Directas a RPC]] las filas 15/16 de `_v2` a `_seguro` y agregar la fila de creación de Presentación (hoy no figura porque esa tabla ya "usaba RPC", pero es la legacy INVOKER).

## Relaciones

- [[Plan de Migración de Mutaciones Directas a RPC]] — plan maestro; este lo detalla para Presentaciones
- [[Deuda Técnica - Pendientes]] — P-052 (doble-log latente en categoría/proveedor/fabricante), P-036, P-041
- [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]] — origen del módulo
- [[Sesión 2026-08-16 - Creación auditada de catálogos y contactos mediante RPC]] — migración parcial que dejó fuera a Presentaciones
- [[Módulos de Catálogos Administrativos]] — patrón de los catálogos hermanos
- [[Arquitectura Actual]] — estado vivo del sistema
- [[ADR-024 - Rol Administrador inmutable con acceso total]] — propagación de acciones al Administrador
