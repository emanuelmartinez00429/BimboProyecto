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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_proveedor_seguro', 'PROVEEDORES_CREAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
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
        4,
        14,
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_proveedor_seguro', 'PROVEEDORES_MODIFICAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.proveedores WHERE id_proveedor = p_id_proveedor;
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
        4,
        15,
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
    IF p_id_estado = 2 THEN
        v_codigo_accion := 'PROVEEDORES_ELIMINAR';
        v_id_accion := 16;
        v_codigo_notif := 'PROVEEDOR_DESACTIVADO';
        v_titulo_notif := 'Proveedor desactivado';
    ELSE
        v_codigo_accion := 'PROVEEDORES_MODIFICAR';
        v_id_accion := 15;
        v_codigo_notif := 'PROVEEDOR_ACTIVADO';
        v_titulo_notif := 'Proveedor activado';
    END IF;

    IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
        RAISE EXCEPTION 'No tienes permiso para cambiar el estado de proveedores.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object('id_proveedor', p_id_proveedor, 'id_estado', p_id_estado);
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_proveedor_seguro', v_codigo_accion, v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.proveedores WHERE id_proveedor = p_id_proveedor;
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
        4,
        v_id_accion,
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_fabricante_seguro', 'FABRICANTES_CREAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
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
        4,
        17,
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_fabricante_seguro', 'FABRICANTES_MODIFICAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.fabricante WHERE id_fabricante = p_id_fabricante;
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
        4,
        18,
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
    v_registro_actual record;
    v_codigo_accion text := 'FABRICANTES_MODIFICAR'; -- Fabricante usa modificar para cambios de estado
    v_id_accion int := 18;
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_fabricante_seguro', v_codigo_accion, v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.fabricante WHERE id_fabricante = p_id_fabricante;
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
        4,
        v_id_accion,
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
