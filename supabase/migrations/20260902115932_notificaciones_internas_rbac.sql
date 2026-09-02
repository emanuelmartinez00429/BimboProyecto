-- Submodulo de notificaciones internas, RBAC por codigo e idempotencia de Roles.
-- Supabase es la unica fuente de verdad; no existe persistencia local/offline.

-- ---------------------------------------------------------------------------
-- 1. Codigos RBAC estables
-- ---------------------------------------------------------------------------

alter table public.acciones add column if not exists codigo_accion varchar(100);

update public.acciones set codigo_accion = case id_accion
  when 1 then 'PRODUCTOS_CREAR' when 2 then 'PRODUCTOS_MODIFICAR'
  when 3 then 'PRODUCTOS_ELIMINAR' when 4 then 'PRODUCTOS_CONSULTAR'
  when 5 then 'EMPLEADOS_CREAR' when 6 then 'EMPLEADOS_MODIFICAR'
  when 7 then 'EMPLEADOS_ELIMINAR' when 8 then 'EMPLEADOS_CONSULTAR'
  when 9 then 'PESAJES_REGISTRAR_ENTRADA' when 10 then 'PESAJES_MODIFICAR'
  when 11 then 'PESAJES_COMPLETAR' when 12 then 'PESAJES_CANCELAR'
  when 13 then 'PESAJES_CONSULTAR' when 14 then 'PROVEEDORES_CREAR'
  when 15 then 'PROVEEDORES_MODIFICAR' when 16 then 'PROVEEDORES_ELIMINAR'
  when 17 then 'FABRICANTES_CREAR' when 18 then 'FABRICANTES_MODIFICAR'
  when 19 then 'REPORTES_GENERAR' when 20 then 'REPORTES_EXPORTAR'
  when 21 then 'REPORTES_CONSULTAR' when 22 then 'CONFIGURACION_MODIFICAR'
  when 23 then 'USUARIOS_CREAR' when 24 then 'USUARIOS_MODIFICAR'
  when 25 then 'USUARIOS_ELIMINAR' when 26 then 'PROVEEDORES_CONSULTAR'
  when 27 then 'FABRICANTES_CONSULTAR' when 28 then 'USUARIOS_CONSULTAR'
  when 29 then 'ROLES_CONSULTAR' when 30 then 'ROLES_CREAR'
  when 31 then 'ROLES_MODIFICAR' when 32 then 'ROLES_CAMBIAR_ESTADO'
  when 33 then 'ROLES_ASIGNAR_PERMISOS' when 34 then 'USUARIOS_ASIGNAR_ROL'
  else codigo_accion
end
where codigo_accion is null;

update public.acciones
set nombre_accion = 'Cambiar Estado de Rol'
where id_accion = 32 and nombre_accion = 'Eliminar Rol';

do $do$
declare v_modulo integer;
begin
  insert into public.modulos(nombre_modulo, descripcion_modulo)
  values ('Notificaciones', 'Bandeja de eventos internos del sistema')
  on conflict (nombre_modulo) do update
    set descripcion_modulo = excluded.descripcion_modulo
  returning id_modulo into v_modulo;

  if v_modulo is null then
    select id_modulo into v_modulo from public.modulos where nombre_modulo='Notificaciones';
  end if;

  insert into public.acciones(nombre_accion,id_modulo,descripcion_accion,codigo_accion)
  values
    ('Consultar Notificaciones',v_modulo,'Consultar, leer y archivar notificaciones propias','NOTIFICACIONES_CONSULTAR'),
    ('Gestionar Notificaciones',v_modulo,'Administrar alertas globalmente resolubles','NOTIFICACIONES_GESTIONAR')
  on conflict (nombre_accion,id_modulo) do update set
    descripcion_accion=excluded.descripcion_accion,
    codigo_accion=excluded.codigo_accion;
end
$do$;

alter table public.acciones alter column codigo_accion set not null;
alter table public.acciones drop constraint if exists acciones_codigo_accion_formato;
alter table public.acciones add constraint acciones_codigo_accion_formato
  check (codigo_accion ~ '^[A-Z][A-Z0-9_]*$');
create unique index if not exists acciones_codigo_accion_uidx
  on public.acciones(codigo_accion);

create or replace function private.usuario_tiene_permiso_codigo(p_codigo_accion text)
returns boolean
language sql
stable
security definer
set search_path = pg_catalog, pg_temp
as $function$
  select exists (
    select 1
    from public.usuarios u
    join public.roles r on r.id_rol=u.id_rol and r.id_estado=1
    join public.acciones_roles ar on ar.id_rol=r.id_rol and ar.id_estado=1
    join public.acciones a on a.id_accion=ar.id_accion
    where u.uuid_usuario=auth.uid()
      and u.id_estado=1
      and a.codigo_accion=p_codigo_accion
  );
$function$;

revoke all on function private.usuario_tiene_permiso_codigo(text) from public,anon;
grant execute on function private.usuario_tiene_permiso_codigo(text) to authenticated;

-- ---------------------------------------------------------------------------
-- 2. Infraestructura generica de idempotencia y auditoria RBAC
-- ---------------------------------------------------------------------------

alter table private.solicitudes_rpc enable row level security;
revoke all on private.solicitudes_rpc from public,anon,authenticated;

