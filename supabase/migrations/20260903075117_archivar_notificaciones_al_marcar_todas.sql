-- Conserva nombre, firma, propietario, SECURITY DEFINER, search_path y grants.
-- La acción masiva afecta en un solo UPDATE todas las filas de Bandeja del
-- usuario autenticado, incluidas las que ya estaban leídas.
create or replace function public.marcar_todas_mis_notificaciones_leidas()
returns integer
language plpgsql
security definer
set search_path=pg_catalog,pg_temp
as $function$
declare
  v_usuario integer;
  v_total integer;
begin
  if not private.usuario_tiene_permiso_codigo('NOTIFICACIONES_CONSULTAR') then
    raise exception using
      errcode='42501',
      message='No tienes permiso para gestionar tus notificaciones';
  end if;

  select id_usuario
  into v_usuario
  from public.usuarios
  where uuid_usuario=auth.uid()
    and id_estado=1;

  update public.notificaciones_usuario
  set fecha_leida=coalesce(fecha_leida,now()),
      fecha_archivada=coalesce(fecha_archivada,now())
  where id_usuario=v_usuario
    and fecha_archivada is null;

  get diagnostics v_total=row_count;
  return v_total;
end;
$function$;
