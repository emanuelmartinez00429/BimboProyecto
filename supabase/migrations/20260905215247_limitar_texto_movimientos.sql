-- ============================================================================
-- Migración: 20260905215247_limitar_texto_movimientos.sql  (aplicada 2026-09-05)
-- Propósito: Darle un tope real en la BD a los dos campos de texto que el
--            operador escribe al registrar un camión. Hasta ahora
--            `placa_vehiculo` era varchar SIN límite y `observaciones` era
--            `text`: la UI no tenía ningún número de la base que reflejar, así
--            que el tope quedaba inventado en la pantalla.
--            Mismo criterio que 20260902184000_limitar_columnas_texto_a_varchar_500.
-- Tablas afectadas:
--   - movimientos.placa_vehiculo  (varchar sin límite → varchar 20)
--   - movimientos.observaciones   (text             → varchar 500)
--
-- Verificado antes de aplicar (2026-09-05, 22 filas):
--   max(length(placa_vehiculo)) = 8   ·   max(length(observaciones)) = 46
--   0 filas superan los topes nuevos. Ningún dato existente se trunca.
--
-- El espejo en C# es `ReglasCamion` (CapaDominio/Reglas/ReglasEntidades.cs):
-- si acá cambia un número, ahí cambia también.
--
-- ── Por qué se recrea una vista ─────────────────────────────────────────────
-- Postgres rechaza ALTER COLUMN ... TYPE sobre una columna que una vista lee
-- ("cannot alter type of a column used by a view or rule"), aunque el tipo nuevo
-- sea compatible. `v_mov_productos_resumen` selecciona `m.placa_vehiculo`, así
-- que hay que soltarla y volver a crearla IDÉNTICA. Se conservan las dos cosas
-- que no viajan en el CREATE VIEW y que romperían seguridad si se perdieran:
--   · security_invoker = on  → la vista corre con los permisos de quien consulta,
--                              no del dueño; sin esto se saltearía RLS.
--   · los GRANT a anon / authenticated / service_role (los que tenía).
-- Nada más depende de esta vista (verificado en pg_depend).
-- ============================================================================

DROP VIEW IF EXISTS public.v_mov_productos_resumen;

ALTER TABLE public.movimientos
    ALTER COLUMN placa_vehiculo TYPE character varying(20);

ALTER TABLE public.movimientos
    ALTER COLUMN observaciones TYPE character varying(500);

CREATE VIEW public.v_mov_productos_resumen
WITH (security_invoker = on) AS
 SELECT mp.id_mov_producto,
    mp.id_movimiento,
    m.placa_vehiculo,
    pv.nombre_proveedor,
    p.nombre_producto,
    p.codigo_producto,
    mp.peso_manifestado,
    mp.bultos_teoricos,
    COALESCE(sum(e.peso_neto), 0::numeric) AS peso_recibido,
    COALESCE(sum(e.numero_bultos_recibido), 0::bigint) AS bultos_recibidos,
    mp.bultos_teoricos - COALESCE(sum(e.numero_bultos_recibido), 0::bigint) AS bultos_restantes,
        CASE
            WHEN mp.peso_manifestado > 0::numeric THEN round((mp.peso_manifestado - COALESCE(sum(e.peso_neto), 0::numeric)) / mp.peso_manifestado * 100::numeric, 2)
            ELSE 0::numeric
        END AS pct_peso_restante,
    COALESCE(sum(e.peso_neto), 0::numeric) - mp.peso_manifestado AS diferencia_kg,
        CASE
            WHEN mp.peso_manifestado > 0::numeric THEN round((COALESCE(sum(e.peso_neto), 0::numeric) - mp.peso_manifestado) / mp.peso_manifestado * 100::numeric, 2)
            ELSE 0::numeric
        END AS diferencia_pct,
    (COALESCE(sum(e.peso_neto), 0::numeric) - mp.peso_manifestado) * COALESCE(p.precio_por_kg, 0::numeric) AS diferencia_usd,
    count(e.id_pesaje) AS total_entradas,
    eg.tipo_estado AS estado
   FROM movimiento_productos mp
     JOIN movimientos m ON m.id_movimiento = mp.id_movimiento
     JOIN proveedores pv ON pv.id_proveedor = m.id_proveedor
     JOIN productos p ON p.id_producto = mp.id_producto
     JOIN estado_general eg ON eg.id_estado = mp.id_estado
     LEFT JOIN entradas_producto e ON e.id_mov_producto = mp.id_mov_producto
  GROUP BY mp.id_mov_producto, mp.id_movimiento, m.placa_vehiculo, pv.nombre_proveedor, p.nombre_producto, p.codigo_producto, mp.peso_manifestado, mp.bultos_teoricos, p.precio_por_kg, eg.tipo_estado;

GRANT ALL ON TABLE public.v_mov_productos_resumen TO anon;
GRANT ALL ON TABLE public.v_mov_productos_resumen TO authenticated;
GRANT ALL ON TABLE public.v_mov_productos_resumen TO service_role;