create or replace function private.preparar_solicitud_rpc(
  p_id_solicitud uuid,
  p_nombre_rpc text,
  p_codigo_accion text,
  p_parametros jsonb,
  p_id_operacion uuid default null
) returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, pg_temp
as $function$
declare
  v_uid uuid := auth.uid();
  v_usuario integer;
  v_accion integer;
  v_modulo integer;
  v_hash text;
  v_insertadas integer;
  v_existente private.solicitudes_rpc%rowtype;
begin
  if p_id_solicitud is null then
    raise exception using errcode='22004',message='id_solicitud es obligatorio';
  end if;
  select u.id_usuario into v_usuario
  from public.usuarios u join public.roles r on r.id_rol=u.id_rol and r.id_estado=1
  where u.uuid_usuario=v_uid and u.id_estado=1;
  if v_usuario is null then
    raise exception using errcode='28000',message='El usuario no existe o esta inactivo';
  end if;
  select a.id_accion,a.id_modulo into v_accion,v_modulo
  from public.acciones a
  where a.codigo_accion=p_codigo_accion
    and private.usuario_tiene_permiso_codigo(p_codigo_accion);
  if v_accion is null then
    raise exception using errcode='42501',message=format('Permiso RBAC requerido: %s',p_codigo_accion);
  end if;
  v_hash := encode(extensions.digest(convert_to(coalesce(p_parametros,'{}'::jsonb)::text,'UTF8'),'sha256'),'hex');
  insert into private.solicitudes_rpc(id_solicitud,id_operacion,id_usuario,nombre_rpc,hash_parametros)
  values(p_id_solicitud,p_id_operacion,v_usuario,p_nombre_rpc,v_hash)
  on conflict(id_solicitud) do nothing;
  get diagnostics v_insertadas=row_count;
  if v_insertadas=0 then
    select * into v_existente from private.solicitudes_rpc where id_solicitud=p_id_solicitud for update;
    if v_existente.id_usuario<>v_usuario or v_existente.nombre_rpc<>p_nombre_rpc
       or v_existente.hash_parametros<>v_hash or v_existente.id_operacion is distinct from p_id_operacion then
      raise exception using errcode='22023',message='id_solicitud ya fue utilizado con otro usuario, RPC o parametros';
    end if;
    if v_existente.estado='COMPLETADA' then
      return jsonb_build_object('es_reintento',true,'resultado',v_existente.resultado);
    end if;
    raise exception using errcode='55P03',message='La solicitud aun se encuentra en proceso';
  end if;
  return jsonb_build_object('es_reintento',false,'id_usuario',v_usuario,'id_accion',v_accion,'id_modulo',v_modulo);
end;
$function$;

create or replace function private.completar_solicitud_rpc(p_id_solicitud uuid,p_resultado jsonb)
returns jsonb language plpgsql security definer set search_path=pg_catalog,pg_temp
as $function$
declare v_filas integer;
begin
  update private.solicitudes_rpc set estado='COMPLETADA',resultado=p_resultado,fecha_completada=clock_timestamp()
  where id_solicitud=p_id_solicitud and estado='EN_PROCESO';
  get diagnostics v_filas=row_count;
  if v_filas<>1 then raise exception 'No se pudo completar exactamente una solicitud RPC'; end if;
  return p_resultado;
end;
$function$;

create or replace function private.registrar_auditoria_rbac(
  p_codigo_accion text,p_estado_anterior text,p_estado_actual text,p_campo_afectado text,
  p_tabla_afectada text,p_id_registro integer,p_campo_extra text,p_id_solicitud uuid
) returns void language plpgsql security definer set search_path=pg_catalog,pg_temp
as $function$
declare v_usuario integer; v_accion integer; v_modulo integer;
begin
  select u.id_usuario into v_usuario from public.usuarios u
  where u.uuid_usuario=auth.uid() and u.id_estado=1;
  select a.id_accion,a.id_modulo into v_accion,v_modulo from public.acciones a
  where a.codigo_accion=p_codigo_accion;
  if v_usuario is null or v_accion is null then raise exception 'No se pudo resolver actor o accion de auditoria'; end if;
  insert into public.bitacora(id_usuario,estado_anterior,estado_actual,campo_afectado,campo_extra,
    id_accion,id_modulo,tabla_afectada,id_registro_afectado,id_solicitud)
  values(v_usuario,left(coalesce(p_estado_anterior,'Sin registro'),500),left(coalesce(p_estado_actual,'Sin registro'),500),
    left(coalesce(p_campo_afectado,'Sin registro'),500),left(p_campo_extra,500),v_accion,v_modulo,
    left(p_tabla_afectada,100),p_id_registro,p_id_solicitud);
end;
$function$;

revoke all on function private.preparar_solicitud_rpc(uuid,text,text,jsonb,uuid) from public,anon,authenticated;
revoke all on function private.completar_solicitud_rpc(uuid,jsonb) from public,anon,authenticated;
revoke all on function private.registrar_auditoria_rbac(text,text,text,text,text,integer,text,uuid) from public,anon,authenticated;

-- ---------------------------------------------------------------------------
-- 3. Esquema de notificaciones
-- ---------------------------------------------------------------------------

create table public.tipos_notificacion(
  id_tipo_notificacion integer generated always as identity primary key,
  codigo varchar(80) not null unique check(codigo ~ '^[A-Z][A-Z0-9_]*$'),
  nombre varchar(120) not null,
  descripcion varchar(500),
  severidad_predeterminada varchar(20) not null check(severidad_predeterminada in('informativa','advertencia','critica')),
  id_estado integer not null references public.estado_general(id_estado),
  fecha_creacion timestamptz not null default now(),
  fecha_actualizacion timestamptz not null default now()
);

