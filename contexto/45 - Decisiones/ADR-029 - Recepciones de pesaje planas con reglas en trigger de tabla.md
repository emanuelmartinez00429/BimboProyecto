---
title: "ADR-029 — Recepciones de pesaje planas con reglas en trigger de tabla"
tags:
  - adr
  - decision
  - pesaje
  - supabase
date: 2026-09-18
estado: aceptado
---

# ADR-029 — Recepciones de pesaje planas con reglas en trigger de tabla

## Contexto

En la BD, una fila de `movimientos` es **una placa + un proveedor**. Un camión que trae carga de varios proveedores son varias recepciones con la misma placa.

Entre el 2026-09-05 y el 2026-09-16 la pantalla de Pesaje agrupó esas recepciones por placa: cabecera con sub-filas, «placa vacía» como cascarón, edición de la placa propagada a todas las recepciones hermanas. Eso trajo tres problemas:

1. **Complejidad sin respaldo en el modelo.** Hacían falta unas 600 líneas de UI para una agrupación que no existe en los datos.
2. **Escrituras no atómicas.** Propagar la placa eran N updates sueltos.
3. **Reglas de negocio solo de pantalla.** El cupo de 5, la unicidad placa + proveedor y «no quitar con productos» no las validaba el servidor. Con dos terminales a la vez se podían romper.

Además, el cupo se contaba por placa distinta y el contador de la tabla por fila.

## Decisión

1. **La pantalla muestra lo que hay en la BD:** una fila por recepción. No se agrupa por placa. El KG es el manifestado de cada fila y no hay total por placa.
2. **El cupo es de 5 recepciones abiertas, contadas por fila.** Tres proveedores con la misma placa ocupan tres lugares.
3. **Las reglas entre filas se hacen cumplir en un trigger de la tabla** (`trg_validar_recepcion_movimiento`) y no en cada RPC. El trigger:
   - normaliza la placa;
   - rechaza la sexta recepción abierta;
   - rechaza placa + proveedor repetidos entre abiertas;
   - rechaza anular una recepción con productos vivos.

   Un `pg_advisory_xact_lock` serializa las altas concurrentes, y un índice único parcial queda como última red.
4. **La misma regla vive en Dominio** (`ReglasCamion.ValidarRecepciones`) para avisarle al operador antes de enviar (ADR-021). El tope `5` se duplica a propósito, con un comentario cruzado.
5. **El reporte se elige por placa.** Marcar una placa incluye todas sus recepciones, y todo sale en un solo archivo.

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| Mantener la agrupación por placa en la UI | Se ve como "un camión" | ~600 líneas; N updates sin transacción; el cupo se cuenta distinto que el contador | ❌ |
| Tabla `camiones` padre (placa) con recepciones hijas | Modela el camión físico | Migración de esquema y de todas las RPC y el reporte por un concepto que el negocio no pidió gestionar | ❌ |
| Validar el cupo y la unicidad dentro de cada RPC | Mensajes por operación | Hay que repetirlo en 4 caminos (lote, alta simple, edición, alta automática legada) y los futuros lo pueden olvidar | ❌ |
| **Trigger de tabla + índice único parcial** | Una sola pieza cubre todos los caminos; la concurrencia se resuelve con advisory lock; el mensaje ya sale escrito para el operador | La regla vive en SQL además de en C#, con el tope duplicado | ✅ |
| Vista SQL para KG y totales por placa | 1 round trip menos | Sin totales por placa no aporta; suma `security_invoker`, GRANTs y recreación en cada `ALTER` | ❌ |

## Consecuencias

- **Se gana:**
  - las reglas son seguras con varias terminales;
  - hay menos código de UI;
  - el cupo es coherente entre el contador y la validación;
  - cada fila se edita con una sola RPC transaccional.
- **Se sacrifica:** la pantalla ya no muestra el "camión físico" como unidad. Esa unidad reaparece solo en el reporte, que se elige por placa.
- **Seguimiento:**
  - si el tope cambia, se cambia en `ReglasCamion.MaxRecepcionesAbiertas` **y** en `private.validar_recepcion_movimiento`;
  - el reporte todavía no busca recepciones cerradas de días anteriores con la misma placa.

---

## Relaciones

- [[Módulo Pesaje]]
- [[Sesión 2026-09-18 - Camiones en tabla plana y reglas de recepción en BD]]
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]]
- [[Plan de Migración de Mutaciones Directas a RPC]]
- [[Arquitectura Actual]]
