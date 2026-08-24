-- RPC auditadas e idempotentes del modulo Pesaje.
-- Los privilegios DML directos se conservan durante la migracion del cliente WPF.

create or replace function private.preparar_solicitud_pesaje(
    p_id_solicitud uuid,
    p_id_operacion uuid,
    p_nombre_rpc text,
    p_permiso text,
    p_parametros jsonb
) returns jsonb
language plpgsql
security definer
set search_path = pg_catalog, pg_temp
as $function$
declare
    v_auth_uid uuid := auth.uid();
    v_id_usuario integer;
    v_id_accion integer;
    v_hash text;
    v_insertadas integer;
    v_existente private.solicitudes_rpc%rowtype;
begin
    if p_id_solicitud is null then
        raise exception using errcode = '22004', message = 'id_solicitud es obligatorio';
    end if;
    if v_auth_uid is null or not exists (select 1 from auth.users au where au.id = v_auth_uid) then
        raise exception using errcode = '28000', message = 'La sesion autenticada no existe';
    end if;

    select u.id_usuario
      into v_id_usuario
      from public.usuarios u
     where u.uuid_usuario = v_auth_uid and u.id_estado = 1;
    if v_id_usuario is null then
        raise exception using errcode = '28000', message = 'El usuario del sistema no existe o esta inactivo';
    end if;

    select a.id_accion
      into v_id_accion
      from public.usuarios u
      join public.acciones_roles ar on ar.id_rol = u.id_rol and ar.id_estado = 1
      join public.acciones a on a.id_accion = ar.id_accion
     where u.id_usuario = v_id_usuario
       and a.id_modulo = 3
       and a.nombre_accion = p_permiso;
    if v_id_accion is null then
        raise exception using errcode = '42501', message = format('Permiso RBAC requerido: %s', p_permiso);
    end if;

    v_hash := pg_catalog.encode(extensions.digest(convert_to(coalesce(p_parametros, '{}'::jsonb)::text, 'UTF8'), 'sha256'), 'hex');
    insert into private.solicitudes_rpc(id_solicitud,id_operacion,id_usuario,nombre_rpc,hash_parametros)
    values (p_id_solicitud,p_id_operacion,v_id_usuario,p_nombre_rpc,v_hash)
    on conflict (id_solicitud) do nothing;
    get diagnostics v_insertadas = row_count;

    if v_insertadas = 0 then
        select * into v_existente from private.solicitudes_rpc where id_solicitud = p_id_solicitud for update;
        if v_existente.id_usuario <> v_id_usuario
           or v_existente.nombre_rpc <> p_nombre_rpc
           or v_existente.hash_parametros <> v_hash
           or v_existente.id_operacion is distinct from p_id_operacion then
            raise exception using errcode = '22023', message = 'id_solicitud ya fue utilizado con otro usuario, RPC o parametros';
        end if;
        if v_existente.estado = 'COMPLETADA' then
            return jsonb_build_object('es_reintento',true,'resultado',v_existente.resultado);
        end if;
        raise exception using errcode = '55P03', message = 'La solicitud aun se encuentra en proceso';
    end if;

    return jsonb_build_object('es_reintento',false,'id_usuario',v_id_usuario,'id_accion',v_id_accion,'id_modulo',3);
end;
$function$;

create or replace function private.completar_solicitud_pesaje(p_id_solicitud uuid,p_resultado jsonb)
returns jsonb language plpgsql security definer set search_path = pg_catalog, pg_temp
as $function$
declare v_filas integer;
begin
    update private.solicitudes_rpc
       set estado='COMPLETADA',resultado=p_resultado,fecha_completada=clock_timestamp()
     where id_solicitud=p_id_solicitud and estado='EN_PROCESO';
    get diagnostics v_filas = row_count;
    if v_filas <> 1 then raise exception 'No se pudo completar exactamente una solicitud RPC'; end if;
    return p_resultado;
