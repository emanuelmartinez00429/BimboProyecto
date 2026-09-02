-- Corrección forward-only: no exponer correo en notificaciones de creación.
CREATE OR REPLACE FUNCTION public.crear_usuario_empleado_seguro(
  p_id_empleado integer,p_email text,p_rol integer,p_id_solicitud uuid
) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $function$
DECLARE v_ctx jsonb; v_user_id uuid; v_id_usuario integer; v_params jsonb; v_resultado jsonb; v_estado_actual jsonb;
BEGIN
  IF NOT private.usuario_tiene_permiso_codigo('USUARIOS_CREAR') THEN
    RAISE EXCEPTION 'No tienes permiso para crear usuarios.' USING ERRCODE='42501';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM public.roles WHERE id_rol=p_rol AND id_estado=1) THEN
    RAISE EXCEPTION 'El rol seleccionado no existe o está inactivo.' USING ERRCODE='22023';
  END IF;
  v_params:=jsonb_build_object('id_empleado',p_id_empleado,'email',p_email,'rol',p_rol);
  v_ctx:=private.preparar_solicitud_rpc(p_id_solicitud,'crear_usuario_empleado_seguro','USUARIOS_CREAR',v_params);
  IF (v_ctx->>'es_reintento')::boolean THEN RETURN v_ctx->'resultado'; END IF;
  SELECT au.id INTO v_user_id FROM auth.users au WHERE lower(au.email)=lower(btrim(p_email)) ORDER BY au.created_at DESC LIMIT 1;
  IF v_user_id IS NULL THEN
    v_resultado:=jsonb_build_object('error','USUARIO_NO_EXISTE');
    PERFORM private.completar_solicitud_rpc(p_id_solicitud,v_resultado); RETURN v_resultado;
  END IF;
  INSERT INTO public.usuarios(id_empleado,alias_usuario,uuid_usuario,id_rol,id_estado)
  VALUES(p_id_empleado,btrim(p_email),v_user_id,p_rol,1)
  ON CONFLICT(uuid_usuario) DO NOTHING RETURNING id_usuario INTO v_id_usuario;
  IF v_id_usuario IS NULL THEN
    v_resultado:=jsonb_build_object('error','USUARIO_YA_EXISTE');
    PERFORM private.completar_solicitud_rpc(p_id_solicitud,v_resultado); RETURN v_resultado;
  END IF;
  v_estado_actual:=jsonb_build_object('id_usuario',v_id_usuario,'id_rol',p_rol,'id_estado',1);
  PERFORM private.registrar_auditoria_rbac('USUARIOS_CREAR','{}',v_estado_actual::text,'Usuario','usuarios',v_id_usuario,
    'Creación de usuario con asignación inicial de rol',p_id_solicitud);
  PERFORM private.crear_notificacion('USUARIO_CREADO','Nuevo usuario registrado','Se registró un nuevo usuario.',
    'informativa',(v_ctx->>'id_modulo')::integer,(v_ctx->>'id_accion')::integer,'usuarios',v_id_usuario,p_id_solicitud,NULL,
    jsonb_build_object('id_usuario',v_id_usuario));
  v_resultado:=jsonb_build_object('id_usuario',v_id_usuario,'hubo_cambios',true);
  PERFORM private.completar_solicitud_rpc(p_id_solicitud,v_resultado); RETURN v_resultado;
END;
$function$;

REVOKE ALL ON FUNCTION public.crear_usuario_empleado_seguro(integer,text,integer,uuid) FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.crear_usuario_empleado_seguro(integer,text,integer,uuid) TO authenticated;
