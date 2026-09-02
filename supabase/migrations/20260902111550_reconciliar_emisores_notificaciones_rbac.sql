-- Reconciliación forward-only de emisores empresariales de notificaciones.
-- Rollback documentado: restaurar las definiciones previas desde la migración
-- 20260902140000_hotfix_notificaciones_seguridad_rbac.sql y recrear únicamente
-- las políticas DML que un consumidor legítimo necesite. No elimina datos.

-- La metadata de una notificación es exclusivamente auxiliar de navegación.
-- El origen canónico ya vive en las columnas tabla_origen/id_registro_origen.
CREATE OR REPLACE FUNCTION private.crear_notificacion(
  p_codigo_tipo text,p_titulo text,p_mensaje text,p_severidad text DEFAULT NULL,
  p_id_modulo integer DEFAULT NULL,p_id_accion integer DEFAULT NULL,p_tabla_origen text DEFAULT NULL,
  p_id_registro_origen integer DEFAULT NULL,p_id_solicitud uuid DEFAULT NULL,
  p_clave_deduplicacion text DEFAULT NULL,p_metadata jsonb DEFAULT NULL
) RETURNS bigint LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,pg_temp
AS $function$
DECLARE v_tipo public.tipos_notificacion%rowtype; v_usuario integer; v_id bigint; v_metadata jsonb;
BEGIN
  SELECT * INTO v_tipo FROM public.tipos_notificacion WHERE codigo=p_codigo_tipo AND id_estado=1;
  IF NOT FOUND THEN RAISE EXCEPTION USING errcode='22023',message='Tipo de notificacion inexistente o inactivo'; END IF;
  SELECT id_usuario INTO v_usuario FROM public.usuarios WHERE uuid_usuario=auth.uid() AND id_estado=1;
  IF v_usuario IS NULL THEN RAISE EXCEPTION USING errcode='28000',message='Usuario autenticado inexistente o inactivo'; END IF;
  IF p_metadata IS NOT NULL AND jsonb_typeof(p_metadata)<>'object' THEN RAISE EXCEPTION 'metadata debe ser un objeto JSON'; END IF;
  v_metadata := CASE WHEN p_id_registro_origen IS NULL THEN '{}'::jsonb
                     ELSE jsonb_build_object('id_registro_origen', p_id_registro_origen) END;
  INSERT INTO public.notificaciones(id_tipo_notificacion,titulo,mensaje,severidad,id_modulo,id_accion,id_usuario_actor,
    tabla_origen,id_registro_origen,id_solicitud,clave_deduplicacion,metadata)
  VALUES(v_tipo.id_tipo_notificacion,left(btrim(p_titulo),150),left(btrim(p_mensaje),1000),
    coalesce(p_severidad,v_tipo.severidad_predeterminada),p_id_modulo,p_id_accion,v_usuario,
    p_tabla_origen,p_id_registro_origen,p_id_solicitud,p_clave_deduplicacion,v_metadata)
  ON CONFLICT(id_tipo_notificacion,id_solicitud) WHERE id_solicitud IS NOT NULL DO NOTHING
  RETURNING id_notificacion INTO v_id;
  IF v_id IS NULL AND p_id_solicitud IS NOT NULL THEN
    SELECT id_notificacion INTO v_id FROM public.notificaciones
      WHERE id_tipo_notificacion=v_tipo.id_tipo_notificacion AND id_solicitud=p_id_solicitud;
  END IF;
  IF v_id IS NULL THEN RAISE EXCEPTION 'No se pudo crear la notificacion'; END IF;
  INSERT INTO public.notificaciones_usuario(id_notificacion,id_usuario)
  SELECT v_id,u.id_usuario FROM public.usuarios u
  JOIN public.roles r ON r.id_rol=u.id_rol AND r.id_estado=1
  JOIN public.acciones_roles ar ON ar.id_rol=r.id_rol AND ar.id_estado=1
  JOIN public.acciones a ON a.id_accion=ar.id_accion AND a.codigo_accion='NOTIFICACIONES_CONSULTAR'
  WHERE u.id_estado=1 ON CONFLICT(id_notificacion,id_usuario) DO NOTHING;
  RETURN v_id;
END;
$function$;

