-- Reporte de catalogo completo de productos, filtrable por categoria.
-- Reemplaza public.consultar_reporte_primeros_productos, que devolvia 10 filas fijas
-- (order by id_producto limit 10) sin admitir filtros.
-- Forward-only. El drop es obligatorio porque create or replace no puede cambiar la
-- firma de la funcion; por eso se vuelve a otorgar EXECUTE explicito.
drop function if exists public.consultar_reporte_primeros_productos(integer);

create or replace function public.consultar_reporte_productos(
    p_id_categoria integer,
    p_usuario_consultando integer)
returns table(id_producto integer, codigo text, nombre text, categoria text, proveedor text, estado text)
language plpgsql
set search_path to ''
as $function$
begin
 if not exists(select 1 from public.usuarios u where u.id_usuario=p_usuario_consultando and u.uuid_usuario=auth.uid() and u.id_estado=1)
    or not private.usuario_tiene_permiso('Consultar Reporte') then raise exception 'No autorizado para consultar reportes'; end if;
 if p_id_categoria is not null and not exists(
    select 1 from public.categoria c where c.id_categoria=p_id_categoria and c.estado_categoria=true)
 then raise exception 'La categoria seleccionada ya no esta activa'; end if;
 return query select p.id_producto,p.codigo_producto::text,p.nombre_producto::text,coalesce(c.nombre_categoria,'Sin categoria')::text,
        coalesce(pr.nombre_proveedor,'Sin proveedor')::text,eg.tipo_estado::text
 from public.productos p left join public.categoria c on c.id_categoria=p.id_categoria
 left join public.fabricante f on f.id_fabricante=p.id_fabricante left join public.proveedores pr on pr.id_proveedor=f.id_proveedor
 join public.estado_general eg on eg.id_estado=p.id_estado
 where p.id_estado=1 and (p_id_categoria is null or p.id_categoria=p_id_categoria)
 order by p.id_producto asc;
end $function$;

grant execute on function public.consultar_reporte_productos(integer, integer) to authenticated, service_role;
