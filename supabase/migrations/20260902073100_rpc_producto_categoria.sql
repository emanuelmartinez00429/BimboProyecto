-- =====================================================================================
-- Migración: RPCs para Producto y Categoría con Idempotencia y Notificaciones
-- =====================================================================================

-- =====================================================================================
-- PRODUCTO
-- =====================================================================================

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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_producto_seguro', 'PRODUCTOS_CREAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_producto_seguro', 'PRODUCTOS_MODIFICAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.productos WHERE id_producto = p_id_producto;
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
        1,
        2, -- PRODUCTOS_MODIFICAR
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
    v_registro_actual record;
    v_codigo_accion text := 'PRODUCTOS_ELIMINAR';
    v_id_accion int := 3;
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_producto_seguro', v_codigo_accion, v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.productos WHERE id_producto = p_id_producto;
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
        1,
        v_id_accion,
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_categoria_seguro', 'CATEGORIAS_CREAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
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
        1,
        38, -- CATEGORIAS_CREAR
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_categoria_seguro', 'CATEGORIAS_MODIFICAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.categoria WHERE id_categoria = p_id_categoria;
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
        1,
        39, -- CATEGORIAS_MODIFICAR
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


CREATE OR REPLACE FUNCTION public.cambiar_estado_categoria_seguro(
    p_id_categoria integer,
    p_estado_categoria boolean,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_registro_actual record;
    v_codigo_accion text;
    v_id_accion int;
    v_codigo_notif text;
    v_titulo_notif text;
    v_mensaje_notif text;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_anterior jsonb;
    v_estado_actual jsonb;
BEGIN
    IF NOT p_estado_categoria THEN
        v_codigo_accion := 'CATEGORIAS_DESACTIVAR';
        v_id_accion := 40;
        v_codigo_notif := 'CATEGORIA_DESACTIVADA';
        v_titulo_notif := 'Categoría desactivada';
    ELSE
        v_codigo_accion := 'CATEGORIAS_ACTIVAR';
        v_id_accion := 41;
        v_codigo_notif := 'CATEGORIA_ACTIVADA';
        v_titulo_notif := 'Categoría activada';
    END IF;

    IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
        RAISE EXCEPTION 'No tienes permiso para cambiar el estado de categorías.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object('id_categoria', p_id_categoria, 'estado_categoria', p_estado_categoria);
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_categoria_seguro', v_codigo_accion, v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.categoria WHERE id_categoria = p_id_categoria;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'La categoría no existe.' USING ERRCODE = '22023';
    END IF;

    IF v_registro_actual.estado_categoria = p_estado_categoria THEN
        v_resultado := jsonb_build_object('id_categoria', p_id_categoria, 'hubo_cambios', false);
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_anterior := to_jsonb(v_registro_actual);
    UPDATE public.categoria SET estado_categoria = p_estado_categoria WHERE id_categoria = p_id_categoria;
    
    v_estado_actual := jsonb_build_object('id_categoria', p_id_categoria, 'estado_categoria', p_estado_categoria);
    v_mensaje_notif := 'La categoría ' || v_registro_actual.nombre_categoria || CASE WHEN NOT p_estado_categoria THEN ' ha sido desactivada' ELSE ' ha sido activada' END;

    PERFORM private.registrar_auditoria_rbac(
        v_codigo_accion,
        v_estado_anterior::text,
        v_estado_actual::text,
        'Estado de la categoría',
        'categoria',
        p_id_categoria,
        'Cambio de estado mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        v_codigo_notif,
        v_titulo_notif,
        v_mensaje_notif,
        'informativa',
        1,
        v_id_accion,
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
