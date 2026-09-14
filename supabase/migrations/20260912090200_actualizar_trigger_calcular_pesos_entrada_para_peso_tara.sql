-- El trigger de entradas_producto leia productos.id_tara -> tara.peso_tara_envalaje.
-- Ahora lee productos.peso_tara directo, sin el join al catalogo.
-- Nota: esta funcion no vivia en las migraciones trackeadas del repo (hueco documentado
-- como P-033 en Deuda Tecnica - Pendientes.md); se agrega acá ya versionada.
CREATE OR REPLACE FUNCTION public.calcular_pesos_entrada()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    v_tara_ind NUMERIC;
BEGIN
    SELECT COALESCE(p.peso_tara, 0) INTO v_tara_ind
    FROM movimiento_productos mp
    JOIN productos p ON p.id_producto = mp.id_producto
    WHERE mp.id_mov_producto = NEW.id_mov_producto;

    NEW.peso_tara_individual := v_tara_ind;
    NEW.peso_tara_total      := COALESCE(NEW.peso_tara_extra, 0) + v_tara_ind;
    NEW.peso_neto            := NEW.peso_bruto - NEW.peso_tara_total;

    IF NEW.peso_neto <= 0 THEN
        RAISE EXCEPTION 'peso_neto no puede ser <= 0 (bruto=%, tara_total=%)',
            NEW.peso_bruto, NEW.peso_tara_total;
    END IF;

    RETURN NEW;
END;
$function$;