end;
$function$;

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
        v_anterior := case
            when p_anterior = 'Estado 7' then 'Recepción abierta'
            when p_anterior = 'Estado 8' then 'Recepción cerrada'
            when p_anterior = 'Estado 9' then 'Recepción anulada'
            else p_anterior
        end;
        v_actual := case
            when p_actual = 'Estado 7' then 'Recepción abierta'
            when p_actual = 'Estado 8' then 'Recepción cerrada'
            when p_actual = 'Estado 9' then 'Recepción anulada'
            else p_actual
        end;
    elsif p_tabla = 'movimiento_productos' and p_campo = 'Estado de producto' then
        v_anterior := case
            when p_anterior = 'Estado 7' then 'Producto abierto'
            when p_anterior = 'Estado 8' then 'Producto cerrado'
            when p_anterior = 'Estado 9' then 'Producto anulado'
            else p_anterior
        end;
        v_actual := case
            when p_actual = 'Estado 7' then 'Producto abierto'
            when p_actual = 'Estado 8' then 'Producto cerrado'
            when p_actual = 'Estado 9' then 'Producto anulado'
            else p_actual
        end;
    elsif p_tabla = 'entradas_producto' and p_campo = 'Estado del pesaje' then
        v_anterior := case
            when p_anterior in ('Estado Activo (1)','Estado 1') then 'Pesaje activo'
            when p_anterior in ('Estado Anulado (9)','Estado 9') then 'Pesaje anulado'
            else p_anterior
        end;
        v_actual := case
            when p_actual in ('Estado Activo (1)','Estado 1') then 'Pesaje activo'
            when p_actual in ('Estado Anulado (9)','Estado 9') then 'Pesaje anulado'
            else p_actual
        end;
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
            v_actual := format(
                'Producto: %s - %s; proveedor: %s; placa: %s; peso bruto: %s kg; tara total: %s kg (empaque: %s kg + extra: %s kg); peso neto recibido: %s kg',
                v_detalle.codigo_producto,v_detalle.nombre_producto,v_detalle.nombre_proveedor,
                coalesce(v_detalle.placa_vehiculo,'Sin placa'),v_detalle.peso_bruto,v_detalle.peso_tara_total,
                v_detalle.peso_tara_individual,v_detalle.peso_tara_extra,v_detalle.peso_neto);
            v_extra := format(
                'Pesaje %s; recepción %s; producto de recepción %s; observaciones: %s',
                p_id_registro,v_detalle.id_movimiento,v_detalle.id_mov_producto,
                coalesce(nullif(v_detalle.observaciones,''),'Sin observaciones'));
        end if;
    end if;

    insert into public.bitacora(id_usuario,estado_anterior,estado_actual,campo_afectado,campo_extra,
      id_accion,id_modulo,tabla_afectada,id_registro_afectado,id_solicitud)
    values (p_id_usuario,left(coalesce(v_anterior,'Sin registro'),500),left(coalesce(v_actual,'Sin registro'),500),
      left(coalesce(v_campo,'Sin registro'),500),left(v_extra,500),p_id_accion,3,left(p_tabla,100),p_id_registro,p_id_solicitud);
end;
$function$;

create or replace function private.ejecutar_rpc_pesaje(
    p_nombre_rpc text,p_permiso text,p_id_solicitud uuid,p_id_operacion uuid,p_parametros jsonb
) returns jsonb language plpgsql security definer set search_path = pg_catalog, public, pg_temp
as $function$
declare
    v_ctx jsonb; v_resultado jsonb; v_old record; v_new record; v_filas integer;
    v_usuario integer; v_accion integer; v_id integer; v_id_mov integer; v_creado boolean := false;
    v_estado integer; v_ids integer[]; v_total numeric; v_base numeric; v_residuo numeric; v_i integer := 0; v_n integer;
