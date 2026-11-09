-- Cierra el ACL por defecto de Postgres en el reporte de catalogo de productos.
-- create function otorga EXECUTE a PUBLIC de forma implicita y los default privileges de
-- Supabase suman anon. El resto de las RPC de reportes solo admite authenticated y
-- service_role, asi que la funcion nueva queda alineada con ese contrato.
revoke all on function public.consultar_reporte_productos(integer, integer) from public, anon;
grant execute on function public.consultar_reporte_productos(integer, integer) to authenticated, service_role;
