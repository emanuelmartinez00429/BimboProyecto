-- Preferencias por usuario (Fase 1 del plan de escalado propio de la app).
--
-- Tabla clave/valor con jsonb: agregar una preferencia nueva (densidad de tablas,
-- filas por pagina, pantalla de inicio...) no necesita migracion.
--
-- `ambito` distingue preferencias que dependen del contexto de las que no:
--   'global'            -> casi todas
--   '1920x1080@1.75'    -> huella de pantalla, solo la usa `escala_ui`
-- Un operario que usa la terminal de planta y una PC de oficina necesita factores
-- de escala distintos: el 0.8 de una deja la otra ilegible.
--
-- Escritura DIRECTA (upsert), sin pasar por el camino de RPC con idempotencia y
-- auditoria RBAC que usan las entidades de negocio: la preferencia es del propio
-- usuario, no toca datos de negocio, y auditar cada ajuste de escala llenaria la
-- bitacora de ruido. RLS garantiza que nadie escriba la fila de otro.

create table if not exists public.usuario_preferencias (
  id_usuario  integer     not null
              references public.usuarios(id_usuario) on delete cascade,
  clave       varchar(64) not null,
  ambito      varchar(64) not null default 'global',
  valor       jsonb       not null,
  created_at  timestamptz not null default (CURRENT_TIMESTAMP AT TIME ZONE 'America/Tegucigalpa'),
  updated_at  timestamptz not null default (CURRENT_TIMESTAMP AT TIME ZONE 'America/Tegucigalpa'),

  constraint usuario_preferencias_pk primary key (id_usuario, clave, ambito),

  -- Mismo criterio que acciones_codigo_accion_formato: formato cerrado para que la
  -- clave no se convierta en texto libre escrito por el cliente.
  constraint usuario_preferencias_clave_formato
    check (clave ~ '^[a-z][a-z0-9_]*$'),

  -- 'global' o una huella de pantalla ANCHOxALTO@ESCALA (ej. 1920x1080@1.75).
  constraint usuario_preferencias_ambito_formato
    check (ambito = 'global' or ambito ~ '^[0-9]{3,5}x[0-9]{3,5}@[0-9]+(\.[0-9]+)?$'),

  -- El cliente escribe esta fila directamente, asi que el valor lleva tope: una
  -- preferencia legitima pesa decenas de bytes, no kilobytes.
  constraint usuario_preferencias_valor_tamano
    check (octet_length(valor::text) <= 4096)
);

comment on table public.usuario_preferencias is
  'Preferencias personales por usuario. Clave/valor jsonb; `ambito` = ''global'' o huella de pantalla.';

create index if not exists idx_usuario_preferencias_usuario
  on public.usuario_preferencias(id_usuario);

-- updated_at automatico: misma funcion compartida y mismo nombre de trigger que el
-- resto de las tablas (trg_<tabla>_updated_at).
drop trigger if exists trg_usuario_preferencias_updated_at on public.usuario_preferencias;
create trigger trg_usuario_preferencias_updated_at
  before update on public.usuario_preferencias
  for each row execute function public.actualizar_updated_at();

-- ---------------------------------------------------------------------------
-- Tope de filas por usuario
-- ---------------------------------------------------------------------------
-- Al conceder INSERT directo, nada impediria que una sesion autenticada creara
-- claves arbitrarias sin limite. El formato de `clave` acota el contenido pero no
-- la cantidad. 100 filas alcanzan de sobra (un punado de preferencias globales mas
-- una escala por pantalla conocida).

create or replace function private.limitar_preferencias_usuario()
returns trigger
language plpgsql
security definer
set search_path = pg_catalog, pg_temp
as $function$
begin
  if (select count(*) from public.usuario_preferencias where id_usuario = new.id_usuario) >= 100 then
    raise exception 'El usuario % ya alcanzo el maximo de preferencias almacenadas.', new.id_usuario
      using errcode = 'check_violation';
  end if;
  return new;
end;
$function$;

revoke all on function private.limitar_preferencias_usuario() from public, anon;

drop trigger if exists trg_usuario_preferencias_tope on public.usuario_preferencias;
create trigger trg_usuario_preferencias_tope
  before insert on public.usuario_preferencias
  for each row execute function private.limitar_preferencias_usuario();

-- ---------------------------------------------------------------------------
-- Permisos y RLS
-- ---------------------------------------------------------------------------
-- Solo `authenticated`: una preferencia siempre pertenece a un usuario con sesion.
-- `anon` no recibe nada (a diferencia de `empresa`, que si lo concede).
--
-- El revoke incluye a `authenticated` a proposito: Supabase concede ALL por defecto
-- sobre cada tabla nueva de `public`, y ese ALL trae TRUNCATE. RLS **no** filtra
-- TRUNCATE — es privilegio puro —, asi que una tabla con RLS impecable igual se
-- puede vaciar entera si el rol conserva ese permiso. Se revoca todo y se conceden
-- las cuatro operaciones que la app realmente usa.

revoke all on public.usuario_preferencias from public, anon, authenticated;
grant select, insert, update, delete on public.usuario_preferencias to authenticated;

alter table public.usuario_preferencias enable row level security;

-- Mismo patron que notificaciones_usuario_consulta: `to authenticated`,
-- `(select auth.uid())` envuelto para que el planner lo evalue una sola vez,
-- y el usuario debe estar activo (id_estado = 1).

drop policy if exists usuario_preferencias_consulta on public.usuario_preferencias;
create policy usuario_preferencias_consulta on public.usuario_preferencias
for select to authenticated using (
  exists (select 1 from public.usuarios u
          where u.id_usuario = usuario_preferencias.id_usuario
            and u.uuid_usuario = (select auth.uid())
            and u.id_estado = 1));

drop policy if exists usuario_preferencias_insercion on public.usuario_preferencias;
create policy usuario_preferencias_insercion on public.usuario_preferencias
for insert to authenticated with check (
  exists (select 1 from public.usuarios u
          where u.id_usuario = usuario_preferencias.id_usuario
            and u.uuid_usuario = (select auth.uid())
            and u.id_estado = 1));

-- USING y WITH CHECK: sin el segundo, una fila propia podria reasignarse a otro
-- id_usuario en el UPDATE.
drop policy if exists usuario_preferencias_actualizacion on public.usuario_preferencias;
create policy usuario_preferencias_actualizacion on public.usuario_preferencias
for update to authenticated
using (
  exists (select 1 from public.usuarios u
          where u.id_usuario = usuario_preferencias.id_usuario
            and u.uuid_usuario = (select auth.uid())
            and u.id_estado = 1))
with check (
  exists (select 1 from public.usuarios u
          where u.id_usuario = usuario_preferencias.id_usuario
            and u.uuid_usuario = (select auth.uid())
            and u.id_estado = 1));

drop policy if exists usuario_preferencias_borrado on public.usuario_preferencias;
create policy usuario_preferencias_borrado on public.usuario_preferencias
for delete to authenticated using (
  exists (select 1 from public.usuarios u
          where u.id_usuario = usuario_preferencias.id_usuario
            and u.uuid_usuario = (select auth.uid())
            and u.id_estado = 1));
