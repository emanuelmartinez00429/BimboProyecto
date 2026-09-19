-- Ejecutar con conexión administrativa. Todo cambio de prueba se revierte.
begin;
select set_config('request.jwt.claim.sub', u.uuid_usuario::text, true)
from public.usuarios u join public.roles r on r.id_rol=u.id_rol
where u.id_estado=1 and r.es_sistema order by u.id_usuario limit 1;
set local role authenticated;
do $$
declare
 v_producto public.productos%rowtype;
 v_solicitud uuid := gen_random_uuid();
 v_resultado jsonb; v_reintento jsonb; v_rol jsonb;
 v_reporte integer; v_usuario integer; v_antes bigint; v_despues bigint; v_max integer;
 v_texto text := 'Origen: Pesajes; Camiones: 2; Productos: 5; Total neto (kg): 12.5';
begin
 select id_usuario into strict v_usuario from public.usuarios where uuid_usuario=auth.uid() and id_estado=1;
 select coalesce(max(id_bitacora),0) into v_max from public.bitacora;
 select * into strict v_producto from public.productos where id_estado=1 order by id_producto limit 1;

 v_resultado := public.actualizar_producto_seguro(
  v_producto.id_producto,v_producto.codigo_producto,left(v_producto.nombre_producto,170)||' QA texto',
  v_producto.id_presentacion,v_producto.id_fabricante,v_producto.id_unidad,
  v_producto.peso_teorico,v_producto.peso_tara,v_producto.id_categoria,v_producto.contenido,
  v_producto.id_pais,v_producto.precio_por_kg,v_solicitud);
 if not (v_resultado->>'hubo_cambios')::boolean then raise exception 'La prueba debe modificar el producto'; end if;
 select count(*) into v_antes from public.bitacora;
 v_reintento := public.actualizar_producto_seguro(
  v_producto.id_producto,v_producto.codigo_producto,left(v_producto.nombre_producto,170)||' QA texto',
  v_producto.id_presentacion,v_producto.id_fabricante,v_producto.id_unidad,
  v_producto.peso_teorico,v_producto.peso_tara,v_producto.id_categoria,v_producto.contenido,
  v_producto.id_pais,v_producto.precio_por_kg,v_solicitud);
 select count(*) into v_despues from public.bitacora;
 if v_resultado<>v_reintento or v_antes<>v_despues then raise exception 'Reintento duplicó auditoría'; end if;
 if not exists(select 1 from public.bitacora where id_solicitud=v_solicitud and estado_actual like '%QA texto%') then
  raise exception 'Falta texto legible de la RPC de productos';
 end if;
 perform public.cambiar_estado_producto_seguro(v_producto.id_producto,2,gen_random_uuid());
 v_rol := public.crear_rol_seguro('QA texto '||left(gen_random_uuid()::text,8),gen_random_uuid());
 perform public.actualizar_rol_seguro((v_rol->>'id_rol')::integer,'QA texto editado '||left(gen_random_uuid()::text,8),gen_random_uuid());
 perform public.reemplazar_permisos_rol_seguro((v_rol->>'id_rol')::integer,
   array(select id_accion from public.acciones order by id_accion limit 2),gen_random_uuid());
 perform public.cambiar_estado_rol_seguro((v_rol->>'id_rol')::integer,2,gen_random_uuid());

 v_reporte := public.ingresar_reporte_tabla_bitacora('QA texto','PDF','Prueba reversible',
   current_date,current_date,v_texto,v_usuario);
 if (select parametros_reporte from public.reporteria where id_reporte=v_reporte) is distinct from v_texto then
  raise exception 'El reporte no conservó exactamente los parámetros textuales';
 end if;
 if exists(select 1 from public.bitacora where id_bitacora>v_max and
    (left(ltrim(estado_actual),1) in ('{','[') or left(ltrim(estado_anterior),1) in ('{','[')
     or left(ltrim(campo_extra),1) in ('{','['))) then
  raise exception 'Una operación sigue guardando JSON en la bitácora';
 end if;
 if not exists(select 1 from public.bitacora where id_bitacora>v_max and estado_actual like '%Estado: Inactivo%') then
  raise exception 'Falta descripción semántica del estado';
 end if;
end $$;
rollback;