create table public.notificaciones(
  id_notificacion bigint generated always as identity primary key,
  id_tipo_notificacion integer not null references public.tipos_notificacion(id_tipo_notificacion) on delete restrict,
  titulo varchar(150) not null,
  mensaje varchar(1000) not null,
  severidad varchar(20) not null check(severidad in('informativa','advertencia','critica')),
  id_modulo integer references public.modulos(id_modulo) on delete restrict,
  id_accion integer references public.acciones(id_accion) on delete restrict,
  id_usuario_actor integer references public.usuarios(id_usuario) on delete restrict,
  tabla_origen varchar(63), id_registro_origen integer,
  id_solicitud uuid references private.solicitudes_rpc(id_solicitud) on delete restrict,
  clave_deduplicacion varchar(200),
  estado_notificacion varchar(20) not null default 'activa' check(estado_notificacion in('activa','resuelta','cancelada')),
  fecha_creacion timestamptz not null default now(),
  fecha_resolucion timestamptz,
  id_usuario_resolucion integer references public.usuarios(id_usuario) on delete restrict,
  metadata jsonb,
  constraint notificaciones_metadata_objeto check(metadata is null or jsonb_typeof(metadata)='object'),
  constraint notificaciones_resolucion_coherente check(
    (estado_notificacion='activa' and fecha_resolucion is null and id_usuario_resolucion is null)
    or (estado_notificacion in('resuelta','cancelada') and fecha_resolucion is not null)
  )
);

create table public.notificaciones_usuario(
  id_notificacion_usuario bigint generated always as identity primary key,
  id_notificacion bigint not null references public.notificaciones(id_notificacion) on delete cascade,
  id_usuario integer not null references public.usuarios(id_usuario) on delete restrict,
  fecha_asignacion timestamptz not null default now(),
  fecha_leida timestamptz,
  fecha_archivada timestamptz,
  unique(id_notificacion,id_usuario)
);

create index tipos_notificacion_estado_codigo_idx on public.tipos_notificacion(id_estado,codigo);
create index notificaciones_fecha_idx on public.notificaciones(fecha_creacion desc,id_notificacion desc);
create index notificaciones_tipo_fecha_idx on public.notificaciones(id_tipo_notificacion,fecha_creacion desc);
create index notificaciones_estado_fecha_idx on public.notificaciones(estado_notificacion,fecha_creacion desc);
create index notificaciones_solicitud_idx on public.notificaciones(id_solicitud) where id_solicitud is not null;
create unique index notificaciones_tipo_solicitud_uidx on public.notificaciones(id_tipo_notificacion,id_solicitud) where id_solicitud is not null;
create unique index notificaciones_clave_activa_uidx on public.notificaciones(clave_deduplicacion) where clave_deduplicacion is not null and estado_notificacion='activa';
create index notificaciones_origen_idx on public.notificaciones(tabla_origen,id_registro_origen);
create index notificaciones_usuario_usuario_notificacion_idx on public.notificaciones_usuario(id_usuario,id_notificacion desc);
create index notificaciones_usuario_no_leidas_idx on public.notificaciones_usuario(id_usuario,id_notificacion desc) where fecha_leida is null and fecha_archivada is null;
create index usuarios_rol_estado_idx on public.usuarios(id_rol,id_estado);

insert into public.tipos_notificacion(codigo,nombre,descripcion,severidad_predeterminada,id_estado) values
('PESAJE_REGISTRADO','Pesaje registrado','Se registró un pesaje de producto','informativa',1),
('PROVEEDOR_CREADO','Proveedor creado','Se creó un proveedor','informativa',1),
('PROVEEDOR_MODIFICADO','Proveedor modificado','Se modificó un proveedor','informativa',1),
('PROVEEDOR_DESACTIVADO','Proveedor desactivado','Se desactivó un proveedor','advertencia',1),
('FABRICANTE_CREADO','Fabricante creado','Se creó un fabricante','informativa',1),
('FABRICANTE_MODIFICADO','Fabricante modificado','Se modificó un fabricante','informativa',1),
('FABRICANTE_DESACTIVADO','Fabricante desactivado','Se desactivó un fabricante','advertencia',1),
('USUARIO_CREADO','Usuario creado','Se creó un usuario','informativa',1),
('USUARIO_MODIFICADO','Usuario modificado','Se modificó un usuario','informativa',1),
('USUARIO_DESACTIVADO','Usuario desactivado','Se desactivó un usuario','advertencia',1),
('PRODUCTO_CREADO','Producto creado','Se creó un producto','informativa',1),
('PRODUCTO_MODIFICADO','Producto modificado','Se modificó un producto','informativa',1),
('PRODUCTO_DESACTIVADO','Producto desactivado','Se desactivó un producto','advertencia',1),
('CATEGORIA_CREADA','Categoría creada','Se creó una categoría','informativa',1),
('CATEGORIA_MODIFICADA','Categoría modificada','Se modificó una categoría','informativa',1),
('CATEGORIA_DESACTIVADA','Categoría desactivada','Se desactivó una categoría','advertencia',1),
('PERMISOS_ROL_MODIFICADOS','Permisos de rol modificados','Se modificaron los permisos de un rol','advertencia',1),
('ROL_USUARIO_MODIFICADO','Rol de usuario modificado','Se cambió el rol de un usuario','advertencia',1),
('PRODUCTO_EXISTENCIA_BAJA','Producto con poca existencia','Un producto cruzó su existencia mínima','advertencia',2);

