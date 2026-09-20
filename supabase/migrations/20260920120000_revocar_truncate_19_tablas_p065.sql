-- P-065: Revocar privilegio TRUNCATE a roles no privilegiados (anon, authenticated, public)
-- en las 19 tablas de negocio del esquema public.
--
-- Supabase concede ALL por defecto a los roles `anon` y `authenticated` al crear tablas en `public`.
-- El privilegio ALL incluye TRUNCATE, y en PostgreSQL las políticas de Row Level Security (RLS)
-- NO aplican a comandos TRUNCATE (TRUNCATE es un privilegio de tabla puro a nivel de catálogo).
--
-- Esta migración revoca explícitamente el privilegio TRUNCATE a `anon`, `authenticated` y `public`
-- en las 19 tablas restantes, eliminando la brecha de seguridad P-065 sin afectar las operaciones
-- legítimas de la aplicación (SELECT, INSERT, UPDATE, DELETE).

revoke truncate on table
  public.bitacora,
  public.contactos_fabricante,
  public.contactos_proveedor,
  public.empleados,
  public.empresa,
  public.entradas_producto,
  public.estado_general,
  public.fabricantes_pais,
  public.modulos,
  public.movimiento_productos,
  public.movimientos,
  public.paises,
  public.productos_paises,
  public.proveedores_paises,
  public.reporteria,
  public.tara,
  public.tarima,
  public.tipo_unidad,
  public.unidad_medida
from anon, authenticated, public;
