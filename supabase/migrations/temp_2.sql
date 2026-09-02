CREATE OR REPLACE FUNCTION public.crear_producto_seguro(
    p_codigo_producto character varying,
    p_nombre_producto character varying,
    p_id_presentacion integer,
    p_id_fabricante integer,
    p_id_unidad integer,
    p_peso_teorico numeric,
    p_id_tara integer,
    p_id_categoria integer,
    p_contenido character varying,
    p_id_pais integer,
    p_precio_por_kg numeric,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_id_producto int;
    v_id_estado int := 1;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_actual jsonb;
BEGIN
    IF NOT private.usuario_tiene_permiso_codigo('PRODUCTOS_CREAR') THEN
        RAISE EXCEPTION 'No tienes permiso para crear productos.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'codigo', p_codigo_producto,
        'nombre', p_nombre_producto,
        'id_presentacion', p_id_presentacion,
        'id_fabricante', p_id_fabricante,
        'id_unidad', p_id_unidad,
        'peso_teorico', p_peso_teorico,
        'id_tara', p_id_tara,
        'id_categoria', p_id_categoria,
        'contenido', p_contenido,
        'id_pais', p_id_pais,
        'precio_por_kg', p_precio_por_kg
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_producto_seguro', 'PRODUCTOS_CREAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    INSERT INTO public.productos (
        codigo_producto, nombre_producto, id_presentacion, id_fabricante, id_unidad, id_estado,
        peso_teorico, id_tara, id_categoria, contenido, id_pais, precio_por_kg
    ) VALUES (
        trim(p_codigo_producto),
        trim(p_nombre_producto),
        p_id_presentacion,
        p_id_fabricante,
        p_id_unidad,
        v_id_estado,
        p_peso_teorico,
        p_id_tara,
        p_id_categoria,
        nullif(trim(p_contenido), ''),
        p_id_pais,
        p_precio_por_kg
    ) RETURNING id_producto INTO v_id_producto;

    v_estado_actual := jsonb_build_object(
        'id_producto', v_id_producto,
        'nombre_producto', trim(p_nombre_producto),
        'id_estado', v_id_estado
    );

    PERFORM private.registrar_auditoria_rbac(
        'PRODUCTOS_CREAR',
        '{}',
        v_estado_actual::text,
        'Producto nuevo',
        'productos',
        v_id_producto,
        'Registro creado mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'PRODUCTO_CREADO',
        'Nuevo producto registrado',
        'Se ha registrado el producto ' || trim(p_nombre_producto),
        'informativa',
        1, -- id_modulo Gestion Inventario
        1, -- PRODUCTOS_CREAR
        'productos',
        v_id_producto,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_producto', v_id_producto, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


CREATE OR REPLACE FUNCTION public.actualizar_producto_seguro(
    p_id_producto integer,
    p_codigo_producto character varying,
    p_nombre_producto character varying,
    p_id_presentacion integer,
    p_id_fabricante integer,
    p_id_unidad integer,
    p_peso_teorico numeric,
    p_id_tara integer,
    p_id_categoria integer,
    p_contenido character varying,
    p_id_pais integer,
    p_precio_por_kg numeric,
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
    IF NOT private.usuario_tiene_permiso_codigo('PRODUCTOS_MODIFICAR') THEN
        RAISE EXCEPTION 'No tienes permiso para modificar productos.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'id_producto', p_id_producto,
        'codigo', p_codigo_producto,
        'nombre', p_nombre_producto,
        'id_presentacion', p_id_presentacion,
        'id_fabricante', p_id_fabricante,
        'id_unidad', p_id_unidad,
        'peso_teorico', p_peso_teorico,
        'id_tara', p_id_tara,
        'id_categoria', p_id_categoria,
        'contenido', p_contenido,
        'id_pais', p_id_pais,
        'precio_por_kg', p_precio_por_kg
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_producto_seguro', 'PRODUCTOS_MODIFICAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.productos WHERE id_producto = p_id_producto FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El producto no existe.' USING ERRCODE = '22023';
    END IF;

    -- Simple check to avoid no-op not handled exhaustively to save space
    IF v_registro_actual.nombre_producto IS NOT DISTINCT FROM trim(p_nombre_producto) AND
       v_registro_actual.codigo_producto IS NOT DISTINCT FROM trim(p_codigo_producto) AND
       v_registro_actual.id_presentacion IS NOT DISTINCT FROM p_id_presentacion AND
       v_registro_actual.id_fabricante IS NOT DISTINCT FROM p_id_fabricante AND
       v_registro_actual.id_unidad IS NOT DISTINCT FROM p_id_unidad AND
       v_registro_actual.peso_teorico IS NOT DISTINCT FROM p_peso_teorico AND
       v_registro_actual.id_tara IS NOT DISTINCT FROM p_id_tara AND
       v_registro_actual.id_categoria IS NOT DISTINCT FROM p_id_categoria AND
       v_registro_actual.contenido IS NOT DISTINCT FROM nullif(trim(p_contenido), '') AND
       v_registro_actual.id_pais IS NOT DISTINCT FROM p_id_pais AND
       v_registro_actual.precio_por_kg IS NOT DISTINCT FROM p_precio_por_kg THEN
        
        v_resultado := jsonb_build_object('id_producto', p_id_producto, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);

    UPDATE public.productos SET
        codigo_producto = trim(p_codigo_producto),
        nombre_producto = trim(p_nombre_producto),
        id_presentacion = p_id_presentacion,
        id_fabricante = p_id_fabricante,
        id_unidad = p_id_unidad,
        peso_teorico = p_peso_teorico,
        id_tara = p_id_tara,
        id_categoria = p_id_categoria,
        contenido = nullif(trim(p_contenido), ''),
        id_pais = p_id_pais,
        precio_por_kg = p_precio_por_kg
    WHERE id_producto = p_id_producto;

    v_estado_actual := jsonb_build_object(
        'id_producto', p_id_producto,
        'nombre_producto', trim(p_nombre_producto),
        'id_estado', v_registro_actual.id_estado
    );

    PERFORM private.registrar_auditoria_rbac(
        'PRODUCTOS_MODIFICAR',
        v_estado_anterior::text,
        v_estado_actual::text,
        'Datos del producto',
        'productos',
        p_id_producto,
        'Actualización mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'PRODUCTO_MODIFICADO',
        'Producto modificado',
        'Se han actualizado los datos del producto ' || trim(p_nombre_producto),
        'informativa',
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer, -- PRODUCTOS_MODIFICAR
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


