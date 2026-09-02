-- ============================================================================
-- Migración: 20260902184000_limitar_columnas_texto_a_varchar_500.sql
-- Propósito: Alinear columnas de texto libre (text) a character varying(500)
--            para reflejar en la BD el tope defensivo de UI (evitar textos desmedidos).
-- Tablas afectadas:
--   - fabricante.descripcion_fabricante
--   - presentacion_producto.descripcion_presentacion
--   - proveedores.direccion_proveedor
--   - empresa.direccion_empresa
-- ============================================================================

ALTER TABLE public.fabricante
    ALTER COLUMN descripcion_fabricante TYPE character varying(500);

ALTER TABLE public.presentacion_producto
    ALTER COLUMN descripcion_presentacion TYPE character varying(500);

ALTER TABLE public.proveedores
    ALTER COLUMN direccion_proveedor TYPE character varying(500);

ALTER TABLE public.empresa
    ALTER COLUMN direccion_empresa TYPE character varying(500);
