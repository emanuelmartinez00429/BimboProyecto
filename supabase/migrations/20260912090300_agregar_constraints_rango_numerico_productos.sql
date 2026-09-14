-- Endurece las reglas numericas de productos: sin negativos ni cero, tope 999999.
-- IS NULL OR se mantiene (mismo criterio que el resto del proyecto): "obligatorio" para
-- filas nuevas se exige en RPC/UI, no reescribiendo datos historicos con NOT NULL.
ALTER TABLE public.productos
  DROP CONSTRAINT IF EXISTS chk_peso_teorico_positivo,
  DROP CONSTRAINT IF EXISTS productos_precio_por_kg_check,
  ADD CONSTRAINT productos_peso_teorico_rango_chk
    CHECK (peso_teorico IS NULL OR (peso_teorico > 0 AND peso_teorico <= 999999)) NOT VALID,
  ADD CONSTRAINT productos_precio_por_kg_rango_chk
    CHECK (precio_por_kg IS NULL OR (precio_por_kg > 0 AND precio_por_kg <= 999999)) NOT VALID,
  ADD CONSTRAINT productos_contenido_rango_chk
    CHECK (contenido IS NULL OR (contenido > 0 AND contenido <= 999999)) NOT VALID,
  ADD CONSTRAINT productos_peso_tara_rango_chk
    CHECK (peso_tara IS NULL OR (peso_tara > 0 AND peso_tara <= 999999)) NOT VALID;
