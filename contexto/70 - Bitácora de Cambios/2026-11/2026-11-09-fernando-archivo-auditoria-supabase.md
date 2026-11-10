---
title: "Sesión 2026-11-09 — Archivo de Auditoría en Supabase (R4)"
type: sesion
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: antigravity
modelo: gemini-3.8-flash-high
rama: feat/archivo-auditoria
commits: [5d3dd46, aca0ff3]
tags: [sesion, auditoria, supabase, worm, rls, devsecops]
summary: "Implementación del archivo de auditoría inmutable en Supabase (R4): esquema DDL WORM, script de ingestión y verificación, workflow semanal y suite de pruebas."
deuda: []
adr: []
---

# Sesión 2026-11-09 — Archivo de Auditoría en Supabase (R4)

## Problema

La acumulación de sesiones históricas (~184 notas) y deuda técnica resuelta en el repositorio elevaba el footprint del contexto de arranque por encima de 65k tokens, ralentizando el onboarding y consumiendo cuota innecesaria en agentes de IA y colaboradores humanos. Se requería trasladar las sesiones de meses cerrados (>60 días) y deuda técnica resuelta (>60 días) a una base de datos de auditoría independiente (`bimbo-auditoria-dev`), garantizando inmutabilidad estricta (WORM), control de acceso por lista blanca (RLS) y búsqueda de texto completo en español (FTS), sin perder la trazabilidad de Git.

## Cambios

1. **Migración SQL WORM (`auditoria/migrations/0001_esquema_auditoria.sql`):**
   - Creación del esquema `auditoria`.
   - Tabla `auditoria.sesiones` con todos los campos especificados: `id`, `date`, `autor`, `agente`, `modelo`, `rama`, `commits`, `titulo`, `tags`, `summary`, `deuda`, `adr`, `cuerpo_md`, `ruta_original`, `sha256` único, `archivado_en` y `commit_archivo`.
   - Tabla `auditoria.deuda_resuelta`: `id_p`, `titulo`, `prioridad`, `cerrada_en`, `cuerpo_md`, `sha256` y `archivado_en`.
   - Tabla `auditoria.lectores`: control de acceso para usuarios autenticados autorizados.
   - Índices GIN de texto completo en español sobre título + summary + cuerpo.
   - Disparador WORM estricto (`trg_sesiones_inmutables` y `trg_deuda_resuelta_inmutable`) que rechaza incondicionalmente cualquier `UPDATE` o `DELETE` con excepción.
   - Políticas RLS para lectura exclusiva a usuarios `authenticated` registrados en `auditoria.lectores`, denegando acceso a `anon`. Privilegios mínimos de esquema.

2. **Script de archivo e ingestión (`scripts/contexto/archivar-bitacora.js`):**
   - Node 20+, sin dependencias externas obligatorias.
   - Modo `--dry-run` por defecto y soporte para `--aplicar`, `--backup-dir` y `--fecha-referencia`.
   - Detección precisa de meses cerrados (>60 días): a 2026-11 detecta 2026-05 a 2026-08 (100 sesiones con `archivo/`) y 49 ítems de deuda resuelta.
   - Validación obligatoria de existencia de `AAAA-MM-resumen.md` previo a cualquier operación.
   - Con `--aplicar`: upsert idempotente en Supabase por `sha256`, verificación estricta de ida y vuelta antes de ejecutar `git rm` y actualización de resúmenes mensuales.

3. **Automatización CI/CD (`.github/workflows/archivar-bitacora.yml`):**
   - Ejecución programada los lunes a las 06:00 UTC (previene pausa por inactividad de Supabase Free) y bajo demanda (`workflow_dispatch`).
   - Acciones fijadas por SHA de commit con versión en comentario.
   - Apertura de PR automatizado hacia `main` si hubo cambios (sin push directo).

4. **Guía de consulta y referencia (`contexto/50 - Referencia/Auditoría - cómo consultar.md`):**
   - Consultas SQL completas: por autor, agente, rango de fechas, P-NNN, ADR y FTS con `websearch_to_tsquery('spanish', ...)`.
   - Aclaración explícita sobre la preservación histórica permanente en Git.

5. **Suite de pruebas unitarias (`scripts/contexto/archivar-bitacora.test.js`):**
   - 7 pruebas con `node:test` verificando hashes SHA-256, cálculo de fechas de corte, validación de resúmenes, extracción de deuda y generación de respaldos JSONL.

## Verificación

- `node scripts/contexto/archivar-bitacora.js --dry-run`: 100 sesiones y 49 ítems de deuda resuelta identificados correctamente; validación de resúmenes en verde.
- `node --test scripts/contexto/lib.test.js scripts/contexto/archivar-bitacora.test.js`: 14 pruebas superadas (0 fallos).
- `node scripts/contexto/build-index.js`: `INDEX.md` regenerado deterministamente en 10.212 B (tope 10.240 B).
- `node scripts/contexto/verificar.js`: 0 errores en 306 notas analizadas.
- `dotnet build BimboProyecto.sln -c Release`: 0 errores y 0 advertencias.

## Lo que no cambió

- No se modificó ningún archivo de código de la aplicación C#/WPF (`CapaUI`, `CapaDatos`, `CapaDominio`, `CapaAplicacion4`).
- No se realizaron escrituras en la base de datos de producción de la báscula ni se crearon proyectos en Supabase en esta fase.
- Las notas de sesión físicas permanecen en el repositorio local; su borrado efectivo sólo ocurrirá al ejecutarse `--aplicar` contra el entorno `bimbo-auditoria-dev` una vez aprovisionado.

## Pendientes

- Aprovisionamiento del proyecto Supabase `bimbo-auditoria-dev` y aplicación de la migración `0001_esquema_auditoria.sql` por parte de un integrador humano.
- Configuración de los secretos `AUDIT_SUPABASE_URL` y `AUDIT_SUPABASE_SERVICE_KEY` en los secretos de GitHub Actions.

## Relaciones

- [[Auditoría - cómo consultar]]
- [[Deuda - Resuelta]]
- [[Arquitectura Actual]]
- [[INDEX-bitacora]]
