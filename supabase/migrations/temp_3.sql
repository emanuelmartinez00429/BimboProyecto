CREATE OR REPLACE FUNCTION public.cambiar_estado_producto_seguro(
    p_id_producto integer,
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
    v_codigo_accion text := 'PRODUCTOS_ELIMINAR';

    v_codigo_notif text;
    v_titulo_notif text;
    v_mensaje_notif text;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_anterior jsonb;
    v_estado_actual jsonb;
BEGIN
    IF p_id_estado = 2 THEN
        v_codigo_notif := 'PRODUCTO_DESACTIVADO';
        v_titulo_notif := 'Producto desactivado';
    ELSE
        v_codigo_notif := 'PRODUCTO_ACTIVADO';
        v_titulo_notif := 'Producto activado';
    END IF;

    IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
        RAISE EXCEPTION 'No tienes permiso para cambiar el estado de productos.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object('id_producto', p_id_producto, 'id_estado', p_id_estado);
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_producto_seguro', v_codigo_accion, v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.productos WHERE id_producto = p_id_producto FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El producto no existe.' USING ERRCODE = '22023';
    END IF;

    IF v_registro_actual.id_estado = p_id_estado THEN
        v_resultado := jsonb_build_object('id_producto', p_id_producto, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);
    UPDATE public.productos SET id_estado = p_id_estado WHERE id_producto = p_id_producto;
    
    v_estado_actual := jsonb_build_object('id_producto', p_id_producto, 'id_estado', p_id_estado);
    v_mensaje_notif := 'El producto ' || v_registro_actual.nombre_producto || CASE WHEN p_id_estado = 2 THEN ' ha sido desactivado' ELSE ' ha sido activado' END;

    PERFORM private.registrar_auditoria_rbac(
        v_codigo_accion,
        v_estado_anterior::text,
        v_estado_actual::text,
        'Estado del producto',
        'productos',
        p_id_producto,
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
        'productos',
        p_id_producto,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_producto', p_id_producto, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


-- =====================================================================================
-- CATEGORIA
-- =====================================================================================

CREATE OR REPLACE FUNCTION public.crear_categoria_seguro(
    p_nombre_categoria character varying,
    p_descripcion_categoria character varying,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_id_categoria int;
    v_estado_categoria boolean := true;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_actual jsonb;
BEGIN
    IF NOT private.usuario_tiene_permiso_codigo('CATEGORIAS_CREAR') THEN
        RAISE EXCEPTION 'No tienes permiso para crear categorías.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'nombre', p_nombre_categoria,
        'descripcion', p_descripcion_categoria
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_categoria_seguro', 'CATEGORIAS_CREAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    INSERT INTO public.categoria (
        nombre_categoria, descripcion_categoria, estado_categoria
    ) VALUES (
        trim(p_nombre_categoria),
        nullif(trim(p_descripcion_categoria), ''),
        v_estado_categoria
    ) RETURNING id_categoria INTO v_id_categoria;

    v_estado_actual := jsonb_build_object(
        'id_categoria', v_id_categoria,
        'nombre_categoria', trim(p_nombre_categoria),
        'estado_categoria', v_estado_categoria
    );

    PERFORM private.registrar_auditoria_rbac(
        'CATEGORIAS_CREAR',
        '{}',
        v_estado_actual::text,
        'Categoría nueva',
        'categoria',
        v_id_categoria,
        'Registro creado mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'CATEGORIA_CREADA',
        'Nueva categoría registrada',
        'Se ha registrado la categoría ' || trim(p_nombre_categoria),
        'informativa',
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer, -- CATEGORIAS_CREAR
        'categoria',
        v_id_categoria,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_categoria', v_id_categoria, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


CREATE OR REPLACE FUNCTION public.actualizar_categoria_seguro(
    p_id_categoria integer,
    p_nombre_categoria character varying,
    p_descripcion_categoria character varying,
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
    IF NOT private.usuario_tiene_permiso_codigo('CATEGORIAS_MODIFICAR') THEN
        RAISE EXCEPTION 'No tienes permiso para modificar categorías.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'id_categoria', p_id_categoria,
        'nombre', p_nombre_categoria,
        'descripcion', p_descripcion_categoria
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_categoria_seguro', 'CATEGORIAS_MODIFICAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.categoria WHERE id_categoria = p_id_categoria FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'La categoría no existe.' USING ERRCODE = '22023';
    END IF;

    IF v_registro_actual.nombre_categoria IS NOT DISTINCT FROM trim(p_nombre_categoria) AND
       v_registro_actual.descripcion_categoria IS NOT DISTINCT FROM nullif(trim(p_descripcion_categoria), '') THEN
        
        v_resultado := jsonb_build_object('id_categoria', p_id_categoria, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);

    UPDATE public.categoria SET
        nombre_categoria = trim(p_nombre_categoria),
        descripcion_categoria = nullif(trim(p_descripcion_categoria), '')
    WHERE id_categoria = p_id_categoria;

    v_estado_actual := jsonb_build_object(
        'id_categoria', p_id_categoria,
        'nombre_categoria', trim(p_nombre_categoria),
        'estado_categoria', v_registro_actual.estado_categoria
    );

    PERFORM private.registrar_auditoria_rbac(
        'CATEGORIAS_MODIFICAR',
        v_estado_anterior::text,
        v_estado_actual::text,
        'Datos de la categoría',
        'categoria',
        p_id_categoria,
        'Actualización mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'CATEGORIA_MODIFICADA',
        'Categoría modificada',
        'Se han actualizado los datos de la categoría ' || trim(p_nombre_categoria),
        'informativa',
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer, -- CATEGORIAS_MODIFICAR
        'categoria',
        p_id_categoria,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_categoria', p_id_categoria, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