create or replace function private.crear_notificacion(
  p_codigo_tipo text,p_titulo text,p_mensaje text,p_severidad text default null,
  p_id_modulo integer default null,p_id_accion integer default null,p_tabla_origen text default null,
  p_id_registro_origen integer default null,p_id_solicitud uuid default null,
  p_clave_deduplicacion text default null,p_metadata jsonb default null
) returns bigint language plpgsql security definer set search_path=pg_catalog,pg_temp
as $function$
declare v_tipo public.tipos_notificacion%rowtype; v_usuario integer; v_id bigint;
begin
  select * into v_tipo from public.tipos_notificacion where codigo=p_codigo_tipo and id_estado=1;
  if not found then raise exception using errcode='22023',message='Tipo de notificacion inexistente o inactivo'; end if;
  select id_usuario into v_usuario from public.usuarios where uuid_usuario=auth.uid() and id_estado=1;
  if v_usuario is null then raise exception using errcode='28000',message='Usuario autenticado inexistente o inactivo'; end if;
  if p_metadata is not null and jsonb_typeof(p_metadata)<>'object' then raise exception 'metadata debe ser un objeto JSON'; end if;
  insert into public.notificaciones(id_tipo_notificacion,titulo,mensaje,severidad,id_modulo,id_accion,id_usuario_actor,
    tabla_origen,id_registro_origen,id_solicitud,clave_deduplicacion,metadata)
  values(v_tipo.id_tipo_notificacion,left(btrim(p_titulo),150),left(btrim(p_mensaje),1000),
    coalesce(p_severidad,v_tipo.severidad_predeterminada),p_id_modulo,p_id_accion,v_usuario,
    p_tabla_origen,p_id_registro_origen,p_id_solicitud,p_clave_deduplicacion,p_metadata)
  on conflict(id_tipo_notificacion,id_solicitud) where id_solicitud is not null do nothing
  returning id_notificacion into v_id;
  if v_id is null and p_id_solicitud is not null then
    select id_notificacion into v_id from public.notificaciones
    where id_tipo_notificacion=v_tipo.id_tipo_notificacion and id_solicitud=p_id_solicitud;
  end if;
  if v_id is null then raise exception 'No se pudo crear la notificacion'; end if;
  insert into public.notificaciones_usuario(id_notificacion,id_usuario)
  select v_id,u.id_usuario from public.usuarios u
  join public.roles r on r.id_rol=u.id_rol and r.id_estado=1
  join public.acciones_roles ar on ar.id_rol=r.id_rol and ar.id_estado=1
  join public.acciones a on a.id_accion=ar.id_accion and a.codigo_accion='NOTIFICACIONES_CONSULTAR'
  where u.id_estado=1 on conflict(id_notificacion,id_usuario) do nothing;
  return v_id;
end;
$function$;

revoke all on function private.crear_notificacion(text,text,text,text,integer,integer,text,integer,uuid,text,jsonb) from public,anon,authenticated;

-- ---------------------------------------------------------------------------
-- 4. RLS y API publica de bandeja
-- ---------------------------------------------------------------------------

alter table public.tipos_notificacion enable row level security;
alter table public.notificaciones enable row level security;
alter table public.notificaciones_usuario enable row level security;
revoke all on public.tipos_notificacion,public.notificaciones,public.notificaciones_usuario from public,anon,authenticated;
grant select on public.tipos_notificacion,public.notificaciones,public.notificaciones_usuario to authenticated;

create policy tipos_notificacion_consulta on public.tipos_notificacion for select to authenticated
using(private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR'));
create policy notificaciones_consulta on public.notificaciones for select to authenticated using(
  private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR') and exists(
    select 1 from public.notificaciones_usuario nu join public.usuarios u on u.id_usuario=nu.id_usuario
    where nu.id_notificacion=notificaciones.id_notificacion and u.uuid_usuario=(select auth.uid()) and u.id_estado=1));
create policy notificaciones_usuario_consulta on public.notificaciones_usuario for select to authenticated using(
  private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR') and exists(
    select 1 from public.usuarios u where u.id_usuario=notificaciones_usuario.id_usuario
    and u.uuid_usuario=(select auth.uid()) and u.id_estado=1));

create or replace function public.listar_mis_notificaciones(
 p_estado_bandeja text default 'todas',p_severidad text default null,p_codigo_tipo text default null,
 p_limite integer default 25,p_cursor_fecha timestamptz default null,p_cursor_id bigint default null)
returns table(id_notificacion bigint,codigo_tipo text,nombre_tipo text,titulo text,mensaje text,severidad text,
 estado_notificacion text,fecha_creacion timestamptz,fecha_leida timestamptz,fecha_archivada timestamptz,
 actor text,id_modulo integer,id_accion integer,tabla_origen text,id_registro_origen integer,metadata jsonb)
