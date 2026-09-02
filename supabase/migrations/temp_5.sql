CREATE OR REPLACE FUNCTION public.cambiar_estado_usuario_seguro(
    p_id_usuario integer,
    p_id_estado integer,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_registro_actual record;
    v_codigo_accion text := 'USUARIOS_ELIMINAR'; -- o modificar
    
    v_codigo_notif text;
    v_titulo_notif text;
    v_mensaje_notif text;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_anterior jsonb;
    v_estado_actual jsonb;
BEGIN
    IF p_id_estado = 2 THEN
        v_codigo_notif := 'USUARIO_DESACTIVADO';
        v_titulo_notif := 'Usuario desactivado';
    ELSE
        v_codigo_notif := 'USUARIO_ACTIVADO';
        v_titulo_notif := 'Usuario activado';
    END IF;

    IF p_id_estado = 1 THEN
        v_codigo_accion := 'USUARIOS_ACTIVAR';
    ELSE
        v_codigo_accion := 'USUARIOS_ELIMINAR';
    END IF;

    IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
        RAISE EXCEPTION 'No tienes permiso para cambiar el estado de usuarios.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object('id_usuario', p_id_usuario, 'id_estado', p_id_estado);
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_usuario_seguro', v_codigo_accion, v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.usuarios WHERE id_usuario = p_id_usuario FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El usuario no existe.' USING ERRCODE = '22023';
    END IF;

    IF v_registro_actual.id_estado = p_id_estado THEN
        v_resultado := jsonb_build_object('id_usuario', p_id_usuario, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);
    UPDATE public.usuarios SET id_estado = p_id_estado WHERE id_usuario = p_id_usuario;
    
    v_estado_actual := jsonb_build_object('id_usuario', p_id_usuario, 'id_estado', p_id_estado);
    v_mensaje_notif := 'El usuario ' || v_registro_actual.alias_usuario || CASE WHEN p_id_estado = 2 THEN ' ha sido desactivado' ELSE ' ha sido activado' END;

    PERFORM private.registrar_auditoria_rbac(
        v_codigo_accion,
        v_estado_anterior::text,
        v_estado_actual::text,
        'Estado del usuario',
        'usuarios',
        p_id_usuario,
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
        'usuarios',
        p_id_usuario,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_usuario', p_id_usuario, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;

DO $do$ 
DECLARE r record; 
BEGIN 
    FOR r IN 
        SELECT p.oid::regprocedure firma 
        FROM pg_proc p 
        JOIN pg_namespace n ON n.oid=p.pronamespace 
        WHERE n.nspname='public' 
        AND p.proname IN (
            'crear_proveedor_seguro', 'actualizar_proveedor_seguro', 'cambiar_estado_proveedor_seguro',
            'crear_fabricante_seguro', 'actualizar_fabricante_seguro', 'cambiar_estado_fabricante_seguro',
            'crear_producto_seguro', 'actualizar_producto_seguro', 'cambiar_estado_producto_seguro',
            'crear_categoria_seguro', 'actualizar_categoria_seguro', 'cambiar_estado_categoria_seguro',
            'crear_usuario_empleado_seguro', 'actualizar_usuario_empleado_seguro', 'cambiar_estado_usuario_seguro',
            'cambiar_contrasena_usuario_seguro'
        ) 
    LOOP 
        EXECUTE format('REVOKE ALL ON FUNCTION %s FROM public, anon', r.firma); 
        EXECUTE format('GRANT EXECUTE ON FUNCTION %s TO authenticated', r.firma); 
    END LOOP; 
END $do$;