CREATE OR REPLACE FUNCTION public.actualizar_usuario_seguro(
  p_id_usuario integer,p_id_rol integer,p_id_estado integer,p_email text,p_id_solicitud uuid
) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $function$
DECLARE
  v_ctx jsonb; v_registro_actual record; v_params jsonb; v_resultado jsonb;
  v_estado_anterior jsonb; v_estado_actual jsonb; v_rol integer; v_estado integer; v_alias text;
BEGIN
  IF NOT private.usuario_tiene_permiso_codigo('USUARIOS_MODIFICAR') THEN
    RAISE EXCEPTION 'No tienes permiso para modificar usuarios.' USING ERRCODE='42501';
  END IF;
  v_params := jsonb_build_object('id_usuario',p_id_usuario,'id_rol',p_id_rol,'id_estado',p_id_estado,'email',p_email);
  v_ctx := private.preparar_solicitud_rpc(p_id_solicitud,'actualizar_usuario_seguro','USUARIOS_MODIFICAR',v_params);
  IF (v_ctx->>'es_reintento')::boolean THEN RETURN v_ctx->'resultado'; END IF;
  SELECT * INTO v_registro_actual FROM public.usuarios WHERE id_usuario=p_id_usuario FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'El usuario no existe.' USING ERRCODE='22023'; END IF;
  v_rol := COALESCE(p_id_rol,v_registro_actual.id_rol);
  v_estado := COALESCE(p_id_estado,v_registro_actual.id_estado);
  v_alias := COALESCE(NULLIF(btrim(p_email),''),v_registro_actual.alias_usuario);
  IF p_id_rol IS NOT NULL AND NOT EXISTS (SELECT 1 FROM public.roles WHERE id_rol=p_id_rol AND id_estado=1) THEN
    RAISE EXCEPTION 'El rol seleccionado no existe o está inactivo.' USING ERRCODE='22023';
  END IF;
  IF v_registro_actual.id_rol IS NOT DISTINCT FROM v_rol
     AND v_registro_actual.id_estado IS NOT DISTINCT FROM v_estado
     AND v_registro_actual.alias_usuario IS NOT DISTINCT FROM v_alias THEN
    v_resultado:=jsonb_build_object('id_usuario',p_id_usuario,'hubo_cambios',false);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud,v_resultado); RETURN v_resultado;
  END IF;
  v_estado_anterior:=to_jsonb(v_registro_actual);
  UPDATE public.usuarios SET id_rol=v_rol,id_estado=v_estado,alias_usuario=v_alias WHERE id_usuario=p_id_usuario;
  v_estado_actual:=jsonb_build_object('id_usuario',p_id_usuario,'id_rol',v_rol,'id_estado',v_estado);
  PERFORM private.registrar_auditoria_rbac('USUARIOS_MODIFICAR',v_estado_anterior::text,v_estado_actual::text,
    'Datos del usuario','usuarios',p_id_usuario,'Actualización mediante RPC segura',p_id_solicitud);
  PERFORM private.crear_notificacion('USUARIO_MODIFICADO','Usuario modificado','Se actualizaron los datos de un usuario.',
    'informativa',(v_ctx->>'id_modulo')::integer,(v_ctx->>'id_accion')::integer,'usuarios',p_id_usuario,p_id_solicitud,NULL,
    jsonb_build_object('id_usuario',p_id_usuario));
  v_resultado:=jsonb_build_object('id_usuario',p_id_usuario,'hubo_cambios',true);
  PERFORM private.completar_solicitud_rpc(p_id_solicitud,v_resultado); RETURN v_resultado;
END;
$function$;

CREATE OR REPLACE FUNCTION public.cambiar_estado_usuario_seguro(
  p_id_usuario integer,p_id_estado integer,p_id_solicitud uuid
) RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $function$
DECLARE
  v_ctx jsonb; v_registro_actual record; v_codigo_accion text; v_codigo_notif text;
  v_titulo_notif text; v_params jsonb; v_resultado jsonb; v_estado_anterior jsonb; v_estado_actual jsonb;