language plpgsql security definer set search_path=pg_catalog,pg_temp
as $function$
declare v_usuario integer; v_estado text:=lower(coalesce(p_estado_bandeja,'todas'));
begin
 if not private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR') then raise exception using errcode='42501',message='No tienes permiso para consultar notificaciones'; end if;
 select u.id_usuario into v_usuario from public.usuarios u join public.roles r on r.id_rol=u.id_rol and r.id_estado=1 where u.uuid_usuario=auth.uid() and u.id_estado=1;
 if v_estado not in('todas','no_leidas','leidas','archivadas') or p_limite not between 1 and 100 then raise exception using errcode='22023',message='Filtros de notificaciones no validos'; end if;
 return query select n.id_notificacion,t.codigo::text,t.nombre::text,n.titulo::text,n.mensaje::text,n.severidad::text,
  n.estado_notificacion::text,n.fecha_creacion,nu.fecha_leida,nu.fecha_archivada,u.alias_usuario::text,
  n.id_modulo,n.id_accion,n.tabla_origen::text,n.id_registro_origen,n.metadata
 from public.notificaciones_usuario nu join public.notificaciones n using(id_notificacion)
 join public.tipos_notificacion t using(id_tipo_notificacion) left join public.usuarios u on u.id_usuario=n.id_usuario_actor
 where nu.id_usuario=v_usuario
 and (p_cursor_fecha is null or (n.fecha_creacion,n.id_notificacion)<(p_cursor_fecha,p_cursor_id))
 and (p_severidad is null or n.severidad=p_severidad) and (p_codigo_tipo is null or t.codigo=p_codigo_tipo)
 and ((v_estado='todas' and nu.fecha_archivada is null) or (v_estado='no_leidas' and nu.fecha_leida is null and nu.fecha_archivada is null)
   or (v_estado='leidas' and nu.fecha_leida is not null and nu.fecha_archivada is null) or (v_estado='archivadas' and nu.fecha_archivada is not null))
 order by n.fecha_creacion desc,n.id_notificacion desc limit p_limite;
end;
$function$;

create or replace function public.obtener_mi_notificacion(p_id_notificacion bigint)
returns setof record language sql security definer set search_path=pg_catalog,pg_temp
as $function$
 select * from public.listar_mis_notificaciones('todas',null,null,100,null,null) x
 where x.id_notificacion=p_id_notificacion;
$function$;

-- Recreate with named output because PostgREST cannot expose anonymous record reliably.
drop function public.obtener_mi_notificacion(bigint);
create function public.obtener_mi_notificacion(p_id_notificacion bigint)
returns table(id_notificacion bigint,codigo_tipo text,nombre_tipo text,titulo text,mensaje text,severidad text,
 estado_notificacion text,fecha_creacion timestamptz,fecha_leida timestamptz,fecha_archivada timestamptz,
 actor text,id_modulo integer,id_accion integer,tabla_origen text,id_registro_origen integer,metadata jsonb)
language sql security definer set search_path=pg_catalog,pg_temp
as $function$
 select * from public.listar_mis_notificaciones('todas',null,null,100,null,null) x where x.id_notificacion=p_id_notificacion;
$function$;

create or replace function public.contar_mis_notificaciones_no_leidas() returns bigint
language plpgsql security definer set search_path=pg_catalog,pg_temp as $function$
declare v_usuario integer; v_total bigint;
begin
 if not private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR') then raise exception using errcode='42501',message='No tienes permiso para consultar notificaciones'; end if;
 select id_usuario into v_usuario from public.usuarios where uuid_usuario=auth.uid() and id_estado=1;
 select count(*) into v_total from public.notificaciones_usuario nu join public.notificaciones n using(id_notificacion)
 where nu.id_usuario=v_usuario and nu.fecha_leida is null and nu.fecha_archivada is null and n.estado_notificacion='activa';
 return v_total;
end;$function$;

create or replace function private.actualizar_mi_notificacion(p_id bigint,p_operacion text) returns jsonb
language plpgsql security definer set search_path=pg_catalog,pg_temp as $function$
declare v_usuario integer; v_fila public.notificaciones_usuario%rowtype;
begin
 if not private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR') then raise exception using errcode='42501',message='No tienes permiso para gestionar tus notificaciones'; end if;
 select id_usuario into v_usuario from public.usuarios where uuid_usuario=auth.uid() and id_estado=1;
 if p_operacion='leer' then update public.notificaciones_usuario set fecha_leida=coalesce(fecha_leida,now()) where id_notificacion=p_id and id_usuario=v_usuario returning * into v_fila;
 elsif p_operacion='archivar' then update public.notificaciones_usuario set fecha_archivada=coalesce(fecha_archivada,now()) where id_notificacion=p_id and id_usuario=v_usuario returning * into v_fila;
 elsif p_operacion='restaurar' then update public.notificaciones_usuario set fecha_archivada=null where id_notificacion=p_id and id_usuario=v_usuario returning * into v_fila;
 else raise exception 'Operacion no valida'; end if;
 if not found then raise exception using errcode='P0002',message='La notificacion no pertenece al usuario autenticado'; end if;
 return jsonb_build_object('id_notificacion',v_fila.id_notificacion,'fecha_leida',v_fila.fecha_leida,'fecha_archivada',v_fila.fecha_archivada);
end;$function$;

