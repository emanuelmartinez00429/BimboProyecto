-- ============================================================================
-- Migración: 20260906180000_rpc_productos_lote.sql
-- Propósito:
--   RPC transaccional para guardar de una sola vez los productos de una
--   recepción: altas, cambios de peso/bultos/observaciones y bajas.
--
--   Hasta ahora los productos eran el ÚNICO camino de escritura del módulo de
--   Pesaje sin RPC: `PesajeRepository.AgregarProductoAsync`,
--   `ActualizarProductoAsync` y `AnularProductoAsync` hacían Insert/Update
--   crudos de PostgREST — sin bitácora, sin `p_id_solicitud` y sin validación
--   en servidor. Con el modal nuevo (tabla de toda la carga) eso además serían
--   N escrituras sueltas por cada "Finalizar", exactamente la familia de deuda
--   que ya cerraron P-032 (reparto de tara extra) y P-053 (alta de camiones).
--
--   Molde: `registrar_camiones_lote_seguro`
--   (20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql).
--
-- Permisos (módulo 3):
--   altas   → 'Registrar Entrada'   cambios → 'Modificar Pesaje'
--   bajas   → 'Cancelar Pesaje'
--   El permiso base que consume la idempotencia es el de la operación
--   principal presente en el lote; los otros se exigen aparte solo si ese
--   tipo de operación viene en el payload. Así, editar un peso no obliga a
--   tener permiso de alta, ni viceversa.
-- ============================================================================