BEGIN
  IF p_id_estado NOT IN (1,2) THEN RAISE EXCEPTION 'Estado de usuario inválido.' USING ERRCODE='22023'; END IF;
  v_codigo_accion:=CASE WHEN p_id_estado=1 THEN 'USUARIOS_MODIFICAR' ELSE 'USUARIOS_ELIMINAR' END;
  v_codigo_notif:=CASE WHEN p_id_estado=1 THEN 'USUARIO_ACTIVADO' ELSE 'USUARIO_DESACTIVADO' END;
  v_titulo_notif:=CASE WHEN p_id_estado=1 THEN 'Usuario activado' ELSE 'Usuario desactivado' END;
  IF NOT private.usuario_tiene_permiso_codigo(v_codigo_accion) THEN
    RAISE EXCEPTION 'No tienes permiso para cambiar el estado de usuarios.' USING ERRCODE='42501';
  END IF;
  v_params:=jsonb_build_object('id_usuario',p_id_usuario,'id_estado',p_id_estado);
  v_ctx:=private.preparar_solicitud_rpc(p_id_solicitud,'cambiar_estado_usuario_seguro',v_codigo_accion,v_params);
  IF (v_ctx->>'es_reintento')::boolean THEN RETURN v_ctx->'resultado'; END IF;
  SELECT * INTO v_registro_actual FROM public.usuarios WHERE id_usuario=p_id_usuario FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'El usuario no existe.' USING ERRCODE='22023'; END IF;
  IF v_registro_actual.id_estado=p_id_estado THEN
    v_resultado:=jsonb_build_object('id_usuario',p_id_usuario,'hubo_cambios',false);
    PERFORM private.completar_solicitud_rpc(p_id_solicitud,v_resultado); RETURN v_resultado;
  END IF;
  v_estado_anterior:=to_jsonb(v_registro_actual);
  UPDATE public.usuarios SET id_estado=p_id_estado WHERE id_usuario=p_id_usuario;
  v_estado_actual:=jsonb_build_object('id_usuario',p_id_usuario,'id_estado',p_id_estado);
  PERFORM private.registrar_auditoria_rbac(v_codigo_accion,v_estado_anterior::text,v_estado_actual::text,
    'Estado del usuario','usuarios',p_id_usuario,'Cambio de estado mediante RPC segura',p_id_solicitud);
  PERFORM private.crear_notificacion(v_codigo_notif,v_titulo_notif,
    CASE WHEN p_id_estado=1 THEN 'Se activó un usuario.' ELSE 'Se desactivó un usuario.' END,
    'informativa',(v_ctx->>'id_modulo')::integer,(v_ctx->>'id_accion')::integer,'usuarios',p_id_usuario,p_id_solicitud,NULL,
    jsonb_build_object('id_usuario',p_id_usuario));
  v_resultado:=jsonb_build_object('id_usuario',p_id_usuario,'hubo_cambios',true);
  PERFORM private.completar_solicitud_rpc(p_id_solicitud,v_resultado); RETURN v_resultado;
END;
$function$;

-- Ningún cliente puede mutar directamente los catálogos empresariales.
DO $do$
DECLARE v_tabla text; v_politica record; v_funcion record;
BEGIN
  FOREACH v_tabla IN ARRAY ARRAY['proveedores','fabricante','productos','categoria','usuarios'] LOOP
    FOR v_politica IN SELECT policyname FROM pg_policies
      WHERE schemaname='public' AND tablename=v_tabla AND cmd IN ('INSERT','UPDATE','DELETE') LOOP
      EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I',v_politica.policyname,v_tabla);
    END LOOP;
    EXECUTE format('REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON TABLE public.%I FROM PUBLIC, anon, authenticated',v_tabla);
  END LOOP;
  FOR v_funcion IN SELECT p.oid::regprocedure AS firma FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
    WHERE n.nspname='public' AND p.proname IN (
      'crear_proveedor_seguro','actualizar_proveedor_seguro','cambiar_estado_proveedor_seguro',
      'crear_fabricante_seguro','actualizar_fabricante_seguro','cambiar_estado_fabricante_seguro',
      'crear_producto_seguro','actualizar_producto_seguro','cambiar_estado_producto_seguro',
      'crear_categoria_seguro','actualizar_categoria_seguro','cambiar_estado_categoria_seguro',
      'crear_usuario_empleado_seguro','actualizar_usuario_seguro','cambiar_estado_usuario_seguro') LOOP
    EXECUTE format('REVOKE ALL ON FUNCTION %s FROM PUBLIC, anon',v_funcion.firma);
    EXECUTE format('GRANT EXECUTE ON FUNCTION %s TO authenticated',v_funcion.firma);
  END LOOP;
END $do$;

REVOKE ALL ON FUNCTION private.crear_notificacion(text,text,text,text,integer,integer,text,integer,uuid,text,jsonb)
  FROM PUBLIC, anon, authenticated;
