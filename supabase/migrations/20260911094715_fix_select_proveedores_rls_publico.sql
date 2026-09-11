-- P-063: select_Proveedores estaba en USING (true) para el rol "public" — sin login
-- y sin chequeo de permiso, cualquiera podia leer RTN/telefono/correo/direccion de
-- todos los proveedores. Se restringe a quienes tengan el permiso dedicado
-- (PROVEEDORES_CONSULTAR) o necesiten elegir un proveedor en el flujo real de la app
-- (el selector de catalogo de ProductoModal/FabricanteModal llama a
-- GetProveedoresAsync, que lee esta misma tabla incluyendo rtn_proveedor, para
-- cualquiera con permiso de crear/modificar productos o fabricantes).

DROP POLICY IF EXISTS "select_Proveedores" ON public.proveedores;

CREATE POLICY "select_Proveedores" ON public.proveedores
FOR SELECT
TO public
USING (
  private.usuario_tiene_permiso_codigo('PROVEEDORES_CONSULTAR'::text)
  OR private.usuario_tiene_permiso_codigo('PRODUCTOS_CREAR'::text)
  OR private.usuario_tiene_permiso_codigo('PRODUCTOS_MODIFICAR'::text)
  OR private.usuario_tiene_permiso_codigo('FABRICANTES_CREAR'::text)
  OR private.usuario_tiene_permiso_codigo('FABRICANTES_MODIFICAR'::text)
);