CREATE OR REPLACE FUNCTION public.registrar_productos_lote_seguro(
    p_id_movimiento integer,
    p_altas   jsonb,
    p_cambios jsonb,
    p_bajas   jsonb,
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
    v_accion_alta integer;
    v_accion_cambio integer;
    v_accion_baja integer;
    v_permiso_base text;

    v_altas   jsonb := coalesce(p_altas,   '[]'::jsonb);
    v_cambios jsonb := coalesce(p_cambios, '[]'::jsonb);
    v_bajas   jsonb := coalesce(p_bajas,   '[]'::jsonb);
    v_n_altas   integer;
    v_n_cambios integer;
    v_n_bajas   integer;

    v_mov record;
    v_elem jsonb;
    v_old record;
    v_new record;
    v_filas integer;

    v_id_producto integer;
    v_id_mov_producto integer;
    v_peso numeric;
    v_bultos integer;
    v_obs text;

    v_creados integer := 0;
    v_actualizados integer := 0;
    v_anulados integer := 0;
    v_ids_creados integer[] := '{}';
    v_resultado jsonb;
BEGIN
    IF p_id_solicitud IS NULL THEN
        RAISE EXCEPTION 'id_solicitud es obligatorio' USING ERRCODE = '22004';
    END IF;

    IF jsonb_typeof(v_altas) <> 'array' OR jsonb_typeof(v_cambios) <> 'array' OR jsonb_typeof(v_bajas) <> 'array' THEN
        RAISE EXCEPTION 'Las altas, cambios y bajas deben ser arreglos JSON' USING ERRCODE = '22023';
    END IF;

    v_n_altas   := jsonb_array_length(v_altas);
    v_n_cambios := jsonb_array_length(v_cambios);
    v_n_bajas   := jsonb_array_length(v_bajas);

    IF v_n_altas + v_n_cambios + v_n_bajas = 0 THEN
        RAISE EXCEPTION 'El lote no trae ninguna operación' USING ERRCODE = '22023';
    END IF;

    -- El permiso base es el de la operación principal del lote. No se fuerza
    -- 'Registrar Entrada' siempre: un lote que solo corrige pesos tiene que
    -- poder ejecutarlo alguien con 'Modificar Pesaje' y nada más.
    v_permiso_base := CASE
        WHEN v_n_altas   > 0 THEN 'Registrar Entrada'
        WHEN v_n_cambios > 0 THEN 'Modificar Pesaje'
        ELSE 'Cancelar Pesaje'
    END;

    v_ctx := private.preparar_solicitud_pesaje(
        p_id_solicitud, p_id_operacion,
        'registrar_productos_lote_seguro', v_permiso_base,
        jsonb_build_object('id_movimiento', p_id_movimiento,
                           'altas', v_altas, 'cambios', v_cambios, 'bajas', v_bajas));

    IF (v_ctx->>'es_reintento')::boolean THEN
        RETURN v_ctx->'resultado';
    END IF;

    v_usuario := (v_ctx->>'id_usuario')::integer;

    -- La acción del permiso base ya vino resuelta; las otras se resuelven acá
    -- para que cada renglón de bitácora quede con SU acción y no con la del
    -- alta. La consulta es la misma que usa preparar_solicitud_pesaje, así que
    -- un NULL significa exactamente "el rol no tiene ese permiso".
    IF v_n_altas > 0 THEN
        v_accion_alta := (v_ctx->>'id_accion')::integer;
    END IF;

    IF v_n_cambios > 0 THEN
        IF v_permiso_base = 'Modificar Pesaje' THEN
            v_accion_cambio := (v_ctx->>'id_accion')::integer;
        ELSE
            SELECT a.id_accion INTO v_accion_cambio
              FROM public.usuarios u
              JOIN public.acciones_roles ar ON ar.id_rol = u.id_rol AND ar.id_estado = 1
              JOIN public.acciones a ON a.id_accion = ar.id_accion
             WHERE u.id_usuario = v_usuario AND a.id_modulo = 3 AND a.nombre_accion = 'Modificar Pesaje';
            IF v_accion_cambio IS NULL THEN
                RAISE EXCEPTION 'Permiso RBAC requerido: Modificar Pesaje' USING ERRCODE = '42501';
            END IF;
        END IF;
    END IF;

    IF v_n_bajas > 0 THEN
        IF v_permiso_base = 'Cancelar Pesaje' THEN
            v_accion_baja := (v_ctx->>'id_accion')::integer;
        ELSE
            SELECT a.id_accion INTO v_accion_baja
              FROM public.usuarios u
              JOIN public.acciones_roles ar ON ar.id_rol = u.id_rol AND ar.id_estado = 1
              JOIN public.acciones a ON a.id_accion = ar.id_accion
             WHERE u.id_usuario = v_usuario AND a.id_modulo = 3 AND a.nombre_accion = 'Cancelar Pesaje';
            IF v_accion_baja IS NULL THEN
                RAISE EXCEPTION 'Permiso RBAC requerido: Cancelar Pesaje' USING ERRCODE = '42501';
            END IF;
        END IF;
    END IF;

    -- Un solo lock sobre la recepción: todo el lote pertenece a ella, así que
    -- dos operarios sobre el mismo camión se serializan acá y no fila por fila.
    SELECT * INTO v_mov FROM public.movimientos
     WHERE id_movimiento = p_id_movimiento FOR UPDATE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'La recepción no existe' USING ERRCODE = '22023';
    END IF;
    IF v_mov.id_estado <> 7 THEN
        RAISE EXCEPTION 'Solo se pueden modificar los productos de una recepción abierta' USING ERRCODE = '22023';
    END IF;

    -- ── ALTAS ───────────────────────────────────────────────────────────────
    FOR v_elem IN SELECT * FROM jsonb_array_elements(v_altas)
    LOOP
        v_id_producto := (v_elem->>'id_producto')::integer;
        v_peso        := (v_elem->>'peso_manifestado')::numeric;
        v_bultos      := (v_elem->>'bultos_teoricos')::integer;
        v_obs         := nullif(btrim(coalesce(v_elem->>'observaciones', '')), '');

        IF coalesce(v_peso, 0) <= 0 OR coalesce(v_bultos, 0) <= 0 THEN
            RAISE EXCEPTION 'Peso manifestado y bultos deben ser positivos (producto %)', v_id_producto USING ERRCODE = '22023';
        END IF;
        IF length(coalesce(v_obs, '')) > 500 THEN
            RAISE EXCEPTION 'Las observaciones del producto % superan los 500 caracteres', v_id_producto USING ERRCODE = '22023';
        END IF;

        -- Mismo control que la rama ingresar_producto_recepcion_pesaje_tabla_bitacora:
        -- el producto tiene que ser del proveedor de ESTA recepción y estar activo.
        IF NOT EXISTS (
            SELECT 1 FROM public.productos p
              JOIN public.fabricante f ON f.id_fabricante = p.id_fabricante
             WHERE p.id_producto = v_id_producto AND p.id_estado = 1
               AND f.id_proveedor = v_mov.id_proveedor AND f.id_estado = 1
        ) THEN
            RAISE EXCEPTION 'El producto % no pertenece al proveedor de la recepción o está inactivo', v_id_producto USING ERRCODE = '22023';
        END IF;

        INSERT INTO public.movimiento_productos(
            id_movimiento, id_producto, peso_manifestado, bultos_teoricos, id_estado, observaciones)
        VALUES (p_id_movimiento, v_id_producto, v_peso, v_bultos, 7, v_obs)
        RETURNING * INTO v_new;

        v_ids_creados := array_append(v_ids_creados, v_new.id_mov_producto);
        v_creados := v_creados + 1;

        PERFORM private.bitacora_pesaje(
            v_usuario, 'Sin registro',
            format('Producto %s agregado a recepcion %s; peso manifestado %s kg; bultos %s',
                   v_id_producto, p_id_movimiento, v_peso, v_bultos),
            'Registro de producto', 'Estado: Abierto',
            v_accion_alta, 'movimiento_productos', v_new.id_mov_producto, p_id_solicitud);
    END LOOP;

    -- ── CAMBIOS ─────────────────────────────────────────────────────────────
    FOR v_elem IN SELECT * FROM jsonb_array_elements(v_cambios)
    LOOP
        v_id_mov_producto := (v_elem->>'id_mov_producto')::integer;
        v_peso            := (v_elem->>'peso_manifestado')::numeric;
        v_bultos          := (v_elem->>'bultos_teoricos')::integer;
        v_obs             := nullif(btrim(coalesce(v_elem->>'observaciones', '')), '');

        IF coalesce(v_peso, 0) <= 0 OR coalesce(v_bultos, 0) <= 0 THEN
            RAISE EXCEPTION 'Peso manifestado y bultos deben ser positivos (producto de recepcion %)', v_id_mov_producto USING ERRCODE = '22023';
        END IF;
        IF length(coalesce(v_obs, '')) > 500 THEN
            RAISE EXCEPTION 'Las observaciones del producto de recepcion % superan los 500 caracteres', v_id_mov_producto USING ERRCODE = '22023';
        END IF;

        SELECT * INTO v_old FROM public.movimiento_productos
         WHERE id_mov_producto = v_id_mov_producto FOR UPDATE;

        IF NOT FOUND OR v_old.id_movimiento <> p_id_movimiento THEN
            RAISE EXCEPTION 'El producto % no pertenece a esta recepcion', v_id_mov_producto USING ERRCODE = '22023';
        END IF;
        IF v_old.id_estado <> 7 THEN
            RAISE EXCEPTION 'Solo se puede modificar un producto abierto (producto %)', v_id_mov_producto USING ERRCODE = '22023';
        END IF;

        UPDATE public.movimiento_productos
           SET peso_manifestado = v_peso, bultos_teoricos = v_bultos, observaciones = v_obs
         WHERE id_mov_producto = v_id_mov_producto
        RETURNING * INTO v_new;

        GET DIAGNOSTICS v_filas = ROW_COUNT;
        IF v_filas <> 1 THEN
            RAISE EXCEPTION 'No se actualizo exactamente un producto' USING ERRCODE = '22023';
        END IF;

        v_actualizados := v_actualizados + 1;

        PERFORM private.bitacora_pesaje(
            v_usuario,
            format('Peso %s kg; bultos %s; observaciones %s',
                   v_old.peso_manifestado, v_old.bultos_teoricos, coalesce(v_old.observaciones, '')),
            format('Peso %s kg; bultos %s; observaciones %s',
                   v_new.peso_manifestado, v_new.bultos_teoricos, coalesce(v_new.observaciones, '')),
            'Manifiesto y observaciones', 'Producto de recepcion abierto',
            v_accion_cambio, 'movimiento_productos', v_new.id_mov_producto, p_id_solicitud);
    END LOOP;

    -- ── BAJAS ───────────────────────────────────────────────────────────────
    FOR v_elem IN SELECT * FROM jsonb_array_elements(v_bajas)
    LOOP
        v_id_mov_producto := (v_elem#>>'{}')::integer;

        SELECT * INTO v_old FROM public.movimiento_productos
         WHERE id_mov_producto = v_id_mov_producto FOR UPDATE;

        IF NOT FOUND OR v_old.id_movimiento <> p_id_movimiento THEN
            RAISE EXCEPTION 'El producto % no pertenece a esta recepcion', v_id_mov_producto USING ERRCODE = '22023';
        END IF;
        IF v_old.id_estado = 9 THEN
            CONTINUE;   -- ya estaba anulado: el lote es idempotente por fila
        END IF;

        -- Mismo guard que la rama cambiar_estado_producto_pesaje_tabla_bitacora:
        -- un producto con pesajes vivos es historial, no se quita.
        IF EXISTS (
            SELECT 1 FROM public.entradas_producto e
             WHERE e.id_mov_producto = v_id_mov_producto AND e.id_estado <> 9
        ) THEN
            RAISE EXCEPTION 'No se puede quitar un producto con pesajes activos (producto %)', v_id_mov_producto USING ERRCODE = '22023';
        END IF;

        UPDATE public.movimiento_productos SET id_estado = 9
         WHERE id_mov_producto = v_id_mov_producto
        RETURNING * INTO v_new;

        v_anulados := v_anulados + 1;

        PERFORM private.bitacora_pesaje(
            v_usuario, format('Estado %s', v_old.id_estado), 'Estado 9',
            'Estado de producto', 'Producto quitado de la carga',
            v_accion_baja, 'movimiento_productos', v_new.id_mov_producto, p_id_solicitud);
    END LOOP;

    v_resultado := jsonb_build_object(
        'creados',       v_creados,
        'actualizados',  v_actualizados,
        'anulados',      v_anulados,
        'ids_creados',   to_jsonb(v_ids_creados));

    RETURN private.completar_solicitud_pesaje(p_id_solicitud, v_resultado);
END;
$$;

REVOKE ALL ON FUNCTION public.registrar_productos_lote_seguro(integer, jsonb, jsonb, jsonb, uuid, uuid) FROM public, anon;
GRANT EXECUTE ON FUNCTION public.registrar_productos_lote_seguro(integer, jsonb, jsonb, jsonb, uuid, uuid) TO authenticated, service_role;
