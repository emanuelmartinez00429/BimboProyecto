-- Auditoría legible y parámetros de reportes en texto.
-- Versión asignada por Supabase MCP: 20260918192751.
-- Forward-only. No actualiza ni elimina filas históricas de public.bitacora.
create or replace function private.etiqueta_auditoria(p_clave text)
returns text language sql immutable set search_path = pg_catalog, pg_temp
as $$
select coalesce(
 '{"id_estado":"Estado","estado_categoria":"Estado","nombre_producto":"Producto","codigo_producto":"Código","nombre_proveedor":"Proveedor","nombre_fabricante":"Fabricante","nombre_categoria":"Categoría","nombre_presentacion":"Presentación","nombre_rol":"Rol","alias_usuario":"Usuario","peso_tara":"Tara (kg)","peso_teorico":"Peso teórico (kg)","precio_por_kg":"Precio por kg","acciones":"Referencias de permisos","created_at":"Creado","updated_at":"Actualizado","es_sistema":"Rol del sistema","rtn_proveedor":"RTN","correo_proveedor":"Correo","telefono_proveedor":"Teléfono","direccion_proveedor":"Dirección","descripcion_categoria":"Descripción","descripcion_presentacion":"Descripción","descripcion_fabricante":"Descripción","uuid_usuario":"Referencia de autenticación","ids_movimientos":"Referencias de recepciones","ids_registros":"Referencias de registros","ids_bitacora":"Referencias de bitácora","cantidad_camiones":"Camiones","cantidad_productos":"Productos","cantidad_registros":"Cantidad de registros","total_neto":"Total neto (kg)","total_manifestado":"Total manifestado (kg)","diferencia_kg":"Diferencia (kg)","fecha_desde":"Desde","fecha_hasta":"Hasta"}'::jsonb ->> p_clave,
 case when p_clave like 'id\_%' escape '\' then 'Referencia de ' || replace(substr(p_clave,4),'_',' ')
 else upper(left(p_clave,1)) || replace(substr(p_clave,2),'_',' ') end)
$$;

