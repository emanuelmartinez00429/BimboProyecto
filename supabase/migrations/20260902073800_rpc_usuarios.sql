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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'crear_usuario_empleado_seguro', 'USUARIOS_CREAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
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
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'actualizar_usuario_seguro', 'USUARIOS_MODIFICAR', v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.usuarios WHERE id_usuario = p_id_usuario;
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
    v_registro_actual record;
    v_codigo_accion text := 'USUARIOS_ELIMINAR'; -- o modificar
    v_id_accion int := 25;
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

    IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
        RAISE EXCEPTION 'No tienes permiso para cambiar el estado de usuarios.' USING ERRCODE = '42501';
    END IF;

    v_params := jsonb_build_object('id_usuario', p_id_usuario, 'id_estado', p_id_estado);
    v_resultado := private.preparar_solicitud_rpc(p_id_solicitud, 'cambiar_estado_usuario_seguro', v_codigo_accion, v_params);
    IF v_resultado IS NOT NULL THEN
        RETURN v_resultado;
    END IF;

    SELECT * INTO v_registro_actual FROM public.usuarios WHERE id_usuario = p_id_usuario;
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
        2,
        v_id_accion,
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
