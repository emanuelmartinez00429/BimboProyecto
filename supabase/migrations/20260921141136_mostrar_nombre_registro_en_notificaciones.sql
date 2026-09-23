-- Enriquece la metadata de lectura sin modificar el historial almacenado.
-- El identificador canonico se conserva para navegacion, pero la UI recibe
-- un nombre legible para los origenes que actualmente puede abrir.
create or replace function public.listar_mis_notificaciones(
 p_estado_bandeja text default 'todas',
 p_severidad text default null,
 p_codigo_tipo text default null,
 p_limite integer default 25,
 p_cursor_fecha timestamptz default null,
 p_cursor_id bigint default null)
returns table(
 id_notificacion bigint,
 codigo_tipo text,
 nombre_tipo text,
 titulo text,
 mensaje text,
 severidad text,
 estado_notificacion text,
 fecha_creacion timestamptz,
 fecha_leida timestamptz,
 fecha_archivada timestamptz,
 actor text,
 id_modulo integer,
 id_accion integer,
 tabla_origen text,
 id_registro_origen integer,
 metadata jsonb)
language plpgsql
security definer
set search_path=pg_catalog,pg_temp
as $function$
declare
 v_usuario integer;
 v_estado text:=lower(coalesce(p_estado_bandeja,'todas'));
begin
 if not private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR') then
  raise exception using errcode='42501',message='No tienes permiso para consultar notificaciones';
 end if;

 select usuario.id_usuario
 into v_usuario
 from public.usuarios usuario
 join public.roles rol_actor on rol_actor.id_rol=usuario.id_rol and rol_actor.id_estado=1
 where usuario.uuid_usuario=auth.uid() and usuario.id_estado=1;

 if v_estado not in('todas','no_leidas','leidas','archivadas') or p_limite not between 1 and 100 then
  raise exception using errcode='22023',message='Filtros de notificaciones no validos';
 end if;

 return query
 select
  n.id_notificacion,
  t.codigo::text,
  t.nombre::text,
  n.titulo::text,
  n.mensaje::text,
  n.severidad::text,
  n.estado_notificacion::text,
  n.fecha_creacion,
  nu.fecha_leida,
  nu.fecha_archivada,
  actor_usuario.alias_usuario::text,
  n.id_modulo,
  n.id_accion,
  n.tabla_origen::text,
  n.id_registro_origen,
  coalesce(n.metadata,'{}'::jsonb) || jsonb_strip_nulls(jsonb_build_object(
   'nombre_registro_origen',
   case
    when n.tabla_origen='usuarios' then coalesce(
     nullif(btrim(concat_ws(' ',empleado_origen.nombre_empleado,empleado_origen.apellido_empleado)),''),
     usuario_origen.alias_usuario::text)
    when n.tabla_origen='roles' then rol_origen.nombre_rol::text
    else null
   end))
 from public.notificaciones_usuario nu
 join public.notificaciones n using(id_notificacion)
 join public.tipos_notificacion t using(id_tipo_notificacion)
 left join public.usuarios actor_usuario on actor_usuario.id_usuario=n.id_usuario_actor
 left join public.usuarios usuario_origen
  on n.tabla_origen='usuarios' and usuario_origen.id_usuario=n.id_registro_origen
 left join public.empleados empleado_origen on empleado_origen.id_empleado=usuario_origen.id_empleado
 left join public.roles rol_origen
  on n.tabla_origen='roles' and rol_origen.id_rol=n.id_registro_origen
 where nu.id_usuario=v_usuario
 and (p_cursor_fecha is null or (n.fecha_creacion,n.id_notificacion)<(p_cursor_fecha,p_cursor_id))
 and (p_severidad is null or n.severidad=p_severidad)
 and (p_codigo_tipo is null or t.codigo=p_codigo_tipo)
 and ((v_estado='todas' and nu.fecha_archivada is null)
   or (v_estado='no_leidas' and nu.fecha_leida is null and nu.fecha_archivada is null)
   or (v_estado='leidas' and nu.fecha_leida is not null and nu.fecha_archivada is null)
   or (v_estado='archivadas' and nu.fecha_archivada is not null))
 order by n.fecha_creacion desc,n.id_notificacion desc
 limit p_limite;
end;
$function$;

revoke all on function public.listar_mis_notificaciones(text,text,text,integer,timestamptz,bigint) from public,anon;
grant execute on function public.listar_mis_notificaciones(text,text,text,integer,timestamptz,bigint) to authenticated;

comment on function public.listar_mis_notificaciones(text,text,text,integer,timestamptz,bigint) is
'Lista la bandeja propia y agrega nombre_registro_origen a la metadata de salida sin alterar el historial ni exponer el identificador en la UI.';
