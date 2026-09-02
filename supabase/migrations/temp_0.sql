CREATE OR REPLACE FUNCTION public.crear_proveedor_seguro(
    p_nombre_proveedor varchar,
    p_rtn_proveedor varchar,
    p_telefono_proveedor varchar,
    p_correo_proveedor varchar,
    p_direccion_proveedor text,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_id_proveedor int;
    v_id_estado int := 1;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_actual jsonb;
BEGIN
    IF NOT private.usuario_tiene_permiso_codigo('PROVEEDORES_CREAR') THEN
        RAISE EXCEPTION 'No tienes permiso para crear proveedores.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'nombre', p_nombre_proveedor,
        'rtn', p_rtn_proveedor,
        'telefono', p_telefono_proveedor,
        'correo', p_correo_proveedor,
        'direccion', p_direccion_proveedor
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_proveedor_seguro', 'PROVEEDORES_CREAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    INSERT INTO public.proveedores (
        nombre_proveedor, rtn_proveedor, telefono_proveedor, correo_proveedor, direccion_proveedor, id_estado
    ) VALUES (
        trim(p_nombre_proveedor),
        nullif(trim(p_rtn_proveedor), ''),
        nullif(trim(p_telefono_proveedor), ''),
        nullif(trim(p_correo_proveedor), ''),
        nullif(trim(p_direccion_proveedor), ''),
        v_id_estado
    ) RETURNING id_proveedor INTO v_id_proveedor;

    v_estado_actual := jsonb_build_object(
        'id_proveedor', v_id_proveedor,
        'nombre_proveedor', trim(p_nombre_proveedor),
        'rtn_proveedor', nullif(trim(p_rtn_proveedor), ''),
        'id_estado', v_id_estado
    );

    PERFORM private.registrar_auditoria_rbac(
        'PROVEEDORES_CREAR',
        '{}',
        v_estado_actual::text,
        'Proveedor nuevo',
        'proveedores',
        v_id_proveedor,
        'Registro creado mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'PROVEEDOR_CREADO',
        'Nuevo proveedor registrado',
        'Se ha registrado el proveedor ' || trim(p_nombre_proveedor),
        'informativa',
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer,
        'proveedores',
        v_id_proveedor,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_proveedor', v_id_proveedor, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


CREATE OR REPLACE FUNCTION public.actualizar_proveedor_seguro(
    p_id_proveedor int,
    p_nombre_proveedor varchar,
    p_rtn_proveedor varchar,
    p_telefono_proveedor varchar,
    p_correo_proveedor varchar,
    p_direccion_proveedor text,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_registro_actual record;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_anterior jsonb;
    v_estado_actual jsonb;
BEGIN
    IF NOT private.usuario_tiene_permiso_codigo('PROVEEDORES_MODIFICAR') THEN
        RAISE EXCEPTION 'No tienes permiso para modificar proveedores.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'id_proveedor', p_id_proveedor,
        'nombre', p_nombre_proveedor,
        'rtn', p_rtn_proveedor,
        'telefono', p_telefono_proveedor,
        'correo', p_correo_proveedor,
        'direccion', p_direccion_proveedor
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_proveedor_seguro', 'PROVEEDORES_MODIFICAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.proveedores WHERE id_proveedor = p_id_proveedor FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El proveedor no existe.' USING ERRCODE = '22023';
    END IF;

    IF v_registro_actual.nombre_proveedor IS NOT DISTINCT FROM trim(p_nombre_proveedor) AND
       v_registro_actual.rtn_proveedor IS NOT DISTINCT FROM nullif(trim(p_rtn_proveedor), '') AND
       v_registro_actual.telefono_proveedor IS NOT DISTINCT FROM nullif(trim(p_telefono_proveedor), '') AND
       v_registro_actual.correo_proveedor IS NOT DISTINCT FROM nullif(trim(p_correo_proveedor), '') AND
       v_registro_actual.direccion_proveedor IS NOT DISTINCT FROM nullif(trim(p_direccion_proveedor), '') THEN
        
        v_resultado := jsonb_build_object('id_proveedor', p_id_proveedor, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);

    UPDATE public.proveedores SET
        nombre_proveedor = trim(p_nombre_proveedor),
        rtn_proveedor = nullif(trim(p_rtn_proveedor), ''),
        telefono_proveedor = nullif(trim(p_telefono_proveedor), ''),
        correo_proveedor = nullif(trim(p_correo_proveedor), ''),
        direccion_proveedor = nullif(trim(p_direccion_proveedor), '')
    WHERE id_proveedor = p_id_proveedor;

    v_estado_actual := jsonb_build_object(
        'id_proveedor', p_id_proveedor,
        'nombre_proveedor', trim(p_nombre_proveedor),
        'rtn_proveedor', nullif(trim(p_rtn_proveedor), ''),
        'id_estado', v_registro_actual.id_estado
    );

    PERFORM private.registrar_auditoria_rbac(
        'PROVEEDORES_MODIFICAR',
        v_estado_anterior::text,
        v_estado_actual::text,
        'Datos del proveedor',
        'proveedores',
        p_id_proveedor,
        'Actualización mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'PROVEEDOR_MODIFICADO',
        'Proveedor modificado',
        'Se han actualizado los datos del proveedor ' || trim(p_nombre_proveedor),
        'informativa',
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer,
        'proveedores',
        p_id_proveedor,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_proveedor', p_id_proveedor, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


CREATE OR REPLACE FUNCTION public.cambiar_estado_proveedor_seguro(
    p_id_proveedor int,
    p_id_estado int,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_registro_actual record;
    v_codigo_accion text;
    
    v_codigo_notif text;
    v_titulo_notif text;
    v_mensaje_notif text;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_anterior jsonb;
    v_estado_actual jsonb;
BEGIN
    IF p_id_estado = 2 THEN
        v_codigo_accion := 'PROVEEDORES_ELIMINAR';

        v_codigo_notif := 'PROVEEDOR_DESACTIVADO';
        v_titulo_notif := 'Proveedor desactivado';
    ELSE
        v_codigo_accion := 'PROVEEDORES_MODIFICAR';

        v_codigo_notif := 'PROVEEDOR_ACTIVADO';
        v_titulo_notif := 'Proveedor activado';
    END IF;

    IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
        RAISE EXCEPTION 'No tienes permiso para cambiar el estado de proveedores.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object('id_proveedor', p_id_proveedor, 'id_estado', p_id_estado);
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_proveedor_seguro', v_codigo_accion, v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.proveedores WHERE id_proveedor = p_id_proveedor FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El proveedor no existe.' USING ERRCODE = '22023';
    END IF;

    IF v_registro_actual.id_estado = p_id_estado THEN
        v_resultado := jsonb_build_object('id_proveedor', p_id_proveedor, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);

    UPDATE public.proveedores SET id_estado = p_id_estado WHERE id_proveedor = p_id_proveedor;

    v_estado_actual := jsonb_build_object('id_proveedor', p_id_proveedor, 'id_estado', p_id_estado);
    v_mensaje_notif := 'El proveedor ' || v_registro_actual.nombre_proveedor || CASE WHEN p_id_estado = 2 THEN ' ha sido desactivado' ELSE ' ha sido activado' END;

    PERFORM private.registrar_auditoria_rbac(
        v_codigo_accion,
        v_estado_anterior::text,
        v_estado_actual::text,
        'Estado del proveedor',
        'proveedores',
        p_id_proveedor,
        'Cambio de estado mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        v_codigo_notif,
        v_titulo_notif,
        v_mensaje_notif,
        'informativa',
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer,
        'proveedores',
        p_id_proveedor,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_proveedor', p_id_proveedor, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


-- =====================================================================================
-- FABRICANTE
-- =====================================================================================

