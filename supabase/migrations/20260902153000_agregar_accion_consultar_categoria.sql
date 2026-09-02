-- Completa el contrato RBAC de Categorías. La UI ya distingue la consulta de
-- las operaciones de mantenimiento y valida mediante codigo_accion estable.
INSERT INTO public.acciones (
    nombre_accion,
    descripcion_accion,
    codigo_accion,
    id_modulo
)
SELECT
    'Consultar Categoría',
    'Permite consultar el catálogo de categorías de productos',
    'CATEGORIAS_CONSULTAR',
    m.id_modulo
FROM public.modulos AS m
WHERE m.nombre_modulo = 'Gestión de Inventario'
  AND NOT EXISTS (
      SELECT 1
      FROM public.acciones AS a
      WHERE a.codigo_accion = 'CATEGORIAS_CONSULTAR'
  );

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM public.acciones
        WHERE codigo_accion = 'CATEGORIAS_CONSULTAR'
    ) THEN
        RAISE EXCEPTION
            'No se pudo crear CATEGORIAS_CONSULTAR: no existe el módulo Gestión de Inventario.';
    END IF;
END;
$$;
