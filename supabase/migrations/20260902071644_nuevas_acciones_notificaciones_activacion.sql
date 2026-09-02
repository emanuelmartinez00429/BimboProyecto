-- =====================================================================================
-- Migración: Nuevas Acciones RBAC y Tipos de Notificación para Activaciones
-- =====================================================================================
-- Añade códigos de acción para el módulo de Categorías y tipos de notificación
-- para eventos de activación de registros (Proveedores, Fabricantes, Productos, etc.)
-- =====================================================================================

-- 1. Añadir acciones para Categorías al Módulo 1 (Gestión de Inventario)
INSERT INTO public.acciones (nombre_accion, descripcion_accion, codigo_accion, id_modulo)
VALUES
    ('Crear Categoría', 'Permite crear nuevas categorías de productos', 'CATEGORIAS_CREAR', 1),
    ('Modificar Categoría', 'Permite modificar categorías de productos', 'CATEGORIAS_MODIFICAR', 1),
    ('Desactivar Categoría', 'Permite desactivar categorías de productos', 'CATEGORIAS_DESACTIVAR', 1),
    ('Activar Categoría', 'Permite activar categorías de productos', 'CATEGORIAS_ACTIVAR', 1)
ON CONFLICT (nombre_accion, id_modulo) DO NOTHING;

-- 2. Añadir tipos de notificación para Activaciones
INSERT INTO public.tipos_notificacion (codigo, nombre, descripcion, severidad_predeterminada, id_estado)
VALUES
    ('PROVEEDOR_ACTIVADO', 'Proveedor activado', 'Se ha reactivado un proveedor', 'informativa', 1),
    ('FABRICANTE_ACTIVADO', 'Fabricante activado', 'Se ha reactivado un fabricante', 'informativa', 1),
    ('PRODUCTO_ACTIVADO', 'Producto activado', 'Se ha reactivado un producto', 'informativa', 1),
    ('CATEGORIA_ACTIVADA', 'Categoría activada', 'Se ha reactivado una categoría', 'informativa', 1),
    ('USUARIO_ACTIVADO', 'Usuario activado', 'Se ha reactivado un usuario', 'informativa', 1)
ON CONFLICT (codigo) DO NOTHING;
