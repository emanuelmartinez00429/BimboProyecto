create or replace function private.bitacora_pesaje(
    p_id_usuario integer,p_anterior text,p_actual text,p_campo text,p_extra text,
    p_id_accion integer,p_tabla text,p_id_registro integer,p_id_solicitud uuid
) returns void language plpgsql security definer set search_path = pg_catalog, pg_temp
as $function$
declare
    v_anterior text := p_anterior;
    v_actual text := p_actual;
    v_campo text := p_campo;
    v_extra text := p_extra;
    v_detalle record;
begin
    if p_tabla = 'movimientos' and p_campo = 'Estado de recepcion' then
        v_anterior := case when p_anterior='Estado 7' then 'Recepción abierta' when p_anterior='Estado 8' then 'Recepción cerrada' when p_anterior='Estado 9' then 'Recepción anulada' else p_anterior end;
        v_actual := case when p_actual='Estado 7' then 'Recepción abierta' when p_actual='Estado 8' then 'Recepción cerrada' when p_actual='Estado 9' then 'Recepción anulada' else p_actual end;
    elsif p_tabla = 'movimiento_productos' and p_campo = 'Estado de producto' then
        v_anterior := case when p_anterior='Estado 7' then 'Producto abierto' when p_anterior='Estado 8' then 'Producto cerrado' when p_anterior='Estado 9' then 'Producto anulado' else p_anterior end;
        v_actual := case when p_actual='Estado 7' then 'Producto abierto' when p_actual='Estado 8' then 'Producto cerrado' when p_actual='Estado 9' then 'Producto anulado' else p_actual end;
    elsif p_tabla = 'entradas_producto' and p_campo = 'Estado del pesaje' then
        v_anterior := case when p_anterior in ('Estado Activo (1)','Estado 1') then 'Pesaje activo' when p_anterior in ('Estado Anulado (9)','Estado 9') then 'Pesaje anulado' else p_anterior end;
        v_actual := case when p_actual in ('Estado Activo (1)','Estado 1') then 'Pesaje activo' when p_actual in ('Estado Anulado (9)','Estado 9') then 'Pesaje anulado' else p_actual end;
    end if;

    if p_tabla = 'entradas_producto' and p_campo = 'Registro de pesaje' then
        select pr.codigo_producto,pr.nombre_producto,pv.nombre_proveedor,m.placa_vehiculo,
               e.peso_bruto,e.peso_tara_individual,e.peso_tara_extra,e.peso_tara_total,e.peso_neto,e.observaciones,
               m.id_movimiento,mp.id_mov_producto
          into v_detalle
          from public.entradas_producto e
          join public.movimiento_productos mp on mp.id_mov_producto=e.id_mov_producto
          join public.movimientos m on m.id_movimiento=mp.id_movimiento
          join public.productos pr on pr.id_producto=e.id_producto
          join public.proveedores pv on pv.id_proveedor=m.id_proveedor
         where e.id_pesaje=p_id_registro;
        if found then
            v_campo := 'Ingreso de pesaje de producto';
            v_actual := format('Producto: %s - %s; proveedor: %s; placa: %s; peso bruto: %s kg; tara total: %s kg (empaque: %s kg + extra: %s kg); peso neto recibido: %s kg',v_detalle.codigo_producto,v_detalle.nombre_producto,v_detalle.nombre_proveedor,coalesce(v_detalle.placa_vehiculo,'Sin placa'),v_detalle.peso_bruto,v_detalle.peso_tara_total,v_detalle.peso_tara_individual,v_detalle.peso_tara_extra,v_detalle.peso_neto);
            v_extra := format('Pesaje %s; recepción %s; producto de recepción %s; observaciones: %s',p_id_registro,v_detalle.id_movimiento,v_detalle.id_mov_producto,coalesce(nullif(v_detalle.observaciones,''),'Sin observaciones'));
        end if;
    end if;

    insert into public.bitacora(id_usuario,estado_anterior,estado_actual,campo_afectado,campo_extra,id_accion,id_modulo,tabla_afectada,id_registro_afectado,id_solicitud)
    values (p_id_usuario,left(coalesce(v_anterior,'Sin registro'),500),left(coalesce(v_actual,'Sin registro'),500),left(coalesce(v_campo,'Sin registro'),500),left(v_extra,500),p_id_accion,3,left(p_tabla,100),p_id_registro,p_id_solicitud);
end;
$function$;

revoke all on function private.bitacora_pesaje(integer,text,text,text,text,integer,text,integer,uuid)
from public,anon,authenticated;