begin
    v_ctx := private.preparar_solicitud_pesaje(p_id_solicitud,p_id_operacion,p_nombre_rpc,p_permiso,p_parametros);
    if (v_ctx->>'es_reintento')::boolean then return v_ctx->'resultado'; end if;
    v_usuario := (v_ctx->>'id_usuario')::integer; v_accion := (v_ctx->>'id_accion')::integer;

    if p_nombre_rpc = 'ingresar_movimiento_pesaje_tabla_bitacora' then
        if coalesce((p_parametros->>'id_proveedor')::integer,0)<=0 or nullif(btrim(p_parametros->>'placa_vehiculo'),'') is null then raise exception 'Proveedor y placa son obligatorios'; end if;
        if not exists(select 1 from public.proveedores p where p.id_proveedor=(p_parametros->>'id_proveedor')::integer and p.id_estado=1) then raise exception 'El proveedor no existe o esta inactivo'; end if;
        insert into public.movimientos(id_proveedor,placa_vehiculo,fecha_asignacion,id_usuario,id_estado,observaciones,peso_tara_extra)
        values((p_parametros->>'id_proveedor')::integer,btrim(p_parametros->>'placa_vehiculo'),(clock_timestamp() at time zone 'America/Tegucigalpa')::date,v_usuario,7,nullif(btrim(p_parametros->>'observaciones'),''),0)
        returning id_movimiento into v_id;
        perform private.bitacora_pesaje(v_usuario,'Sin registro',format('Recepcion %s abierta para proveedor %s, placa %s',v_id,p_parametros->>'id_proveedor',btrim(p_parametros->>'placa_vehiculo')),'Registro de recepcion','Estado: Abierto',v_accion,'movimientos',v_id,p_id_solicitud);
        v_resultado:=jsonb_build_object('id_movimiento',v_id,'id_estado',7);

    elsif p_nombre_rpc = 'ingresar_producto_recepcion_pesaje_tabla_bitacora' then
        if (p_parametros->>'peso_manifestado')::numeric<=0 or (p_parametros->>'bultos_teoricos')::integer<=0 then raise exception 'Peso manifestado y bultos deben ser positivos'; end if;
        if not exists(select 1 from public.productos p join public.fabricante f on f.id_fabricante=p.id_fabricante where p.id_producto=(p_parametros->>'id_producto')::integer and p.id_estado=1 and f.id_proveedor=(p_parametros->>'id_proveedor')::integer and f.id_estado=1) then raise exception 'El producto no pertenece al proveedor o esta inactivo'; end if;
        perform pg_advisory_xact_lock(hashtextextended(upper(btrim(p_parametros->>'placa_vehiculo'))||':'||(p_parametros->>'id_proveedor'),0));
        select m.id_movimiento into v_id_mov from public.movimientos m where m.id_proveedor=(p_parametros->>'id_proveedor')::integer and upper(btrim(m.placa_vehiculo))=upper(btrim(p_parametros->>'placa_vehiculo')) and m.id_estado=7 order by m.id_movimiento limit 1 for update;
        if v_id_mov is null then
            insert into public.movimientos(id_proveedor,placa_vehiculo,fecha_asignacion,id_usuario,id_estado,observaciones,peso_tara_extra)
            values((p_parametros->>'id_proveedor')::integer,btrim(p_parametros->>'placa_vehiculo'),(clock_timestamp() at time zone 'America/Tegucigalpa')::date,v_usuario,7,null,0) returning id_movimiento into v_id_mov;
            v_creado:=true;
            perform private.bitacora_pesaje(v_usuario,'Sin registro',format('Recepcion %s abierta automaticamente para proveedor %s, placa %s',v_id_mov,p_parametros->>'id_proveedor',btrim(p_parametros->>'placa_vehiculo')),'Registro de recepcion','Creada al agregar producto',v_accion,'movimientos',v_id_mov,p_id_solicitud);
        end if;
        insert into public.movimiento_productos(id_movimiento,id_producto,peso_manifestado,bultos_teoricos,id_estado,observaciones)
        values(v_id_mov,(p_parametros->>'id_producto')::integer,(p_parametros->>'peso_manifestado')::numeric,(p_parametros->>'bultos_teoricos')::integer,7,nullif(btrim(p_parametros->>'observaciones'),'')) returning id_mov_producto into v_id;
        perform private.bitacora_pesaje(v_usuario,'Sin registro',format('Producto %s agregado a recepcion %s; peso manifestado %s kg; bultos %s',p_parametros->>'id_producto',v_id_mov,p_parametros->>'peso_manifestado',p_parametros->>'bultos_teoricos'),'Registro de producto','Estado: Abierto',v_accion,'movimiento_productos',v_id,p_id_solicitud);
        v_resultado:=jsonb_build_object('id_movimiento',v_id_mov,'id_mov_producto',v_id,'movimiento_creado',v_creado);

    elsif p_nombre_rpc = 'ingresar_entrada_producto_pesaje_tabla_bitacora' then
        select mp.id_estado,mp.id_producto into v_estado,v_id from public.movimiento_productos mp where mp.id_mov_producto=(p_parametros->>'id_mov_producto')::integer for update;
        if not found or v_estado<>7 or v_id<>(p_parametros->>'id_producto')::integer then raise exception 'El producto de la recepcion no existe, no esta abierto o no coincide'; end if;
        if (p_parametros->>'peso_bruto')::numeric<=0 or coalesce((p_parametros->>'peso_tara_extra')::numeric,0)<0 then raise exception 'Los pesos enviados no son validos'; end if;
        insert into public.entradas_producto(fecha_entrada,hora_entrada,id_producto,peso_bruto,peso_tara_extra,numero_bultos_recibido,id_usuario,observaciones,id_estado,id_mov_producto)
        values((clock_timestamp() at time zone 'America/Tegucigalpa')::date,(clock_timestamp() at time zone 'America/Tegucigalpa')::time,(p_parametros->>'id_producto')::integer,(p_parametros->>'peso_bruto')::numeric,coalesce((p_parametros->>'peso_tara_extra')::numeric,0),null,v_usuario,nullif(btrim(p_parametros->>'observaciones'),''),1,(p_parametros->>'id_mov_producto')::integer)
        returning * into v_new;
        perform private.bitacora_pesaje(v_usuario,'Sin registro',format('Pesaje %s registrado; bruto %s kg; tara empaque %s kg; tara extra %s kg; neto %s kg',v_new.id_pesaje,v_new.peso_bruto,v_new.peso_tara_individual,v_new.peso_tara_extra,v_new.peso_neto),'Registro de pesaje','Estado: Activo',v_accion,'entradas_producto',v_new.id_pesaje,p_id_solicitud);
        v_resultado:=to_jsonb(v_new);

    elsif p_nombre_rpc = 'actualizar_movimiento_pesaje_tabla_bitacora' then
        select * into v_old from public.movimientos where id_movimiento=(p_parametros->>'id_movimiento')::integer for update;
        if not found then raise exception 'La recepcion no existe'; end if; if v_old.id_estado<>7 then raise exception 'Solo se puede modificar una recepcion abierta'; end if;
        if not exists(select 1 from public.proveedores p where p.id_proveedor=(p_parametros->>'id_proveedor')::integer and p.id_estado=1) then raise exception 'El proveedor no existe o esta inactivo'; end if;
        update public.movimientos set id_proveedor=(p_parametros->>'id_proveedor')::integer,placa_vehiculo=btrim(p_parametros->>'placa_vehiculo'),observaciones=nullif(btrim(p_parametros->>'observaciones'),'') where id_movimiento=v_old.id_movimiento returning * into v_new;
        get diagnostics v_filas=row_count; if v_filas<>1 then raise exception 'No se actualizo exactamente una recepcion'; end if;
        perform private.bitacora_pesaje(v_usuario,format('Proveedor %s; placa %s; observaciones %s',v_old.id_proveedor,coalesce(v_old.placa_vehiculo,''),coalesce(v_old.observaciones,'')),format('Proveedor %s; placa %s; observaciones %s',v_new.id_proveedor,coalesce(v_new.placa_vehiculo,''),coalesce(v_new.observaciones,'')),'Proveedor, placa y observaciones','Recepcion abierta',v_accion,'movimientos',v_new.id_movimiento,p_id_solicitud); v_resultado:=to_jsonb(v_new);

    elsif p_nombre_rpc = 'cambiar_estado_movimiento_pesaje_tabla_bitacora' then
        select * into v_old from public.movimientos where id_movimiento=(p_parametros->>'id_movimiento')::integer for update;
        v_estado:=(p_parametros->>'id_estado')::integer;
        if not found then raise exception 'La recepcion no existe'; end if; if v_old.id_estado<>7 or v_estado not in (8,9) then raise exception 'Transicion de estado de recepcion no permitida'; end if;
        if v_estado=8 and exists(select 1 from public.movimiento_productos mp where mp.id_movimiento=v_old.id_movimiento and mp.id_estado<>9 and mp.id_estado<>8) then raise exception 'Todos los productos deben estar cerrados antes de cerrar la recepcion'; end if;
        update public.movimientos set id_estado=v_estado where id_movimiento=v_old.id_movimiento returning * into v_new; get diagnostics v_filas=row_count; if v_filas<>1 then raise exception 'No se actualizo exactamente una recepcion'; end if;
        perform private.bitacora_pesaje(v_usuario,format('Estado %s',v_old.id_estado),format('Estado %s',v_new.id_estado),'Estado de recepcion',case when v_estado=8 then 'Recepcion cerrada' else 'Recepcion anulada' end,v_accion,'movimientos',v_new.id_movimiento,p_id_solicitud); v_resultado:=jsonb_build_object('id_movimiento',v_new.id_movimiento,'id_estado',v_new.id_estado);

    elsif p_nombre_rpc = 'actualizar_producto_movimiento_pesaje_tabla_bitacora' then
        select * into v_old from public.movimiento_productos where id_mov_producto=(p_parametros->>'id_mov_producto')::integer for update;
        if not found then raise exception 'El producto de la recepcion no existe'; end if; if v_old.id_estado<>7 then raise exception 'Solo se puede modificar un producto abierto'; end if;
        if (p_parametros->>'peso_manifestado')::numeric<=0 or (p_parametros->>'bultos_teoricos')::integer<=0 then raise exception 'Peso manifestado y bultos deben ser positivos'; end if;
        update public.movimiento_productos set peso_manifestado=(p_parametros->>'peso_manifestado')::numeric,bultos_teoricos=(p_parametros->>'bultos_teoricos')::integer,observaciones=nullif(btrim(p_parametros->>'observaciones'),'') where id_mov_producto=v_old.id_mov_producto returning * into v_new; get diagnostics v_filas=row_count; if v_filas<>1 then raise exception 'No se actualizo exactamente un producto'; end if;
        perform private.bitacora_pesaje(v_usuario,format('Peso %s kg; bultos %s; observaciones %s',v_old.peso_manifestado,v_old.bultos_teoricos,coalesce(v_old.observaciones,'')),format('Peso %s kg; bultos %s; observaciones %s',v_new.peso_manifestado,v_new.bultos_teoricos,coalesce(v_new.observaciones,'')),'Manifiesto y observaciones','Producto de recepcion abierto',v_accion,'movimiento_productos',v_new.id_mov_producto,p_id_solicitud); v_resultado:=to_jsonb(v_new);

    elsif p_nombre_rpc = 'cambiar_estado_producto_pesaje_tabla_bitacora' then
        select * into v_old from public.movimiento_productos where id_mov_producto=(p_parametros->>'id_mov_producto')::integer for update; v_estado:=(p_parametros->>'id_estado')::integer;
        if not found then raise exception 'El producto de la recepcion no existe'; end if;
        if not ((v_old.id_estado=7 and v_estado in (8,9)) or (v_old.id_estado=8 and v_estado in (7,9))) then raise exception 'Transicion de estado del producto no permitida'; end if;
        if v_estado=9 and exists(select 1 from public.entradas_producto e where e.id_mov_producto=v_old.id_mov_producto and e.id_estado<>9) then raise exception 'No se puede anular un producto con pesajes activos'; end if;
        update public.movimiento_productos set id_estado=v_estado where id_mov_producto=v_old.id_mov_producto returning * into v_new; get diagnostics v_filas=row_count; if v_filas<>1 then raise exception 'No se actualizo exactamente un producto'; end if;
        perform private.bitacora_pesaje(v_usuario,format('Estado %s',v_old.id_estado),format('Estado %s',v_new.id_estado),'Estado de producto','Cambio de estado validado',v_accion,'movimiento_productos',v_new.id_mov_producto,p_id_solicitud); v_resultado:=jsonb_build_object('id_mov_producto',v_new.id_mov_producto,'id_estado',v_new.id_estado);

    elsif p_nombre_rpc = 'actualizar_entrada_producto_pesaje_tabla_bitacora' then
        select e.* into v_old from public.entradas_producto e join public.movimiento_productos mp on mp.id_mov_producto=e.id_mov_producto where e.id_pesaje=(p_parametros->>'id_pesaje')::integer and mp.id_estado=7 for update of e;
        if not found then raise exception 'El pesaje no existe, esta anulado o su producto no esta abierto'; end if; if v_old.id_estado<>1 then raise exception 'Solo se puede modificar un pesaje activo'; end if;
        update public.entradas_producto set peso_bruto=(p_parametros->>'peso_bruto')::numeric,peso_tara_extra=coalesce((p_parametros->>'peso_tara_extra')::numeric,0),observaciones=nullif(btrim(p_parametros->>'observaciones'),'') where id_pesaje=v_old.id_pesaje returning * into v_new; get diagnostics v_filas=row_count; if v_filas<>1 then raise exception 'No se actualizo exactamente un pesaje'; end if;
        perform private.bitacora_pesaje(v_usuario,format('Bruto %s kg; tara extra %s kg; neto %s kg; observaciones %s',v_old.peso_bruto,v_old.peso_tara_extra,v_old.peso_neto,coalesce(v_old.observaciones,'')),format('Bruto %s kg; tara extra %s kg; neto %s kg; observaciones %s',v_new.peso_bruto,v_new.peso_tara_extra,v_new.peso_neto,coalesce(v_new.observaciones,'')),'Pesos y observaciones','Se conservaron identidad, fecha y usuario originales',v_accion,'entradas_producto',v_new.id_pesaje,p_id_solicitud); v_resultado:=to_jsonb(v_new);

    elsif p_nombre_rpc = 'cancelar_entrada_producto_pesaje_tabla_bitacora' then
        select e.* into v_old from public.entradas_producto e join public.movimiento_productos mp on mp.id_mov_producto=e.id_mov_producto where e.id_pesaje=(p_parametros->>'id_pesaje')::integer and mp.id_estado=7 for update of e;
        if not found or v_old.id_estado<>1 then raise exception 'El pesaje no existe, no esta activo o su producto no esta abierto'; end if;
        update public.entradas_producto set id_estado=9 where id_pesaje=v_old.id_pesaje returning * into v_new; get diagnostics v_filas=row_count; if v_filas<>1 then raise exception 'No se cancelo exactamente un pesaje'; end if;
        perform private.bitacora_pesaje(v_usuario,'Estado Activo (1)','Estado Anulado (9)','Estado del pesaje','Pesaje cancelado sin eliminar su historial',v_accion,'entradas_producto',v_new.id_pesaje,p_id_solicitud); v_resultado:=jsonb_build_object('id_pesaje',v_new.id_pesaje,'id_estado',9);

    elsif p_nombre_rpc = 'repartir_tara_extra_pesaje_tabla_bitacora' then
        select array_agg(distinct x::integer order by x::integer) into v_ids from jsonb_array_elements_text(p_parametros->'ids_pesaje') j(x);
        v_total:=(p_parametros->>'peso_tara_extra_total')::numeric; v_n:=coalesce(array_length(v_ids,1),0);
        if v_n=0 or v_total<0 then raise exception 'Debe indicar pesajes y una tara extra total no negativa'; end if;
        perform 1 from public.entradas_producto e where e.id_pesaje=any(v_ids) order by e.id_pesaje for update;
        if (select count(*) from public.entradas_producto e join public.movimiento_productos mp on mp.id_mov_producto=e.id_mov_producto where e.id_pesaje=any(v_ids) and e.id_estado=1 and mp.id_estado=7)<>v_n then raise exception 'Todos los pesajes deben existir, estar activos y pertenecer a productos abiertos'; end if;
        v_base:=trunc(v_total/v_n,3); v_residuo:=v_total-(v_base*(v_n-1));
        for v_old in select * from public.entradas_producto where id_pesaje=any(v_ids) order by id_pesaje loop
            v_i:=v_i+1; update public.entradas_producto set peso_tara_extra=case when v_i=v_n then v_residuo else v_base end where id_pesaje=v_old.id_pesaje returning * into v_new;
            perform private.bitacora_pesaje(v_usuario,format('Tara extra %s kg; neto %s kg',v_old.peso_tara_extra,v_old.peso_neto),format('Tara extra %s kg; neto %s kg',v_new.peso_tara_extra,v_new.peso_neto),'Tara extra del pesaje',format('Reparto de %s kg entre %s pesajes',v_total,v_n),v_accion,'entradas_producto',v_new.id_pesaje,p_id_solicitud);
        end loop;
        v_resultado:=jsonb_build_object('cantidad_actualizada',v_n,'ids_pesaje',to_jsonb(v_ids),'peso_tara_extra_total',v_total);
    else
        raise exception 'RPC de Pesaje no reconocida';
    end if;
    return private.completar_solicitud_pesaje(p_id_solicitud,v_resultado);
