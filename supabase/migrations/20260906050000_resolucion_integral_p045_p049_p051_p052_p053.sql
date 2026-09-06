-- ============================================================================
-- Migración: 20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql
-- Propósito:
--   1. P-049: Agregar contactos_fabricante y contactos_proveedor a supabase_realtime.
--   2. P-051: Cerrar política RLS permisiva en public.usuarios con USUARIOS_CONSULTAR / USUARIOS_VER.
--   3. P-052: Retirar triggers legacy de actualización en categoria, fabricante y proveedores.
--   4. P-053: Crear RPC transaccional registrar_camiones_lote_seguro y alias registrar_camiones_lote.
--   5. P-045: Fijar tope físico de varchar(500) en movimiento_productos y entradas_producto.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. P-049: Realtime Publication
-- ----------------------------------------------------------------------------
ALTER PUBLICATION supabase_realtime ADD TABLE public.contactos_fabricante, public.contactos_proveedor;

-- ----------------------------------------------------------------------------
-- 2. P-051: Política RLS en public.usuarios
-- ----------------------------------------------------------------------------
DROP POLICY IF EXISTS "select_Usuarios" ON public.usuarios;

CREATE POLICY "select_Usuarios" ON public.usuarios
    FOR SELECT
    USING (
        uuid_usuario = auth.uid()
        OR private.usuario_tiene_permiso_codigo('USUARIOS_CONSULTAR')
        OR private.usuario_tiene_permiso_codigo('USUARIOS_VER')
    );

-- ----------------------------------------------------------------------------
-- 3. P-052: Retiro de Triggers Legacy de Catálogos
-- ----------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_upd_categoria ON public.categoria;
DROP TRIGGER IF EXISTS trg_upd_fabricante ON public.fabricante;
DROP TRIGGER IF EXISTS trg_upd_proveedor ON public.proveedores;
DROP TRIGGER IF EXISTS trg_upd_proveedores ON public.proveedores;

DROP FUNCTION IF EXISTS public.log_upd_categoria();
DROP FUNCTION IF EXISTS public.log_upd_fabricante();
DROP FUNCTION IF EXISTS public.log_upd_proveedor();

-- ----------------------------------------------------------------------------
-- 4. P-045: Topes de Texto en Pesaje
-- ----------------------------------------------------------------------------
ALTER TABLE public.movimiento_productos
    ALTER COLUMN observaciones TYPE character varying(500);

ALTER TABLE public.entradas_producto
    ALTER COLUMN observaciones TYPE character varying(500);

-- ----------------------------------------------------------------------------
-- 5. P-053: RPC Transaccional registrar_camiones_lote_seguro
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.registrar_camiones_lote_seguro(
    p_camiones jsonb,
    p_id_solicitud uuid,
    p_id_operacion uuid DEFAULT NULL::uuid
) RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
DECLARE
    v_ctx jsonb;
    v_usuario integer;
    v_accion integer;
    v_elem jsonb;
    v_id_proveedor integer;
    v_placa text;
    v_obs text;
    v_id_movimiento integer;
    v_primer_id integer := NULL;
    v_ids_movimiento integer[] := '{}';
    v_count integer := 0;
    v_resultado jsonb;
