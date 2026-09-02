CREATE OR REPLACE FUNCTION public.crear_fabricante_seguro(
    p_nombre_fabricante varchar,
    p_descripcion_fabricante text,
    p_id_proveedor int,
    p_id_pais int,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_id_fabricante int;
    v_id_estado int := 1;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_actual jsonb;
BEGIN
    IF NOT private.usuario_tiene_permiso_codigo('FABRICANTES_CREAR') THEN
        RAISE EXCEPTION 'No tienes permiso para crear fabricantes.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'nombre', p_nombre_fabricante,
        'descripcion', p_descripcion_fabricante,
        'id_proveedor', p_id_proveedor,
        'id_pais', p_id_pais
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_fabricante_seguro', 'FABRICANTES_CREAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    INSERT INTO public.fabricante (
        nombre_fabricante, descripcion_fabricante, id_proveedor, id_estado, id_pais
    ) VALUES (
        trim(p_nombre_fabricante),
        nullif(trim(p_descripcion_fabricante), ''),
        p_id_proveedor,
        v_id_estado,
        p_id_pais
    ) RETURNING id_fabricante INTO v_id_fabricante;

    v_estado_actual := jsonb_build_object(
        'id_fabricante', v_id_fabricante,
        'nombre_fabricante', trim(p_nombre_fabricante),
        'id_estado', v_id_estado
    );

    PERFORM private.registrar_auditoria_rbac(
        'FABRICANTES_CREAR',
        '{}',
        v_estado_actual::text,
        'Fabricante nuevo',
        'fabricante',
        v_id_fabricante,
        'Registro creado mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'FABRICANTE_CREADO',
        'Nuevo fabricante registrado',
        'Se ha registrado el fabricante ' || trim(p_nombre_fabricante),
        'informativa',
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer,
        'fabricante',
        v_id_fabricante,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_fabricante', v_id_fabricante, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


CREATE OR REPLACE FUNCTION public.actualizar_fabricante_seguro(
    p_id_fabricante int,
    p_nombre_fabricante varchar,
    p_descripcion_fabricante text,
    p_id_proveedor int,
    p_id_pais int,
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
    IF NOT private.usuario_tiene_permiso_codigo('FABRICANTES_MODIFICAR') THEN
        RAISE EXCEPTION 'No tienes permiso para modificar fabricantes.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'id_fabricante', p_id_fabricante,
        'nombre', p_nombre_fabricante,
        'descripcion', p_descripcion_fabricante,
        'id_proveedor', p_id_proveedor,
        'id_pais', p_id_pais
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_fabricante_seguro', 'FABRICANTES_MODIFICAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.fabricante WHERE id_fabricante = p_id_fabricante FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El fabricante no existe.' USING ERRCODE = '22023';
    END IF;

    IF v_registro_actual.nombre_fabricante IS NOT DISTINCT FROM trim(p_nombre_fabricante) AND
       v_registro_actual.descripcion_fabricante IS NOT DISTINCT FROM nullif(trim(p_descripcion_fabricante), '') AND
       v_registro_actual.id_proveedor IS NOT DISTINCT FROM p_id_proveedor AND
       v_registro_actual.id_pais IS NOT DISTINCT FROM p_id_pais THEN
        
        v_resultado := jsonb_build_object('id_fabricante', p_id_fabricante, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);

    UPDATE public.fabricante SET
        nombre_fabricante = trim(p_nombre_fabricante),
        descripcion_fabricante = nullif(trim(p_descripcion_fabricante), ''),
        id_proveedor = p_id_proveedor,
        id_pais = p_id_pais
    WHERE id_fabricante = p_id_fabricante;

    v_estado_actual := jsonb_build_object(
        'id_fabricante', p_id_fabricante,
        'nombre_fabricante', trim(p_nombre_fabricante),
        'id_estado', v_registro_actual.id_estado
    );

    PERFORM private.registrar_auditoria_rbac(
        'FABRICANTES_MODIFICAR',
        v_estado_anterior::text,
        v_estado_actual::text,
        'Datos del fabricante',
        'fabricante',
        p_id_fabricante,
        'Actualización mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'FABRICANTE_MODIFICADO',
        'Fabricante modificado',
        'Se han actualizado los datos del fabricante ' || trim(p_nombre_fabricante),
        'informativa',
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer,
        'fabricante',
        p_id_fabricante,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_fabricante', p_id_fabricante, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


CREATE OR REPLACE FUNCTION public.cambiar_estado_fabricante_seguro(
    p_id_fabricante int,
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
    v_codigo_accion text := 'FABRICANTES_MODIFICAR'; -- Fabricante usa modificar para cambios de estado

    v_codigo_notif text;
    v_titulo_notif text;
    v_mensaje_notif text;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_anterior jsonb;
    v_estado_actual jsonb;
BEGIN
    IF p_id_estado = 2 THEN
        v_codigo_notif := 'FABRICANTE_DESACTIVADO';
        v_titulo_notif := 'Fabricante desactivado';
    ELSE
        v_codigo_notif := 'FABRICANTE_ACTIVADO';
        v_titulo_notif := 'Fabricante activado';
    END IF;

    IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
        RAISE EXCEPTION 'No tienes permiso para cambiar el estado de fabricantes.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object('id_fabricante', p_id_fabricante, 'id_estado', p_id_estado);
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_fabricante_seguro', v_codigo_accion, v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.fabricante WHERE id_fabricante = p_id_fabricante FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El fabricante no existe.' USING ERRCODE = '22023';
    END IF;

    IF v_registro_actual.id_estado = p_id_estado THEN
        v_resultado := jsonb_build_object('id_fabricante', p_id_fabricante, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);

    UPDATE public.fabricante SET id_estado = p_id_estado WHERE id_fabricante = p_id_fabricante;

    v_estado_actual := jsonb_build_object('id_fabricante', p_id_fabricante, 'id_estado', p_id_estado);
    v_mensaje_notif := 'El fabricante ' || v_registro_actual.nombre_fabricante || CASE WHEN p_id_estado = 2 THEN ' ha sido desactivado' ELSE ' ha sido activado' END;

    PERFORM private.registrar_auditoria_rbac(
        v_codigo_accion,
        v_estado_anterior::text,
        v_estado_actual::text,
        'Estado del fabricante',
        'fabricante',
        p_id_fabricante,
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
        'fabricante',
        p_id_fabricante,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_fabricante', p_id_fabricante, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


-- =====================================================================================
-- Migración: RPCs para Producto y Categoría con Idempotencia y Notificaciones
-- =====================================================================================

-- =====================================================================================
-- PRODUCTO
-- =====================================================================================