create or replace function public.marcar_notificacion_leida(p_id_notificacion bigint) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.actualizar_mi_notificacion($1,'leer')$$;
create or replace function public.archivar_mi_notificacion(p_id_notificacion bigint) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.actualizar_mi_notificacion($1,'archivar')$$;
create or replace function public.restaurar_mi_notificacion(p_id_notificacion bigint) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.actualizar_mi_notificacion($1,'restaurar')$$;
create or replace function public.marcar_todas_mis_notificaciones_leidas() returns integer language plpgsql security definer set search_path=pg_catalog,pg_temp as $function$
declare v_usuario integer; v_total integer;
begin
 if not private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR') then raise exception using errcode='42501',message='No tienes permiso para gestionar tus notificaciones'; end if;
 select id_usuario into v_usuario from public.usuarios where uuid_usuario=auth.uid() and id_estado=1;
 update public.notificaciones_usuario set fecha_leida=coalesce(fecha_leida,now()) where id_usuario=v_usuario and fecha_leida is null and fecha_archivada is null;
 get diagnostics v_total=row_count; return v_total;
end;$function$;

revoke all on function private.actualizar_mi_notificacion(bigint,text) from public,anon,authenticated;
do $do$ declare r record; begin for r in select p.oid::regprocedure firma from pg_proc p join pg_namespace n on n.oid=p.pronamespace where n.nspname='public' and p.proname in('listar_mis_notificaciones','obtener_mi_notificacion','contar_mis_notificaciones_no_leidas','marcar_notificacion_leida','marcar_todas_mis_notificaciones_leidas','archivar_mi_notificacion','restaurar_mi_notificacion') loop execute format('revoke all on function %s from public,anon',r.firma); execute format('grant execute on function %s to authenticated',r.firma); end loop; end $do$;

-- ---------------------------------------------------------------------------
-- 5. Roles idempotentes y notificables
-- ---------------------------------------------------------------------------

drop function public.crear_rol_seguro(text);
drop function public.actualizar_rol_seguro(integer,text);
drop function public.cambiar_estado_rol_seguro(integer,integer);
drop function public.reemplazar_permisos_rol_seguro(integer,integer[]);
drop function public.asignar_rol_usuario_seguro(integer,integer);

create function public.crear_rol_seguro(p_nombre_rol text,p_id_solicitud uuid) returns jsonb language plpgsql security definer set search_path=pg_catalog,pg_temp as $function$
declare v_ctx jsonb; v_nombre text:=btrim(p_nombre_rol); v_rol public.roles%rowtype; v_resultado jsonb;
begin
 v_ctx:=private.preparar_solicitud_rpc(p_id_solicitud,'crear_rol_seguro','ROLES_CREAR',jsonb_build_object('nombre_rol',v_nombre)); if (v_ctx->>'es_reintento')::boolean then return v_ctx->'resultado'; end if;
 if v_nombre is null or v_nombre='' or char_length(v_nombre)>50 then raise exception using errcode='22023',message='El nombre del rol es obligatorio y debe tener como maximo 50 caracteres'; end if;
 insert into public.roles(nombre_rol,id_estado,es_sistema) values(v_nombre,1,false) returning * into v_rol;
 perform private.registrar_auditoria_rbac('ROLES_CREAR','{}',jsonb_build_object('nombre_rol',v_nombre)::text,'rol','roles',v_rol.id_rol,'Creacion de rol',p_id_solicitud);
 v_resultado:=jsonb_build_object('id_rol',v_rol.id_rol,'nombre_rol',v_rol.nombre_rol,'id_estado',1,'es_sistema',false,'usuarios_asignados',0,'hubo_cambios',true); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado);
exception when unique_violation then raise exception using errcode='23505',message='Ya existe un rol con ese nombre'; end;$function$;

create function public.actualizar_rol_seguro(p_id_rol integer,p_nombre_rol text,p_id_solicitud uuid) returns jsonb language plpgsql security definer set search_path=pg_catalog,pg_temp as $function$
declare v_ctx jsonb; v_nombre text:=btrim(p_nombre_rol); v_old public.roles%rowtype; v_rol public.roles%rowtype; v_usuarios integer; v_resultado jsonb;
begin
 v_ctx:=private.preparar_solicitud_rpc(p_id_solicitud,'actualizar_rol_seguro','ROLES_MODIFICAR',jsonb_build_object('id_rol',p_id_rol,'nombre_rol',v_nombre)); if (v_ctx->>'es_reintento')::boolean then return v_ctx->'resultado'; end if;
 if v_nombre is null or v_nombre='' or char_length(v_nombre)>50 then raise exception using errcode='22023',message='Nombre de rol no valido'; end if;
 select * into v_old from public.roles where id_rol=p_id_rol for update; if not found then raise exception using errcode='P0002',message='El rol no existe'; end if; if v_old.es_sistema then raise exception using errcode='42501',message='El rol Administrador no se puede renombrar'; end if;
 select count(*) into v_usuarios from public.usuarios where id_rol=p_id_rol;
 if v_old.nombre_rol=v_nombre then v_resultado:=jsonb_build_object('id_rol',v_old.id_rol,'nombre_rol',v_old.nombre_rol,'id_estado',v_old.id_estado,'es_sistema',v_old.es_sistema,'usuarios_asignados',v_usuarios,'hubo_cambios',false); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado); end if;
 update public.roles set nombre_rol=v_nombre where id_rol=p_id_rol returning * into v_rol;
 perform private.registrar_auditoria_rbac('ROLES_MODIFICAR',jsonb_build_object('nombre_rol',v_old.nombre_rol)::text,jsonb_build_object('nombre_rol',v_nombre)::text,'nombre_rol','roles',p_id_rol,'Modificacion de rol',p_id_solicitud);
 v_resultado:=jsonb_build_object('id_rol',v_rol.id_rol,'nombre_rol',v_rol.nombre_rol,'id_estado',v_rol.id_estado,'es_sistema',false,'usuarios_asignados',v_usuarios,'hubo_cambios',true); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado);
