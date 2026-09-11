-- Recuperación de contraseña: antes de enviar el código OTP, chequear si la cuenta
-- está deshabilitada, SIN requerir prueba de dueño (correo + contraseña, o correo +
-- OTP), que es lo que exigen los chequeos existentes de id_estado (AuthService.LoginAsync,
-- VerificarCodigoAsync). Función nueva y separada a propósito: no reutiliza
-- crear_usuario_empleado_seguro (esa exige permiso USUARIOS_CREAR y tiene efecto
-- secundario de inserción; esta es de solo lectura y anonima).
--
-- Diseñada para no ampliar la enumeración de cuentas: si el correo no existe en
-- auth.users, o existe mas no tiene fila en public.usuarios, devuelve TRUE igual
-- que una cuenta habilitada (deja pasar el flujo normal). Solo devuelve FALSE
-- cuando el correo corresponde a una cuenta que SI existe en la app Y esta
-- deshabilitada (id_estado <> 1). El mensaje que ve el usuario en la capa C#/UI
-- es deliberadamente generico ("hubo un error, contacta a soporte"), no
-- "cuenta deshabilitada", para no delatar el motivo especifico a quien esté
-- probando correos al azar.

CREATE OR REPLACE FUNCTION public.verificar_cuenta_habilitada_por_correo(p_correo text)
RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'auth'
AS $function$
DECLARE
  v_user_id uuid;
  v_id_estado integer;
BEGIN
  SELECT au.id INTO v_user_id
  FROM auth.users au
  WHERE lower(au.email) = lower(btrim(p_correo))
  ORDER BY au.created_at DESC
  LIMIT 1;

  IF v_user_id IS NULL THEN
    RETURN true;
  END IF;

  SELECT u.id_estado INTO v_id_estado
  FROM public.usuarios u
  WHERE u.uuid_usuario = v_user_id;

  IF v_id_estado IS NULL THEN
    RETURN true;
  END IF;

  RETURN v_id_estado = 1;
END;
$function$;

GRANT EXECUTE ON FUNCTION public.verificar_cuenta_habilitada_por_correo(text) TO anon, authenticated;
