-- ============================================================================
-- Migración: 20260917132500_consultar_kpis_pesajes.sql
-- Propósito:
--   Crear función RPC consultar_kpis_pesajes para el Dashboard en una sola pasada
--   con SECURITY INVOKER y search_path = ''.
-- ============================================================================

CREATE OR REPLACE FUNCTION public.consultar_kpis_pesajes(
    p_fecha_desde_actual date,
    p_fecha_hasta_actual date,
    p_fecha_desde_anterior date,
    p_fecha_hasta_anterior date
)
RETURNS TABLE (
    pesajes_actual integer,
    pesajes_anterior integer,
    neto_actual numeric,
    neto_anterior numeric,
    teorico_actual numeric,
    recibido_actual numeric
)
LANGUAGE sql
SECURITY INVOKER
SET search_path = ''
AS $$
    with resumen_movimientos as (
        select
            mp.id_mov_producto,
            coalesce(mp.peso_manifestado, 0) as peso_manifestado,
            count(e.id_pesaje) filter (where e.fecha_entrada between p_fecha_desde_actual and p_fecha_hasta_actual) as pesajes_actual,
            count(e.id_pesaje) filter (where e.fecha_entrada between p_fecha_desde_anterior and p_fecha_hasta_anterior) as pesajes_anterior,
            coalesce(sum(e.peso_neto) filter (where e.fecha_entrada between p_fecha_desde_actual and p_fecha_hasta_actual), 0) as neto_actual,
            coalesce(sum(e.peso_neto) filter (where e.fecha_entrada between p_fecha_desde_anterior and p_fecha_hasta_anterior), 0) as neto_anterior
        from public.entradas_producto e
        left join public.movimiento_productos mp on mp.id_mov_producto = e.id_mov_producto and mp.id_estado <> 9
        where e.id_estado <> 9
          and (
              (e.fecha_entrada between p_fecha_desde_actual and p_fecha_hasta_actual)
              or
              (e.fecha_entrada between p_fecha_desde_anterior and p_fecha_hasta_anterior)
          )
        group by mp.id_mov_producto, mp.peso_manifestado
    )
    select
        coalesce(sum(pesajes_actual), 0)::integer as pesajes_actual,
        coalesce(sum(pesajes_anterior), 0)::integer as pesajes_anterior,
        coalesce(sum(neto_actual), 0)::numeric as neto_actual,
        coalesce(sum(neto_anterior), 0)::numeric as neto_anterior,
        coalesce(sum(peso_manifestado) filter (where pesajes_actual > 0), 0)::numeric as teorico_actual,
        coalesce(sum(neto_actual), 0)::numeric as recibido_actual
    from resumen_movimientos;
$$;

GRANT EXECUTE ON FUNCTION public.consultar_kpis_pesajes(date, date, date, date) TO authenticated, anon, service_role;
