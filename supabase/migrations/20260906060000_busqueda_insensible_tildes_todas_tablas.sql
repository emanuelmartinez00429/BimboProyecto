-- ============================================================================
-- Migración: Búsqueda insensible a mayúsculas y tildes en todas las tablas (P-039)
-- Fecha: 2026-09-06
-- Tablas: fabricante, proveedores, categoria, presentacion_producto,
--         empleados, usuarios, bitacora, vista_usuarios_busqueda
-- ADR: ADR-018
-- ============================================================================

-- Asegurar función sin_tildes
CREATE OR REPLACE FUNCTION public.sin_tildes(txt text)
 RETURNS text
 LANGUAGE sql
 IMMUTABLE PARALLEL SAFE
AS $function$
    SELECT lower(extensions.unaccent('extensions.unaccent'::regdictionary, coalesce(txt, '')))
$function$;

-- 1. Fabricante
ALTER TABLE public.fabricante
  ADD COLUMN IF NOT EXISTS busqueda_fabricante text
  GENERATED ALWAYS AS (sin_tildes(coalesce(nombre_fabricante, '') || ' ' || coalesce(descripcion_fabricante, ''))) STORED;

CREATE INDEX IF NOT EXISTS idx_fabricante_busqueda ON public.fabricante USING gin (busqueda_fabricante gin_trgm_ops);

-- 2. Proveedores
ALTER TABLE public.proveedores
  ADD COLUMN IF NOT EXISTS busqueda_proveedor text
  GENERATED ALWAYS AS (sin_tildes(coalesce(nombre_proveedor, '') || ' ' || coalesce(rtn_proveedor, '') || ' ' || coalesce(correo_proveedor, ''))) STORED;

CREATE INDEX IF NOT EXISTS idx_proveedores_busqueda ON public.proveedores USING gin (busqueda_proveedor gin_trgm_ops);

-- 3. Categoria
ALTER TABLE public.categoria
  ADD COLUMN IF NOT EXISTS busqueda_categoria text
  GENERATED ALWAYS AS (sin_tildes(coalesce(nombre_categoria, '') || ' ' || coalesce(descripcion_categoria, ''))) STORED;

CREATE INDEX IF NOT EXISTS idx_categoria_busqueda ON public.categoria USING gin (busqueda_categoria gin_trgm_ops);

-- 4. Presentacion Producto
ALTER TABLE public.presentacion_producto
  ADD COLUMN IF NOT EXISTS busqueda_presentacion text
  GENERATED ALWAYS AS (sin_tildes(coalesce(nombre_presentacion, '') || ' ' || coalesce(descripcion_presentacion, ''))) STORED;

CREATE INDEX IF NOT EXISTS idx_presentacion_busqueda ON public.presentacion_producto USING gin (busqueda_presentacion gin_trgm_ops);

-- 5. Empleados
ALTER TABLE public.empleados
  ADD COLUMN IF NOT EXISTS busqueda_empleado text
  GENERATED ALWAYS AS (sin_tildes(coalesce(nombre_empleado, '') || ' ' || coalesce(apellido_empleado, '') || ' ' || coalesce(numero_identidad, '') || ' ' || coalesce(correo_empleado, ''))) STORED;

CREATE INDEX IF NOT EXISTS idx_empleados_busqueda ON public.empleados USING gin (busqueda_empleado gin_trgm_ops);

-- 6. Usuarios
ALTER TABLE public.usuarios
  ADD COLUMN IF NOT EXISTS busqueda_usuario text
  GENERATED ALWAYS AS (sin_tildes(coalesce(alias_usuario, ''))) STORED;

CREATE INDEX IF NOT EXISTS idx_usuarios_busqueda ON public.usuarios USING gin (busqueda_usuario gin_trgm_ops);

-- 6b. Vista usuarios con busqueda que incluye alias y empleado
CREATE OR REPLACE VIEW public.vista_usuarios_busqueda WITH (security_invoker = on) AS
SELECT u.id_usuario,
    u.uuid_usuario,
    u.alias_usuario,
    u.id_empleado,
    u.id_rol,
    u.id_estado,
    u.ultimo_acceso,
    u.created_at,
    u.updated_at,
    TRIM(BOTH FROM (COALESCE(e.nombre_empleado, ''::character varying)::text || ' '::text) || COALESCE(e.apellido_empleado, ''::character varying)::text) AS nombre_completo,
    sin_tildes(coalesce(u.alias_usuario, '') || ' ' || coalesce(e.nombre_empleado, '') || ' ' || coalesce(e.apellido_empleado, '')) AS busqueda_usuario
   FROM usuarios u
     LEFT JOIN empleados e USING (id_empleado);

-- 7. Bitacora
ALTER TABLE public.bitacora
  ADD COLUMN IF NOT EXISTS busqueda_bitacora text
  GENERATED ALWAYS AS (sin_tildes(coalesce(tabla_afectada, '') || ' ' || coalesce(campo_afectado, '') || ' ' || coalesce(estado_actual, '') || ' ' || coalesce(estado_anterior, ''))) STORED;

CREATE INDEX IF NOT EXISTS idx_bitacora_busqueda ON public.bitacora USING gin (busqueda_bitacora gin_trgm_ops);
