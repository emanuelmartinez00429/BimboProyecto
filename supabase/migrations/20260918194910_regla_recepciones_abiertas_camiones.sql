-- ════════════════════════════════════════════════════════════════════════════
--  Pesaje — reglas de recepciones abiertas (camiones) en la BD
--
--  Una fila de `movimientos` = una placa + un proveedor. La pantalla de Pesaje
--  dejó de agrupar por placa (2026-09-18) y las reglas pasan a vivir también acá,
--  para que dos terminales a la vez no puedan romperlas:
--
--    R1  placa normalizada: upper(btrim())
--    R4  como máximo 5 recepciones abiertas (id_estado = 7), contadas por FILA
--    R5  no hay dos abiertas con la misma placa y el mismo proveedor
--        (misma placa con otro proveedor sí está permitido)
--    R7  no se anula una recepción que tiene productos vivos (estado 7 u 8)
--
--  Se resuelve con UN trigger sobre la tabla en vez de tocar cada RPC: cubre
--  registrar_camiones_lote_seguro, el alta simple, la edición y el alta
--  automática del RPC legado ingresar_producto_recepcion, y cualquier camino futuro.
--  El índice único parcial queda como última red ante cualquier carrera.
--
--  ⚠ El 5 está duplicado a propósito en CapaDominio/Reglas/ReglasEntidades.cs
--    (ReglasCamion.MaxRecepcionesAbiertas). Si cambia, cambian los dos.
-- ════════════════════════════════════════════════════════════════════════════

create or replace function private.validar_recepcion_movimiento()
returns trigger
language plpgsql
security definer
set search_path = pg_catalog, public, pg_temp
as $$
declare
    c_max_abiertas constant integer := 5;   -- = ReglasCamion.MaxRecepcionesAbiertas
    v_abiertas integer;
begin
    -- R1: la placa se guarda siempre normalizada.
    new.placa_vehiculo := upper(btrim(new.placa_vehiculo));

    -- R7: anular solo sin productos vivos.
    if tg_op = 'UPDATE' and new.id_estado = 9 and old.id_estado <> 9
       and exists (select 1 from public.movimiento_productos mp
                   where mp.id_movimiento = new.id_movimiento and mp.id_estado in (7, 8)) then
        raise exception 'No se puede quitar el camión %: tiene productos registrados', new.placa_vehiculo
            using errcode = 'P0001';
    end if;

    -- R4/R5 solo aplican a una recepción que queda abierta y cuya clave cambió.
    if new.id_estado <> 7 then
        return new;
    end if;
    if tg_op = 'UPDATE' and old.id_estado = 7
       and old.placa_vehiculo is not distinct from new.placa_vehiculo
       and old.id_proveedor = new.id_proveedor then
        return new;
    end if;

    -- Serializa altas/ediciones concurrentes: sin esto, dos terminales podrían
    -- contar 4 a la vez y dejar 6 abiertas. Se libera al terminar la transacción.
    perform pg_advisory_xact_lock(hashtext('pesaje:recepciones_abiertas'));

    if exists (select 1 from public.movimientos m
               where m.id_estado = 7
                 and m.id_movimiento <> coalesce(new.id_movimiento, -1)
                 and upper(btrim(m.placa_vehiculo)) = new.placa_vehiculo
                 and m.id_proveedor = new.id_proveedor) then
        raise exception 'La placa % ya tiene una recepción abierta con ese proveedor', new.placa_vehiculo
            using errcode = 'P0001';
    end if;

    -- El cupo solo se consume al abrir una recepción nueva, no al editar una abierta.
    if tg_op = 'INSERT' or old.id_estado <> 7 then
        select count(*) into v_abiertas from public.movimientos m where m.id_estado = 7;
        if v_abiertas >= c_max_abiertas then
            raise exception 'Ya hay % camiones abiertos: cierre o quite uno antes de registrar otro', c_max_abiertas
                using errcode = 'P0001';
        end if;
    end if;

    return new;
end;
$$;

revoke all on function private.validar_recepcion_movimiento() from public, anon, authenticated;

drop trigger if exists trg_validar_recepcion_movimiento on public.movimientos;
create trigger trg_validar_recepcion_movimiento
    before insert or update of placa_vehiculo, id_proveedor, id_estado on public.movimientos
    for each row execute function private.validar_recepcion_movimiento();

-- Última red: aunque el trigger se desactivara, la BD no admite el duplicado.
create unique index if not exists ux_movimientos_abierto_placa_proveedor
    on public.movimientos (upper(btrim(placa_vehiculo)), id_proveedor)
    where id_estado = 7;
