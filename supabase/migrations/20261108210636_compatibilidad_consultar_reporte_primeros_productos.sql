-- Compatibilidad transitoria: la version 1.0.3, ya distribuida, llama a
-- consultar_reporte_primeros_productos. Se mantiene como envoltorio delgado del reporte
-- nuevo (mismas 10 filas, mismo orden) hasta que el parque actualice a la version que usa
-- consultar_reporte_productos. Eliminar en una migracion posterior.
create or replace function public.consultar_reporte_primeros_productos(p_usuario_consultando integer)
returns table(id_producto integer, codigo text, nombre text, categoria text, proveedor text, estado text)
language sql
set search_path to ''
as $function$
  select * from public.consultar_reporte_productos(null::integer, p_usuario_consultando) limit 10;
$function$;

revoke all on function public.consultar_reporte_primeros_productos(integer) from public, anon;
grant execute on function public.consultar_reporte_primeros_productos(integer) to authenticated, service_role;