create or replace function private.valor_auditoria(p_valor jsonb, p_tabla text, p_clave text default null)
returns text language plpgsql immutable set search_path = pg_catalog, pg_temp
as $$
declare v_texto text; v_resultado text;
begin
 if p_valor is null or jsonb_typeof(p_valor)='null' then return 'Sin dato'; end if;
 if jsonb_typeof(p_valor)='object' then
   select string_agg(private.etiqueta_auditoria(key) || ': ' ||
      private.valor_auditoria(value,p_tabla,key), '; '
      order by case when key like 'nombre\_%' escape '\' then 0 else 1 end, key collate "C")
     into v_resultado from jsonb_each(p_valor)
     where key not like 'busqueda\_%' escape '\';
   return coalesce(v_resultado,'Sin registro');
 elsif jsonb_typeof(p_valor)='array' then
   select string_agg(private.valor_auditoria(value,p_tabla,p_clave),', ' order by ord)
     into v_resultado from jsonb_array_elements(p_valor) with ordinality a(value,ord);
   return coalesce(v_resultado,'Ninguno');
 end if;
 v_texto := p_valor #>> '{}';
 if p_clave='id_estado' then
   return case
     when p_tabla='movimientos' and v_texto='7' then 'Recepción abierta'
     when p_tabla='movimientos' and v_texto='8' then 'Recepción cerrada'
     when p_tabla='movimientos' and v_texto='9' then 'Recepción anulada'
     when p_tabla='movimiento_productos' and v_texto='7' then 'Producto abierto'
     when p_tabla='movimiento_productos' and v_texto='8' then 'Producto cerrado'
     when p_tabla='movimiento_productos' and v_texto='9' then 'Producto anulado'
     when p_tabla='entradas_producto' and v_texto='1' then 'Pesaje activo'
     when p_tabla='entradas_producto' and v_texto='9' then 'Pesaje anulado'
     when v_texto='1' then 'Activo' when v_texto='2' then 'Inactivo'
     else format('Estado sin descripción (referencia %s)',v_texto) end;
 elsif p_clave='estado_categoria' then
   return case when v_texto='true' then 'Activa' else 'Inactiva' end;
 elsif jsonb_typeof(p_valor)='boolean' then
   return case when v_texto='true' then 'Sí' else 'No' end;
 elsif p_tabla='reporteria' and p_clave in ('columnas','origen','reporte','orden') then
   return replace(v_texto,'_',' ');
 end if;
 return v_texto;
end;
$$;

create or replace function private.texto_auditoria(p_texto text, p_tabla text default null)
returns text language plpgsql immutable set search_path = pg_catalog, pg_temp
as $$
begin
 if p_texto is null or left(ltrim(p_texto),1) not in ('{','[') then return p_texto; end if;
 begin
   return private.valor_auditoria(p_texto::jsonb,p_tabla);
 exception when invalid_text_representation then
   raise exception 'El detalle de auditoría estructurado está incompleto' using errcode='22023';
 end;
end;
$$;

create or replace function private.resumir_auditoria(p_texto text, p_tabla text)
returns text language sql immutable set search_path = pg_catalog, pg_temp
as $$
select case when length(t)>500 then left(t,486)||' … (resumen)' else t end
from (select private.texto_auditoria(p_texto,p_tabla) t) s
$$;

-- La normalización también cubre emisores que no pasan por el helper RBAC.
create or replace function private.normalizar_bitacora_texto()
returns trigger language plpgsql security definer set search_path = pg_catalog, pg_temp
as $$
begin
 new.estado_anterior := private.resumir_auditoria(new.estado_anterior,new.tabla_afectada);
 new.estado_actual := private.resumir_auditoria(new.estado_actual,new.tabla_afectada);
 new.campo_extra := private.resumir_auditoria(new.campo_extra,new.tabla_afectada);
 return new;
end;
$$;
create trigger trg_bitacora_texto before insert on public.bitacora
for each row execute function private.normalizar_bitacora_texto();

CREATE OR REPLACE FUNCTION private.registrar_auditoria_rbac(p_nombre_accion text, p_estado_anterior text, p_estado_actual text, p_campo_afectado text, p_tabla_afectada text, p_id_registro integer, p_campo_extra text DEFAULT NULL::text)
 RETURNS void
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'pg_catalog', 'public', 'auth'
AS $function$
declare
  v_id_usuario integer;
  v_id_accion integer;
  v_id_modulo integer;
begin
  p_estado_anterior := private.texto_auditoria(p_estado_anterior,p_tabla_afectada);
  p_estado_actual := private.texto_auditoria(p_estado_actual,p_tabla_afectada);
  p_campo_extra := private.texto_auditoria(p_campo_extra,p_tabla_afectada);
  select u.id_usuario into v_id_usuario
  from public.usuarios u
  where u.uuid_usuario = auth.uid()
    and u.id_estado = 1;

  if v_id_usuario is null then
    raise exception using errcode = '42501',
      message = 'No existe un usuario activo asociado a la sesion.';
  end if;

  select a.id_accion, a.id_modulo
    into v_id_accion, v_id_modulo
  from public.acciones a
  where a.nombre_accion = p_nombre_accion
  limit 1;

  if v_id_accion is null then
    raise exception 'No existe la accion de auditoria: %', p_nombre_accion;
  end if;

  insert into public.bitacora (
    id_usuario, estado_anterior, estado_actual,
    campo_afectado, campo_extra, id_accion, id_modulo,
    tabla_afectada, id_registro_afectado, fecha_hora
  ) values (
    v_id_usuario,
    left(coalesce(p_estado_anterior, ''), 500),
    left(coalesce(p_estado_actual, ''), 500),
    left(coalesce(p_campo_afectado, ''), 500),
    left(p_campo_extra, 500),
    v_id_accion,
    v_id_modulo,
    left(coalesce(p_tabla_afectada, ''), 100),
    p_id_registro,
    now()
  );
end;
$function$
;
CREATE OR REPLACE FUNCTION private.registrar_auditoria_rbac(p_codigo_accion text, p_estado_anterior text, p_estado_actual text, p_campo_afectado text, p_tabla_afectada text, p_id_registro integer, p_campo_extra text, p_id_solicitud uuid)
 RETURNS void
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO 'pg_catalog', 'pg_temp'
AS $function$
declare v_usuario integer; v_accion integer; v_modulo integer;
begin
  p_estado_anterior := private.texto_auditoria(p_estado_anterior,p_tabla_afectada);
  p_estado_actual := private.texto_auditoria(p_estado_actual,p_tabla_afectada);
  p_campo_extra := private.texto_auditoria(p_campo_extra,p_tabla_afectada);
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
$function$
;
CREATE OR REPLACE FUNCTION public.log_upd_producto()
 RETURNS trigger
 LANGUAGE plpgsql
 SET search_path TO 'public', 'pg_temp'
AS $function$
declare
    v_id_usuario       integer;
    v_id_rol           integer;
    v_campos_afectados text;
    v_estado_anterior  jsonb;
    v_estado_actual    jsonb;
begin
    select
        u.id_usuario,
        u.id_rol
    into
        v_id_usuario,
        v_id_rol
    from public.usuarios u
    where u.uuid_usuario = (select auth.uid())
      and u.id_estado = 1;

    if v_id_usuario is null then
        raise exception
            'No se pudo identificar al usuario que modificó el producto'
            using errcode = '42501';
    end if;

    if not exists (
        select 1
        from public.acciones_roles ar
        where ar.id_rol = v_id_rol
          and ar.id_accion = 2
          and ar.id_estado = 1
    ) then
        raise exception
            'El usuario no tiene permiso para modificar productos'
            using errcode = '42501';
    end if;

    v_estado_anterior :=
        to_jsonb(old) - array['updated_at', 'busqueda_producto'];

    v_estado_actual :=
        to_jsonb(new) - array['updated_at', 'busqueda_producto'];

    if v_estado_anterior is not distinct from v_estado_actual then
        return new;
    end if;

    select string_agg(claves.campo, ', ' order by claves.campo)
    into v_campos_afectados
    from jsonb_object_keys(
        v_estado_anterior || v_estado_actual
    ) as claves(campo)
    where v_estado_anterior -> claves.campo
          is distinct from
          v_estado_actual -> claves.campo;

    insert into public.bitacora (
        id_usuario,
        estado_anterior,
        estado_actual,
        campo_afectado,
        campo_extra,
        id_accion,
        id_modulo,
        tabla_afectada,
        id_registro_afectado,
        fecha_hora
    )
    values (
        v_id_usuario,
        private.resumir_auditoria(v_estado_anterior::text,'productos'),
        private.resumir_auditoria(v_estado_actual::text,'productos'),
        v_campos_afectados,
        format(
            'Producto: %s; código: %s',
            new.nombre_producto,
            new.codigo_producto
        ),
        2,
        1,
        'productos',
        new.id_producto,
        current_timestamp
    );

    return new;
end;
$function$
;
-- Conversión de los parámetros históricos; la bitácora permanece append-only.
alter table public.reporteria alter column parametros_reporte type text
 using private.texto_auditoria(parametros_reporte::text,'reporteria');

drop function public.ingresar_reporte_tabla_bitacora(character varying,character varying,text,date,date,jsonb,integer);
CREATE OR REPLACE FUNCTION public.ingresar_reporte_tabla_bitacora(p_nombre_reporte character varying, p_tipo_reporte character varying, p_descripcion text, p_fecha_desde date, p_fecha_hasta date, p_parametros_reporte text, p_usuario_ingresando integer)
 RETURNS integer
 LANGUAGE plpgsql
 SET search_path TO 'public', 'pg_temp'
AS $function$
declare
    v_id_reporte integer;
    v_id_rol integer;
begin
    p_parametros_reporte := private.texto_auditoria(p_parametros_reporte,'reporteria');
    select u.id_rol
      into v_id_rol
      from public.usuarios u
     where u.id_usuario = p_usuario_ingresando
       and u.uuid_usuario = (select auth.uid())
       and u.id_estado = 1;

    if v_id_rol is null then
        raise exception
            'El usuario indicado no corresponde a la sesión activa, no existe o está inactivo'
            using errcode = '42501';
    end if;

    if not exists (
        select 1
          from public.acciones_roles ar
         where ar.id_rol = v_id_rol
           and ar.id_accion = 19
           and ar.id_estado = 1
    ) then
        raise exception
            'El usuario no tiene permiso para generar reportes'
            using errcode = '42501';
    end if;

    if nullif(trim(p_nombre_reporte), '') is null then
        raise exception
            'El nombre del reporte es obligatorio'
            using errcode = '22023';
    end if;

    if nullif(trim(p_tipo_reporte), '') is null then
        raise exception
            'El tipo de reporte es obligatorio'
            using errcode = '22023';
    end if;

    if p_fecha_desde is not null
       and p_fecha_hasta is not null
       and p_fecha_desde > p_fecha_hasta then
        raise exception
            'La fecha inicial no puede ser posterior a la fecha final'
            using errcode = '22007';
    end if;

    insert into public.reporteria (
        nombre_reporte,
        tipo_reporte,
        descripcion,
        fecha_generado,
        fecha_desde,
        fecha_hasta,
        id_usuario,
        parametros_reporte
    )
    values (
        trim(p_nombre_reporte),
        trim(p_tipo_reporte),
        nullif(trim(p_descripcion), ''),
        current_timestamp,
        p_fecha_desde,
        p_fecha_hasta,
        p_usuario_ingresando,
        p_parametros_reporte
    )
    returning id_reporte into v_id_reporte;

    insert into public.bitacora (
        id_usuario,
        estado_anterior,
        estado_actual,
        campo_afectado,
        campo_extra,
        id_accion,
        id_modulo,
        tabla_afectada,
        id_registro_afectado,
        fecha_hora
    )
    values (
        p_usuario_ingresando,
        'N/A',
        concat(
            'Nuevo reporte generado: ',
            trim(p_nombre_reporte),
            ' - ',
            trim(p_tipo_reporte)
        ),
        'Reporte nuevo',
        concat(
            'Periodo: ',
            coalesce(p_fecha_desde::text, 'Sin fecha inicial'),
            ' - ',
            coalesce(p_fecha_hasta::text, 'Sin fecha final')
        ),
        19,
        5,
        'reporteria',
        v_id_reporte,
        current_timestamp
    );

    return v_id_reporte;
end;
$function$
;
revoke all on function public.ingresar_reporte_tabla_bitacora(character varying,character varying,text,date,date,text,integer) from public, anon;
grant execute on function public.ingresar_reporte_tabla_bitacora(character varying,character varying,text,date,date,text,integer) to authenticated, service_role;
revoke all on function private.etiqueta_auditoria(text), private.valor_auditoria(jsonb,text,text),
 private.texto_auditoria(text,text), private.resumir_auditoria(text,text), private.normalizar_bitacora_texto() from public, anon, authenticated;
-- El trigger legacy y la RPC de reportes son SECURITY INVOKER: requieren los helpers puros.
grant execute on function private.etiqueta_auditoria(text), private.valor_auditoria(jsonb,text,text),
 private.texto_auditoria(text,text), private.resumir_auditoria(text,text) to authenticated, service_role;
notify pgrst, 'reload schema';

-- Assertions executed atomically with the migration.
do $$
begin
 if private.texto_auditoria('{"nombre_producto":"Café","id_estado":1,"peso_tara":0.25}', 'productos')
    <> 'Producto: Café; Estado: Activo; Tara (kg): 0.25' then
   raise exception 'Falló el contrato de texto legible para productos';
 end if;
 if private.texto_auditoria('{"acciones":[1,2]}','acciones_roles') <> 'Referencias de permisos: 1, 2'
 or private.texto_auditoria('{}',null) <> 'Sin registro'
 or private.texto_auditoria('[]',null) <> 'Ninguno'
 or private.texto_auditoria('{"dato":null}',null) <> 'Dato: Sin dato'
 or private.texto_auditoria('{"id_estado":9}','entradas_producto') <> 'Estado: Pesaje anulado'
 or private.texto_auditoria('Recepción abierta',null) <> 'Recepción abierta'
 or length(private.resumir_auditoria(jsonb_build_object('nombre_producto',repeat('á',600))::text,'productos')) > 500 then
   raise exception 'Falló el contrato de normalización de auditoría';
 end if;
end $$;
