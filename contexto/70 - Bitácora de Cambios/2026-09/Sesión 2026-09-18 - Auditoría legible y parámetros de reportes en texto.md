---
title: "Sesión 2026-09-18 — Auditoría legible y parámetros de reportes en texto"
tags:
  - sesion
  - bitacora
  - reporteria
  - supabase
date: 2026-09-18
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Fernando)
---

# Sesión 2026-09-18 — Auditoría legible y parámetros de reportes en texto

> [!success] Resultado
> Los datos visibles de auditoría dejan de persistirse y presentarse como objetos JSON. Las RPC y emisores de reportes guardan texto descriptivo, mientras que Bitácora conserva compatibilidad de lectura con filas históricas estructuradas sin reescribirlas.

---

## Problema / motivo

Las RPC de catálogos y los registros de reportes enviaban objetos JSON a campos destinados a visualización administrativa. Esto hacía que cambios como la modificación de un producto aparecieran en Bitácora con claves y sintaxis técnicas, y que `reporteria.parametros_reporte` almacenara parámetros como `jsonb` aunque su finalidad fuera descriptiva.

## Cambios aplicados

### Normalización central en Supabase

- La migración `supabase/migrations/20260918192751_bitacora_y_reportes_texto.sql` incorporó los helpers privados `etiqueta_auditoria`, `valor_auditoria`, `texto_auditoria` y `resumir_auditoria` para convertir objetos y arreglos JSON en frases legibles.
- `trg_bitacora_texto` normaliza antes de cada `INSERT` los campos visibles `estado_anterior`, `estado_actual` y `campo_extra`, cubriendo también emisores que no pasan por el helper RBAC.
- Las dos firmas de `private.registrar_auditoria_rbac` normalizan el contenido antes de insertarlo. `log_upd_producto` conserva el resumen de cambios de Productos, pero lo entrega como texto descriptivo.
- Los estados se expresan con significado de negocio: recepción abierta/cerrada/anulada, producto abierto/cerrado/anulado y pesaje activo/anulado.
- La Bitácora histórica no fue actualizada ni eliminada; el cambio es forward-only.

### Contrato textual de reportes

- `public.reporteria.parametros_reporte` cambió de `jsonb` a `text`. Los 26 registros históricos fueron convertidos a su representación textual equivalente.
- `ingresar_reporte_tabla_bitacora` reemplazó `p_parametros_reporte jsonb` por `p_parametros_reporte text`; se eliminó la firma anterior para evitar ambigüedad en PostgREST y se restauraron los permisos de `authenticated` y `service_role`.
- `ReporteRegistroDto.ParametrosJson` pasó a `ParametrosTexto`, y `ReporteRepository` envía el valor directamente a la RPC.
- `ParametrosReporteTexto` centraliza la construcción de pares etiqueta–valor para las exportaciones originadas en Pesaje, Bitácora y Reportería.

### Compatibilidad de presentación

- `TextoAuditoria` aplica en C# el mismo contrato semántico para filas históricas que todavía contienen JSON. Texto ya legible se conserva sin cambios y una estructura histórica inválida se presenta como detalle incompleto, sin exponer excepciones al usuario.
- `BitacoraCrudRepository` normaliza estado anterior, estado actual y detalle durante el mapeo; por eso la grilla y las exportaciones comparten la misma representación.

## Verificación

- `dotnet build BimboProyecto.sln --no-restore --nologo` → 0 errores y 0 advertencias.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-restore --nologo` → 560/560 pruebas aprobadas.
- `supabase/tests/bitacora_y_reportes_texto.sql` validó dentro de una transacción con rollback la modificación y cambio de estado de productos, las mutaciones de roles y la generación de reportes, rechazando nuevos valores visibles que comenzaran con `{` o `[`.
- La migración remota `20260918192751_bitacora_y_reportes_texto` aparece aplicada y el catálogo confirmó `trg_bitacora_texto`, la nueva firma textual de la RPC y los `search_path` fijados en los helpers nuevos.
- El checksum de las 598 filas históricas de `public.bitacora` permaneció idéntico antes y después de la migración.
- Los advisors de seguridad y rendimiento se revisaron; los avisos encontrados eran preexistentes y no fueron introducidos por esta migración.

> [!warning] Validación visual pendiente
> El build, las pruebas y la base viva están verificados, pero todavía falta recorrer con una sesión autenticada la grilla de Bitácora y las exportaciones de Pesaje/Reportería para aprobar visualmente el texto final.

## Lo que NO cambió

- No se modificaron acciones, módulos ni relaciones RBAC de Bitácora.
- No se reescribieron las filas históricas de `public.bitacora`.
- Los JSON técnicos usados internamente para idempotencia, resultados de RPC y metadata no visible permanecen estructurados.
- Los cambios XAML que ya existían en `BitacoraView.xaml` y `RolesView.xaml` no forman parte de esta corrección.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Bitácora]]
- [[Módulo Reportería]]
- [[Módulo Pesaje]]
- [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]]