exception when unique_violation then raise exception using errcode='23505',message='Ya existe un rol con ese nombre'; end;$function$;

create function public.cambiar_estado_rol_seguro(p_id_rol integer,p_id_estado integer,p_id_solicitud uuid) returns jsonb language plpgsql security definer set search_path=pg_catalog,pg_temp as $function$
declare v_ctx jsonb; v_old public.roles%rowtype; v_rol public.roles%rowtype; v_usuarios integer; v_resultado jsonb;
begin
 v_ctx:=private.preparar_solicitud_rpc(p_id_solicitud,'cambiar_estado_rol_seguro','ROLES_CAMBIAR_ESTADO',jsonb_build_object('id_rol',p_id_rol,'id_estado',p_id_estado)); if (v_ctx->>'es_reintento')::boolean then return v_ctx->'resultado'; end if;
 if p_id_estado not in(1,2) then raise exception using errcode='22023',message='Estado de rol no valido'; end if;
 select * into v_old from public.roles where id_rol=p_id_rol for update; if not found then raise exception using errcode='P0002',message='El rol no existe'; end if; if v_old.es_sistema then raise exception using errcode='42501',message='El rol Administrador no se puede desactivar'; end if;
 select count(*) into v_usuarios from public.usuarios where id_rol=p_id_rol; if p_id_estado=2 and v_usuarios>0 then raise exception using errcode='23503',message='No se puede desactivar un rol con usuarios asignados'; end if;
 if v_old.id_estado=p_id_estado then v_resultado:=jsonb_build_object('id_rol',v_old.id_rol,'nombre_rol',v_old.nombre_rol,'id_estado',v_old.id_estado,'es_sistema',false,'usuarios_asignados',v_usuarios,'hubo_cambios',false); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado); end if;
 update public.roles set id_estado=p_id_estado where id_rol=p_id_rol returning * into v_rol;
 perform private.registrar_auditoria_rbac('ROLES_CAMBIAR_ESTADO',jsonb_build_object('id_estado',v_old.id_estado)::text,jsonb_build_object('id_estado',p_id_estado)::text,'id_estado','roles',p_id_rol,'Cambio de estado del rol',p_id_solicitud);
 v_resultado:=jsonb_build_object('id_rol',v_rol.id_rol,'nombre_rol',v_rol.nombre_rol,'id_estado',v_rol.id_estado,'es_sistema',false,'usuarios_asignados',v_usuarios,'hubo_cambios',true); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado); end;$function$;

create function public.reemplazar_permisos_rol_seguro(p_id_rol integer,p_ids_acciones integer[],p_id_solicitud uuid) returns jsonb language plpgsql security definer set search_path=pg_catalog,pg_temp as $function$
declare v_ctx jsonb; v_rol public.roles%rowtype; v_ids integer[]:=coalesce((select array_agg(distinct x order by x) from unnest(coalesce(p_ids_acciones,'{}')) x),'{}'); v_old integer[]; v_resultado jsonb; v_notif bigint;
begin
 v_ctx:=private.preparar_solicitud_rpc(p_id_solicitud,'reemplazar_permisos_rol_seguro','ROLES_ASIGNAR_PERMISOS',jsonb_build_object('id_rol',p_id_rol,'ids_acciones',v_ids)); if (v_ctx->>'es_reintento')::boolean then return v_ctx->'resultado'; end if;
 select * into v_rol from public.roles where id_rol=p_id_rol for update; if not found then raise exception using errcode='P0002',message='El rol no existe'; end if; if v_rol.id_estado<>1 then raise exception using errcode='22023',message='No se pueden modificar permisos de un rol inactivo'; end if; if v_rol.es_sistema then raise exception using errcode='42501',message='Los permisos del rol Administrador son inmutables'; end if;
 if exists(select 1 from unnest(v_ids)x left join public.acciones a on a.id_accion=x where a.id_accion is null) then raise exception using errcode='22023',message='La seleccion contiene acciones inexistentes'; end if;
 select coalesce(array_agg(id_accion order by id_accion),'{}') into v_old from public.acciones_roles where id_rol=p_id_rol and id_estado=1;
 if v_old=v_ids then v_resultado:=jsonb_build_object('id_rol',p_id_rol,'acciones',v_ids,'hubo_cambios',false); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado); end if;
 insert into public.acciones_roles(id_accion,id_rol,id_estado) select x,p_id_rol,1 from unnest(v_ids)x on conflict(id_accion,id_rol) do update set id_estado=1;
 update public.acciones_roles set id_estado=2 where id_rol=p_id_rol and id_estado<>2 and not(id_accion=any(v_ids));
 perform private.registrar_auditoria_rbac('ROLES_ASIGNAR_PERMISOS',jsonb_build_object('acciones',v_old)::text,jsonb_build_object('acciones',v_ids)::text,'acciones','acciones_roles',p_id_rol,'Reemplazo atomico de permisos',p_id_solicitud);
 v_notif:=private.crear_notificacion('PERMISOS_ROL_MODIFICADOS','Permisos de rol modificados',format('Se modificaron los permisos del rol %s',v_rol.nombre_rol),'advertencia',(v_ctx->>'id_modulo')::integer,(v_ctx->>'id_accion')::integer,'roles',p_id_rol,p_id_solicitud,null,jsonb_build_object('id_rol',p_id_rol));
 v_resultado:=jsonb_build_object('id_rol',p_id_rol,'acciones',v_ids,'id_notificacion',v_notif,'hubo_cambios',true); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado); end;$function$;