end;
$function$;

-- Envoltorios publicos con parametros tipados para PostgREST.
create or replace function public.ingresar_movimiento_pesaje_tabla_bitacora(p_id_proveedor integer,p_placa_vehiculo text,p_observaciones text,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('ingresar_movimiento_pesaje_tabla_bitacora','Registrar Entrada',$4,$5,jsonb_build_object('id_proveedor',$1,'placa_vehiculo',$2,'observaciones',$3))$$;
create or replace function public.ingresar_producto_recepcion_pesaje_tabla_bitacora(p_placa_vehiculo text,p_id_proveedor integer,p_id_producto integer,p_peso_manifestado numeric,p_bultos_teoricos integer,p_observaciones text,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('ingresar_producto_recepcion_pesaje_tabla_bitacora','Registrar Entrada',$7,$8,jsonb_build_object('placa_vehiculo',$1,'id_proveedor',$2,'id_producto',$3,'peso_manifestado',$4,'bultos_teoricos',$5,'observaciones',$6))$$;
create or replace function public.ingresar_entrada_producto_pesaje_tabla_bitacora(p_id_mov_producto integer,p_id_producto integer,p_peso_bruto numeric,p_peso_tara_extra numeric,p_observaciones text,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('ingresar_entrada_producto_pesaje_tabla_bitacora','Registrar Entrada',$6,$7,jsonb_build_object('id_mov_producto',$1,'id_producto',$2,'peso_bruto',$3,'peso_tara_extra',$4,'observaciones',$5))$$;
create or replace function public.actualizar_movimiento_pesaje_tabla_bitacora(p_id_movimiento integer,p_id_proveedor integer,p_placa_vehiculo text,p_observaciones text,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('actualizar_movimiento_pesaje_tabla_bitacora','Modificar Pesaje',$5,$6,jsonb_build_object('id_movimiento',$1,'id_proveedor',$2,'placa_vehiculo',$3,'observaciones',$4))$$;
create or replace function public.cambiar_estado_movimiento_pesaje_tabla_bitacora(p_id_movimiento integer,p_id_estado integer,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('cambiar_estado_movimiento_pesaje_tabla_bitacora',case when $2=8 then 'Completar Pesaje' else 'Cancelar Pesaje' end,$3,$4,jsonb_build_object('id_movimiento',$1,'id_estado',$2))$$;
create or replace function public.actualizar_producto_movimiento_pesaje_tabla_bitacora(p_id_mov_producto integer,p_peso_manifestado numeric,p_bultos_teoricos integer,p_observaciones text,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('actualizar_producto_movimiento_pesaje_tabla_bitacora','Modificar Pesaje',$5,$6,jsonb_build_object('id_mov_producto',$1,'peso_manifestado',$2,'bultos_teoricos',$3,'observaciones',$4))$$;
create or replace function public.cambiar_estado_producto_pesaje_tabla_bitacora(p_id_mov_producto integer,p_id_estado integer,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('cambiar_estado_producto_pesaje_tabla_bitacora',case when $2=8 then 'Completar Pesaje' when $2=9 then 'Cancelar Pesaje' else 'Modificar Pesaje' end,$3,$4,jsonb_build_object('id_mov_producto',$1,'id_estado',$2))$$;
create or replace function public.actualizar_entrada_producto_pesaje_tabla_bitacora(p_id_pesaje integer,p_peso_bruto numeric,p_peso_tara_extra numeric,p_observaciones text,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('actualizar_entrada_producto_pesaje_tabla_bitacora','Modificar Pesaje',$5,$6,jsonb_build_object('id_pesaje',$1,'peso_bruto',$2,'peso_tara_extra',$3,'observaciones',$4))$$;
create or replace function public.cancelar_entrada_producto_pesaje_tabla_bitacora(p_id_pesaje integer,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('cancelar_entrada_producto_pesaje_tabla_bitacora','Cancelar Pesaje',$2,$3,jsonb_build_object('id_pesaje',$1))$$;
create or replace function public.repartir_tara_extra_pesaje_tabla_bitacora(p_ids_pesaje integer[],p_peso_tara_extra_total numeric,p_id_solicitud uuid,p_id_operacion uuid default null) returns jsonb language sql security definer set search_path=pg_catalog,pg_temp as $$select private.ejecutar_rpc_pesaje('repartir_tara_extra_pesaje_tabla_bitacora','Modificar Pesaje',$3,$4,jsonb_build_object('ids_pesaje',to_jsonb($1),'peso_tara_extra_total',$2))$$;

revoke all on function private.preparar_solicitud_pesaje(uuid,uuid,text,text,jsonb) from public,anon,authenticated;
revoke all on function private.completar_solicitud_pesaje(uuid,jsonb) from public,anon,authenticated;
revoke all on function private.bitacora_pesaje(integer,text,text,text,text,integer,text,integer,uuid) from public,anon,authenticated;
revoke all on function private.ejecutar_rpc_pesaje(text,text,uuid,uuid,jsonb) from public,anon,authenticated;

do $do$
declare r record;
begin
  for r in select p.oid::regprocedure as firma from pg_proc p join pg_namespace n on n.oid=p.pronamespace where n.nspname='public' and p.proname in ('ingresar_movimiento_pesaje_tabla_bitacora','ingresar_producto_recepcion_pesaje_tabla_bitacora','ingresar_entrada_producto_pesaje_tabla_bitacora','actualizar_movimiento_pesaje_tabla_bitacora','cambiar_estado_movimiento_pesaje_tabla_bitacora','actualizar_producto_movimiento_pesaje_tabla_bitacora','cambiar_estado_producto_pesaje_tabla_bitacora','actualizar_entrada_producto_pesaje_tabla_bitacora','cancelar_entrada_producto_pesaje_tabla_bitacora','repartir_tara_extra_pesaje_tabla_bitacora') loop
    execute format('revoke all on function %s from public, anon',r.firma);
    execute format('grant execute on function %s to authenticated',r.firma);
  end loop;
end;
$do$;
