-- =====================================================================================
-- Migración: RPCs para Proveedor y Fabricante con Idempotencia y Notificaciones
-- =====================================================================================

-- =====================================================================================
-- PROVEEDOR
-- =====================================================================================

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
    IF NOT p_estado_categoria THEN
        v_codigo_accion := 'CATEGORIAS_DESACTIVAR';

        v_codigo_notif := 'CATEGORIA_DESACTIVADA';
        v_titulo_notif := 'Categoría desactivada';
    ELSE
        v_codigo_accion := 'CATEGORIAS_ACTIVAR';

        v_codigo_notif := 'CATEGORIA_ACTIVADA';
        v_titulo_notif := 'Categoría activada';
    END IF;

    IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
        RAISE EXCEPTION 'No tienes permiso para cambiar el estado de categorías.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object('id_categoria', p_id_categoria, 'estado_categoria', p_estado_categoria);
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_categoria_seguro', v_codigo_accion, v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.categoria WHERE id_categoria = p_id_categoria FOR UPDATE;
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
        (v_ctx->>'id_modulo')::integer,
        (v_ctx->>'id_accion')::integer,
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


-- =====================================================================================
-- Migración: RPCs para Usuarios con Idempotencia y Notificaciones
-- =====================================================================================

DROP FUNCTION IF EXISTS public.crear_usuario_empleado_seguro(integer, text, integer);

CREATE OR REPLACE FUNCTION public.crear_usuario_empleado_seguro(
    p_id_empleado integer,
    p_email text,
    p_rol integer,
    p_id_solicitud uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_user_id uuid;
    v_id_usuario integer;
    v_params jsonb;
    v_resultado jsonb;
    v_estado_actual jsonb;
BEGIN
    IF NOT private.usuario_tiene_permiso_codigo('USUARIOS_CREAR') THEN
        RAISE EXCEPTION 'No tienes permiso para crear usuarios.' USING ERRCODE = '42501';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM public.roles WHERE id_rol = p_rol AND id_estado = 1) THEN
        RAISE EXCEPTION 'El rol seleccionado no existe o está inactivo.' USING ERRCODE = '22023';
    END IF;

    v_params := jsonb_build_object(
        'id_empleado', p_id_empleado,
        'email', p_email,
        'rol', p_rol
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_usuario_empleado_seguro', 'USUARIOS_CREAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    -- Se obtiene el id del usuario de auth (debe haberse creado previamente desde el cliente)
    SELECT au.id INTO v_user_id
    FROM auth.users au
    WHERE lower(au.email) = lower(btrim(p_email))
    ORDER BY au.created_at DESC
    LIMIT 1;

    IF v_user_id IS NULL THEN
        -- Retornamos JSON con un flag de error en vez de lanzar excepción, 
        -- para que el backend C# lo lea.
        v_resultado := jsonb_build_object('error', 'USUARIO_NO_EXISTE');
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    INSERT INTO public.usuarios (
        id_empleado, alias_usuario, uuid_usuario, id_rol, id_estado
    ) VALUES (
        p_id_empleado, btrim(p_email), v_user_id, p_rol, 1
    )
    ON CONFLICT (uuid_usuario) DO NOTHING
    RETURNING id_usuario INTO v_id_usuario;

    IF v_id_usuario IS NULL THEN
        v_resultado := jsonb_build_object('error', 'USUARIO_YA_EXISTE');
        PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
        RETURN v_resultado;
    END IF;

    v_estado_actual := jsonb_build_object(
        'id_usuario', v_id_usuario,
        'email', btrim(p_email),
        'id_rol', p_rol,
        'id_estado', 1
    );

    PERFORM private.registrar_auditoria_rbac(
        'USUARIOS_CREAR',
        '{}',
        v_estado_actual::text,
        'Usuario',
        'usuarios',
        v_id_usuario,
        'Creación de usuario con asignación inicial de rol',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'USUARIO_CREADO',
        'Nuevo usuario registrado',
        'Se ha registrado el usuario ' || btrim(p_email),
        'informativa',
        2, -- Módulo de Seguridad
        23, -- USUARIOS_CREAR
        'usuarios',
        v_id_usuario,
        p_id_solicitud,
        NULL,
        v_estado_actual
    );

    v_resultado := jsonb_build_object('id_usuario', v_id_usuario, 'hubo_cambios', true);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud, v_resultado);
    RETURN v_resultado;
END;
$$;


CREATE OR REPLACE FUNCTION public.actualizar_usuario_seguro(
    p_id_usuario integer,
    p_id_rol integer,
    p_id_estado integer,
    p_email text,
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
    IF NOT private.usuario_tiene_permiso_codigo('USUARIOS_MODIFICAR') THEN
        RAISE EXCEPTION 'No tienes permiso para modificar usuarios.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object(
        'id_usuario', p_id_usuario,
        'id_rol', p_id_rol,
        'id_estado', p_id_estado,
        'email', p_email
    );
    v_ctx := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_usuario_seguro', 'USUARIOS_MODIFICAR', v_params);
    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    SELECT * INTO v_registro_actual FROM public.usuarios WHERE id_usuario = p_id_usuario FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'El usuario no existe.' USING ERRCODE = '22023';
    END IF;

    -- Update table
    v_estado_anterior := to_jsonb(v_registro_actual);

    UPDATE public.usuarios SET
        id_rol = COALESCE(p_id_rol, id_rol),
        id_estado = COALESCE(p_id_estado, id_estado),
        alias_usuario = COALESCE(p_email, alias_usuario)
    WHERE id_usuario = p_id_usuario;

    v_estado_actual := jsonb_build_object(
        'id_usuario', p_id_usuario,
        'id_rol', COALESCE(p_id_rol, v_registro_actual.id_rol),
        'id_estado', COALESCE(p_id_estado, v_registro_actual.id_estado),
        'email', COALESCE(p_email, v_registro_actual.alias_usuario)
    );

    PERFORM private.registrar_auditoria_rbac(
        'USUARIOS_MODIFICAR',
        v_estado_anterior::text,
        v_estado_actual::text,
        'Datos del usuario',
        'usuarios',
        p_id_usuario,
        'Actualización mediante RPC segura',
        p_id_solicitud
    );

    PERFORM private.crear_notificacion(
        'USUARIO_MODIFICADO',
        'Usuario modificado',
        'Se han actualizado los datos del usuario ' || COALESCE(p_email, v_registro_actual.alias_usuario),
        'informativa',
        2,
        24, -- USUARIOS_MODIFICAR
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