create function public.asignar_rol_usuario_seguro(p_id_usuario integer,p_id_rol integer,p_id_solicitud uuid) returns jsonb language plpgsql security definer set search_path=pg_catalog,pg_temp as $function$
declare v_ctx jsonb; v_usuario public.usuarios%rowtype; v_rol public.roles%rowtype; v_oldrol text; v_resultado jsonb; v_notif bigint;
begin
 v_ctx:=private.preparar_solicitud_rpc(p_id_solicitud,'asignar_rol_usuario_seguro','USUARIOS_ASIGNAR_ROL',jsonb_build_object('id_usuario',p_id_usuario,'id_rol',p_id_rol)); if (v_ctx->>'es_reintento')::boolean then return v_ctx->'resultado'; end if;
 select * into v_usuario from public.usuarios where id_usuario=p_id_usuario for update; if not found then raise exception using errcode='P0002',message='El usuario no existe'; end if; if v_usuario.uuid_usuario=auth.uid() then raise exception using errcode='42501',message='No puedes cambiar tu propio rol'; end if;
 select * into v_rol from public.roles where id_rol=p_id_rol and id_estado=1 for share; if not found then raise exception using errcode='22023',message='El rol no existe o esta inactivo'; end if;
 if v_usuario.id_rol=p_id_rol then v_resultado:=jsonb_build_object('id_usuario',p_id_usuario,'id_rol',p_id_rol,'hubo_cambios',false); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado); end if;
 select nombre_rol into v_oldrol from public.roles where id_rol=v_usuario.id_rol; update public.usuarios set id_rol=p_id_rol where id_usuario=p_id_usuario;
 perform private.registrar_auditoria_rbac('USUARIOS_ASIGNAR_ROL',jsonb_build_object('id_rol',v_usuario.id_rol)::text,jsonb_build_object('id_rol',p_id_rol)::text,'id_rol','usuarios',p_id_usuario,'Asignacion de rol a usuario',p_id_solicitud);
 v_notif:=private.crear_notificacion('ROL_USUARIO_MODIFICADO','Rol de usuario modificado',format('Se cambió el rol del usuario %s de %s a %s',v_usuario.alias_usuario,v_oldrol,v_rol.nombre_rol),'advertencia',(v_ctx->>'id_modulo')::integer,(v_ctx->>'id_accion')::integer,'usuarios',p_id_usuario,p_id_solicitud,null,jsonb_build_object('id_usuario',p_id_usuario,'id_rol',p_id_rol));
 v_resultado:=jsonb_build_object('id_usuario',p_id_usuario,'id_rol',p_id_rol,'id_notificacion',v_notif,'hubo_cambios',true); return private.completar_solicitud_rpc(p_id_solicitud,v_resultado); end;$function$;

do $do$ declare r record; begin for r in select p.oid::regprocedure firma from pg_proc p join pg_namespace n on n.oid=p.pronamespace where n.nspname='public' and p.proname in('crear_rol_seguro','actualizar_rol_seguro','cambiar_estado_rol_seguro','reemplazar_permisos_rol_seguro','asignar_rol_usuario_seguro') loop execute format('revoke all on function %s from public,anon',r.firma); execute format('grant execute on function %s to authenticated',r.firma); end loop; end $do$;

-- El único evento de Pesaje habilitado en esta fase es la creación real de
-- entradas_producto. Se envuelve el helper existente para conservar todo su
-- detalle auditado y agregar la notificación en la misma transacción.
alter function private.bitacora_pesaje(integer,text,text,text,text,integer,text,integer,uuid)
  rename to bitacora_pesaje_sin_notificacion;

create function private.bitacora_pesaje(
  p_id_usuario integer,p_anterior text,p_actual text,p_campo text,p_extra text,
  p_id_accion integer,p_tabla text,p_id_registro integer,p_id_solicitud uuid
) returns void language plpgsql security definer set search_path=pg_catalog,pg_temp
as $function$
declare v_notificacion bigint;
begin
  perform private.bitacora_pesaje_sin_notificacion(
    p_id_usuario,p_anterior,p_actual,p_campo,p_extra,p_id_accion,p_tabla,p_id_registro,p_id_solicitud);
  if p_tabla='entradas_producto' and p_campo='Registro de pesaje' then
    v_notificacion:=private.crear_notificacion(
      'PESAJE_REGISTRADO','Pesaje registrado',format('Se registró el pesaje %s.',p_id_registro),
      'informativa',3,p_id_accion,'entradas_producto',p_id_registro,p_id_solicitud,null,
      jsonb_build_object('id_pesaje',p_id_registro));
  end if;
end;
$function$;

revoke all on function private.bitacora_pesaje_sin_notificacion(integer,text,text,text,text,integer,text,integer,uuid) from public,anon,authenticated;
revoke all on function private.bitacora_pesaje(integer,text,text,text,text,integer,text,integer,uuid) from public,anon,authenticated;

-- Realtime publica solamente la relacion por destinatario.
do $do$ begin
 if not exists(select 1 from pg_publication_tables where pubname='supabase_realtime' and schemaname='public' and tablename='notificaciones_usuario') then
   alter publication supabase_realtime add table public.notificaciones_usuario;
 end if;
end $do$;
