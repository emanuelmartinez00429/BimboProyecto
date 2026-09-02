-- Defensa en profundidad para formatos que ya valida CapaDominio.
-- NOT VALID conserva datos históricos; la regla se aplica a nuevas inserciones
-- y actualizaciones sin reescribir registros existentes.

ALTER TABLE public.proveedores
  ADD CONSTRAINT proveedores_rtn_formato_chk
  CHECK (
    rtn_proveedor IS NULL OR (
      btrim(rtn_proveedor) ~ '^[0-9 -]+$'
      AND length(regexp_replace(btrim(rtn_proveedor), '[^0-9]', '', 'g')) = 14
    )
  ) NOT VALID,
  ADD CONSTRAINT proveedores_telefono_formato_chk
  CHECK (
    telefono_proveedor IS NULL OR (
      btrim(telefono_proveedor) ~ '^\+?[0-9 -]+$'
      AND length(regexp_replace(btrim(telefono_proveedor), '[^0-9]', '', 'g')) BETWEEN 8 AND 15
    )
  ) NOT VALID,
  ADD CONSTRAINT proveedores_correo_formato_chk
  CHECK (
    correo_proveedor IS NULL OR btrim(correo_proveedor) ~ '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$'
  ) NOT VALID;

-- Rollback documentado:
-- ALTER TABLE public.proveedores
--   DROP CONSTRAINT IF EXISTS proveedores_rtn_formato_chk,
--   DROP CONSTRAINT IF EXISTS proveedores_telefono_formato_chk,
--   DROP CONSTRAINT IF EXISTS proveedores_correo_formato_chk;
