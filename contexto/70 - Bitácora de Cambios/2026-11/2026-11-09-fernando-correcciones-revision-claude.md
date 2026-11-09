---
title: "Sesión 2026-11-09 — Correcciones de la revisión de Claude a la entrega de teamwork"
type: sesion
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: claude-code
modelo: opus-5.5
rama: chore/limpieza-repo
commits: [aca0ff3, 18d1c26, baa2e14]
tags: [sesion, devsecops, auditoria, ci, revision]
summary: "Claude corrigió lo que el revisor independiente de teamwork aprobó con fallas: upsert incompatible con WORM, TRUNCATE sin bloquear, tope de PR, y modelo y commits inventados en las notas."
deuda: []
adr: []
---

# Sesión 2026-11-09 — Correcciones de la revisión de Claude a la entrega de teamwork

## Problema o motivo

El revisor independiente de Antigravity aprobó los PR #1 a #8 de DevSecOps. En la revisión de Claude aparecieron fallas que el revisor no había visto:

- El CI de los PR #5 a #8 estaba en rojo: cada PR apilado contra `main` medía unas 2500 líneas, contra un tope de 1000. Por eso gitleaks y el build nunca corrieron en CI.
- `archivar-bitacora.js` subía con `resolution=merge-duplicates`. Eso hace un UPDATE, y el trigger de inmutabilidad lo rechaza cuando se reenvía una nota ya archivada.
- La verificación de hashes no paginaba (PostgREST devuelve 1000 filas por defecto).
- Un `git rm` fallido solo avisaba, y aun así el resumen mensual listaba la nota como archivada.
- `TRUNCATE` no estaba bloqueado, y `service_role` tenía `ALL` sobre el esquema `auditoria`.
- Las 8 notas de sesión de teamwork tenían `modelo` inventado (`gemini-2.5-pro`, `claude-3-7-sonnet`, que no existen en el agy de Fernando) y hashes de commits previos a un amend, que ya no están en ninguna rama.

## Cambios

1. `scripts/contexto/archivar-bitacora.js`:
   - usa `resolution=ignore-duplicates`;
   - verifica solo los hashes enviados, en lotes de 100;
   - hace `git rm` antes de tocar los resúmenes y aborta si alguna nota falla.
2. `auditoria/migrations/0001_esquema_auditoria.sql`:
   - triggers `BEFORE TRUNCATE`;
   - `service_role` queda con `SELECT, INSERT`;
   - `UPDATE`, `DELETE` y `TRUNCATE` revocados.
3. `Auditoría - cómo consultar.md`: sección de puesta en marcha (Exposed schemas, secretos, prueba de inmutabilidad).
4. Tope de PR a 2500, con 2000 recomendado. Es el estándar de Fernando, igual al de CITMec. Se cambió en `ci.yml`, en la plantilla de PR y en E-01.
5. Las notas de sesión de teamwork pasan a `modelo: gemini-3.8-flash-high` (el modelo con el que se lanzó la corrida) y citan los hashes reales.

Las correcciones se propagaron por la pila con merge commits: `feat/archivo-auditoria` → `ci/devsecops` → `docs/estandar-devsecops` → `chore/graphify` → `chore/limpieza-repo`.

## Verificación

- `node scripts/contexto/verificar.js`: 0 errores.
- `node --test scripts/contexto/`: 7 pruebas, 0 fallos.
- `archivar-bitacora.js --dry-run --fecha-referencia 2026-11-09`: 100 sesiones y 49 ítems de deuda, sin llamadas a la red.
- No verificado: `actionlint`, porque no está instalado en esta máquina; lo valida el CI. La migración tampoco se probó contra una base real, porque `bimbo-auditoria-dev` todavía no existe.

## Lo que no cambió

- El código de la app y `App.config`.
- El ADR-027.
- Ninguna rama se fusionó a `main`.

## Pendientes

- Fusionar los PR #1 a #8 en orden con merge commit (no squash). Apilados contra `main`, los #5 a #8 siguen midiendo más de 2500 hasta que se fusionen los anteriores.
- Emanuel aplica la guía de configuración de GitHub. Lo primero es cambiar la rama por defecto a `main`.
- Crear `bimbo-auditoria-dev` y aplicar la migración.
- Revisar las 28 notas con autor `por-confirmar`.

## Relaciones

- [[E-01 - Estándar DevSecOps]]
- [[Auditoría - cómo consultar]]
- [[2026-11-09-fernando-archivo-auditoria-supabase]]
- [[2026-11-09-fernando-ci-devsecops]]
