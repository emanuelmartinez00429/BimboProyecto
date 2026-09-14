-- Tara deja de ser un catalogo compartido (id_tara -> tara.peso_tara_envalaje) y pasa a
-- ser un numero propio por producto. Se agrega la columna nueva y se hace backfill desde
-- el catalogo antes de tocar el trigger/RPCs (ver migraciones siguientes). id_tara queda
-- vivo por ahora -- se dropea en una migracion separada una vez confirmado en produccion.
ALTER TABLE public.productos ADD COLUMN IF NOT EXISTS peso_tara numeric(10,3);

-- El backfill es una migracion de esquema, no una accion de un usuario de la app -- se
-- desactiva el trigger de auditoria (exige auth.uid() resuelto, que no existe corriendo
-- como migracion) solo para este UPDATE puntual.
ALTER TABLE public.productos DISABLE TRIGGER trg_log_upd_producto;

UPDATE public.productos p
SET peso_tara = t.peso_tara_envalaje
FROM public.tara t
WHERE t.id_tara = p.id_tara
  AND p.peso_tara IS NULL;

-- Contenido era varchar(100) de texto libre ("20 kg", "1 und", vacio) -- se extrae el
-- numero (si lo hay) y se descarta el resto; lo que no tenga digitos queda NULL.
ALTER TABLE public.productos
  ALTER COLUMN contenido TYPE numeric(10,2)
  USING NULLIF(regexp_replace(contenido, '[^0-9.]', '', 'g'), '')::numeric;

ALTER TABLE public.productos ENABLE TRIGGER trg_log_upd_producto;