BEGIN
    IF p_id_solicitud IS NULL THEN
        RAISE EXCEPTION 'id_solicitud es obligatorio' USING ERRCODE = '22004';
    END IF;

    IF p_camiones IS NULL OR jsonb_typeof(p_camiones) <> 'array' OR jsonb_array_length(p_camiones) = 0 THEN
        RAISE EXCEPTION 'El lote de camiones debe ser un arreglo JSON no vacío' USING ERRCODE = '22023';
    END IF;

    -- Validar autenticación, rol e idempotencia (modulo 3, accion 'Registrar Entrada' = id_accion 9)
    v_ctx := private.preparar_solicitud_pesaje(
        p_id_solicitud,
        p_id_operacion,
        'registrar_camiones_lote_seguro',
        'Registrar Entrada',
        jsonb_build_object('camiones', p_camiones)
    );

    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    v_usuario := (v_ctx->>'id_usuario')::integer;
    v_accion  := (v_ctx->>'id_accion')::integer;

    FOR v_elem IN SELECT * FROM jsonb_array_elements(p_camiones)
    LOOP
        v_id_proveedor := COALESCE((v_elem->>'id_proveedor')::integer, (v_elem->>'idProveedor')::integer, 0);
        v_placa := btrim(COALESCE(v_elem->>'placa_vehiculo', v_elem->>'placa', ''));
        v_obs := nullif(btrim(COALESCE(v_elem->>'observaciones', '')), '');

        IF v_id_proveedor <= 0 THEN
            RAISE EXCEPTION 'Proveedor inválido en lote' USING ERRCODE = '22023';
        END IF;

        IF length(v_placa) = 0 THEN
            RAISE EXCEPTION 'La placa del vehículo es obligatoria' USING ERRCODE = '22023';
        END IF;

        IF length(v_placa) > 20 THEN
            RAISE EXCEPTION 'La placa % excede el límite de 20 caracteres', v_placa USING ERRCODE = '22023';
        END IF;

        IF length(COALESCE(v_obs, '')) > 500 THEN
            RAISE EXCEPTION 'Las observaciones del camión % superan los 500 caracteres', v_placa USING ERRCODE = '22023';
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM public.proveedores p 
            WHERE p.id_proveedor = v_id_proveedor AND p.id_estado = 1
        ) THEN
            RAISE EXCEPTION 'El proveedor % no existe o está inactivo', v_id_proveedor USING ERRCODE = '22023';
        END IF;

        INSERT INTO public.movimientos (
            id_proveedor,
            placa_vehiculo,
            fecha_asignacion,
            id_usuario,
            id_estado,
            observaciones,
            peso_tara_extra
        ) VALUES (
            v_id_proveedor,
            v_placa,
            (clock_timestamp() AT TIME ZONE 'America/Tegucigalpa')::date,
            v_usuario,
            7, -- Abierto
            v_obs,
            0
        ) RETURNING id_movimiento INTO v_id_movimiento;

        IF v_primer_id IS NULL THEN
            v_primer_id := v_id_movimiento;
        END IF;

        v_ids_movimiento := array_append(v_ids_movimiento, v_id_movimiento);
        v_count := v_count + 1;

        PERFORM private.bitacora_pesaje(
            v_usuario,
            'Sin registro',
            format('Recepcion %s abierta en lote para proveedor %s, placa %s', v_id_movimiento, v_id_proveedor, v_placa),
            'Registro de recepcion',
            'Estado: Abierto',
            v_accion,
            'movimientos',
            v_id_movimiento,
            p_id_solicitud
        );
    END LOOP;

    v_resultado := jsonb_build_object(
        'creados', v_count,
        'primer_id', v_primer_id,
        'ids_movimiento', to_jsonb(v_ids_movimiento)
    );

    PERFORM private.completar_solicitud_pesaje(p_id_solicitud, v_resultado);

    RETURN v_resultado;
END;
$$;

CREATE OR REPLACE FUNCTION public.registrar_camiones_lote(
    p_camiones jsonb,
    p_id_solicitud uuid,
    p_id_operacion uuid DEFAULT NULL::uuid
) RETURNS jsonb
LANGUAGE sql
SECURITY DEFINER
SET search_path TO 'pg_catalog', 'public', 'private', 'auth'
AS $$
    SELECT public.registrar_camiones_lote_seguro($1, $2, $3);
$$;

REVOKE ALL ON FUNCTION public.registrar_camiones_lote_seguro(jsonb, uuid, uuid) FROM public, anon;
GRANT EXECUTE ON FUNCTION public.registrar_camiones_lote_seguro(jsonb, uuid, uuid) TO authenticated, service_role;

REVOKE ALL ON FUNCTION public.registrar_camiones_lote(jsonb, uuid, uuid) FROM public, anon;
GRANT EXECUTE ON FUNCTION public.registrar_camiones_lote(jsonb, uuid, uuid) TO authenticated, service_role;
