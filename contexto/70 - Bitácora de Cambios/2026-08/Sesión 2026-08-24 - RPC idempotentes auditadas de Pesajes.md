---
type: session-log
project: Bimbo Honduras
date: 2026-08-24
tags:
  - session
  - pesaje
  - supabase
  - rpc
  - idempotencia
---

# Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes

## Objetivo

Crear en Supabase las RPC de escritura planeadas para Pesajes, aplicando [[GUIA_APLICACION_IDEMPOTENCIA_RPC|la guía de idempotencia]], autenticación, RBAC, bloqueo, validación de transiciones y bitácora.

## Trabajo realizado

- Se creó la migración canónica `supabase/migrations/202608240001_rpc_pesajes_idempotentes.sql`.
- Se desplegaron diez RPC públicas de Pesajes y cuatro helpers privados.
- Toda identidad se deriva de `auth.uid()` y se comprueba contra `auth.users` y `public.usuarios` activo.
- Los permisos se validan por los nombres literales `Registrar Entrada`, `Modificar Pesaje`, `Completar Pesaje` y `Cancelar Pesaje` del módulo Pesaje.
- Cada escritura exige `p_id_solicitud`; acepta `p_id_operacion`; calcula SHA-256 servidor; conserva y reproduce el mismo resultado JSONB; rechaza reutilización conflictiva.
- Inserts, updates, bitácora y finalización de `private.solicitudes_rpc` ocurren en una transacción.
- Los updates bloquean la fila, validan la transición, comprueban `ROW_COUNT = 1` y registran anterior/actual.
- El reparto de tara extra bloquea IDs ordenados, distribuye a tres decimales, coloca el residuo en la última entrada y genera una bitácora por pesaje.
- La creación automática de recepción por placa/proveedor usa advisory lock transaccional para evitar carreras.

## RPC públicas

1. `ingresar_movimiento_pesaje_tabla_bitacora`
2. `ingresar_producto_recepcion_pesaje_tabla_bitacora`
3. `ingresar_entrada_producto_pesaje_tabla_bitacora`
4. `actualizar_movimiento_pesaje_tabla_bitacora`
5. `cambiar_estado_movimiento_pesaje_tabla_bitacora`
6. `actualizar_producto_movimiento_pesaje_tabla_bitacora`
7. `cambiar_estado_producto_pesaje_tabla_bitacora`
8. `actualizar_entrada_producto_pesaje_tabla_bitacora`
9. `cancelar_entrada_producto_pesaje_tabla_bitacora`
10. `repartir_tara_extra_pesaje_tabla_bitacora`

## Pruebas y validaciones

- Supabase aceptó la migración atómica.
- Prueba controlada con rollback invocó las diez RPC sobre una cadena recepción → producto → entrada → actualizaciones → reparto → cancelaciones.
- Repetir el mismo UUID devolvió el mismo JSONB sin duplicar registro ni bitácora.
- Reutilizar el UUID con parámetros distintos produjo SQLSTATE `22023`.
- Todas las solicitudes terminaron `COMPLETADA` con resultado y el rollback dejó cero filas de prueba.
- Se verificaron 10 funciones públicas, 4 privadas, `SECURITY DEFINER`, `search_path` fijo y ACL privadas cerradas.
- `git diff --check` no reportó errores.

## Hallazgos

- `trg_calcular_pesos_entrada` sí cubre `UPDATE`, cerrando la duda P-033.
- Su función `calcular_pesos_entrada()` usa relaciones sin esquema y no fija su propio `search_path`; el núcleo privado fija `pg_catalog, public, pg_temp` para mantener compatibilidad. El asesor de seguridad conserva este hallazgo preexistente.
- Las tablas de Pesajes y bitácora aún tienen privilegios DML amplios para `anon`/`authenticated`. No se revocaron porque el repositorio C# todavía escribe directamente.

## Pendientes

- Migrar las operaciones restantes de `movimiento_productos` y los updates/cancelación/reparto de `entradas_producto`; `movimientos` y el ingreso de una entrada ya consumen sus RPC.
- Ejecutar prueba concurrente real de dos sesiones con el mismo UUID.
- Tras migrar y probar el cliente, revocar DML directo y retirar las políticas de escritura permisivas.
- Corregir la función del trigger para cualificar relaciones o fijar su propio `search_path`.

## Integración C# de movimientos

En la misma fecha se migraron `CrearCamionAsync`, `ActualizarCamionAsync`, `CerrarCamionAsync` y `AnularCamionAsync` en `PesajeRepository` para consumir las tres RPC de `movimientos`. Cada intención genera un único `Guid` antes de ejecutar la llamada, valida el resultado JSONB y mantiene las firmas existentes. El parámetro legado `idUsuario` se conserva por compatibilidad, pero la identidad efectiva proviene de `auth.uid()`.

No se revocó ningún permiso DML y no se modificó ninguna política RLS, por decisión explícita: el endurecimiento de acceso se realizará al final del desarrollo del sistema.

`dotnet build BimboProyecto.sln` terminó con 0 errores; permanecen warnings preexistentes de nullable, acceso temporal al archivo de salida y consulta de vulnerabilidades NuGet sin red.

## Corrección del detalle visible en Bitácora

Se corrigió `private.bitacora_pesaje` porque el ingreso de una `entrada_producto` no identificaba de forma administrativa qué se había pesado. Para `Registro de pesaje`, el helper ahora resuelve la cadena completa `entrada → movimiento_producto → producto → movimiento → proveedor` y guarda en `estado_actual`, que es la columna **DETALLE** visible en la grilla:

- código y nombre del producto;
- nombre del proveedor;
- placa del vehículo;
- peso bruto;
- tara total, separando empaque y tara extra;
- peso neto recibido.

`campo_extra` conserva los IDs de pesaje, recepción y producto de recepción, además de las observaciones. La migración incremental es `supabase/migrations/202608240002_mejorar_detalle_bitacora_ingreso_pesaje.sql`.

La prueba controlada utilizó el pesaje 114, verificó que el texto incluyera `B11110 - Sticker cajilla`, `8960.000 kg` brutos y `8955.000 kg` netos, y revirtió la bitácora temporal sin dejar residuos. No se modificaron DML ni RLS.

Para que el detalle se genere en el flujo real, `PesajeRepository.CrearEntradaAsync` dejó el `INSERT` directo y ahora llama `ingresar_entrada_producto_pesaje_tabla_bitacora`, genera un UUID por intención y mapea la fila JSONB calculada por el trigger al mismo `EntradaDto`. Los updates y cancelaciones de entradas no se migraron en este ajuste.

## Estados expresados por significado

La migración `supabase/migrations/202608240003_describir_estados_pesaje_en_bitacora.sql` eliminó de la descripción administrativa expresiones como `Estado 7`, `Estado 8`, `Estado 9`, `Estado Activo (1)` y `Estado Anulado (9)`. `private.bitacora_pesaje` ahora traduce según la entidad:

- recepción: `Recepción abierta`, `Recepción cerrada`, `Recepción anulada`;
- producto de recepción: `Producto abierto`, `Producto cerrado`, `Producto anulado`;
- entrada: `Pesaje activo`, `Pesaje anulado`.

Una prueba transaccional cubrió las tres familias y revirtió las bitácoras temporales sin residuos. Los IDs internos permanecen en las tablas como integridad referencial, pero ya no forman parte de `estado_anterior` ni `estado_actual` para estas transiciones.

## Relaciones

- [[Módulo Pesaje]]
- [[Deuda Técnica - Pendientes]]
- [[Arquitectura Actual]]
