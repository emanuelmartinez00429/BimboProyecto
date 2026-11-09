---
title: "Sesión 2026-11-09 — Esquema de sesión, validador de contexto e índices generados"
type: sesion
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: antigravity
modelo: gemini-2.5-pro
rama: chore/contexto-validador
commits: [ca74dab]
tags:
  - sesion
  - devsecops
  - contexto
  - validador
summary: "Portabilidad de lib.js, verificar.js y build-index.js, normalización de 178 notas de bitácora e índices INDEX.md e INDEX-bitacora.md"
deuda: []
adr: []
---

# Sesión 2026-11-09 — Esquema de sesión, validador de contexto e índices generados

## Problema o motivo

La bóveda de contexto de BimboProyecto acumulaba más de 295 notas sin portón de calidad automatizado, con esquemas dispares de frontmatter, menciones dispersas de autores y formatos no normalizados. No existían índices deterministas autogenerados (`INDEX.md` e `INDEX-bitacora.md`), lo que obligaba a inyectar notas pesadas al arrancar las sesiones de los agentes.

## Cambios

1. **Herramientas de contexto (`scripts/contexto/`):**
   - `lib.js`: Módulo nativo (Node 20+, sin dependencias externas) con taxonomía Bimbo (`00 - MOC` hasta `70 - Bitácora de Cambios`, incluyendo `60 - Revisiones QA`), parseo seguro de frontmatter, relaciones, cálculo de meses abiertos y generadores de índices.
   - `verificar.js`: Validador estricto de portón de calidad que verifica frontmatter obligatorio en bitácora y notas nuevas, unicidad de IDs ADR y P en deuda, ausencia de rutas absolutas de disco, protección contra fugas de datos personales (correos y teléfonos) y patrones de secretos (JWT, `service_role`, `sb_secret_`), así como vigencia y tamaño estricto de `AGENTS.md` (<= 8 KB) e `INDEX.md` (<= 10 KB).
   - `build-index.js`: Generador de `contexto/INDEX.md` (resumen condensado de una línea por nota viva y solo 3 sesiones recientes de bitácora) y `contexto/INDEX-bitacora.md`.
   - `lib.test.js`: Suite de 7 pruebas unitarias ejecutadas con el runner nativo `node --test`.
2. **Plantilla de sesión (`contexto/_templates/Plantilla Sesión.md`):**
   - Estandarización de frontmatter (`title`, `type: sesion`, `status: vigente`, `date`, `updated`, `autor`, `agente`, `modelo`, `rama`, `commits`, `tags`, `summary`, `deuda`, `adr`) y estructura de 6 secciones obligatorias.
3. **Normalización de bitácora (`scripts/contexto/normalizar-bitacora.js`):**
   - Script idempotente con `--dry-run` por defecto y flag `--aplicar`.
   - Normalización de 178 notas existentes en `70 - Bitácora de Cambios`.
   - Descomposición de `autor_cambios` en `autor`, `agente` y `modelo`.
   - Mapeo de autores no resolubles a `autor: por-confirmar` con auditoría en `contexto/.control/mapeo-autores-pendiente.md`.
4. **Higiene de contexto y seguridad:**
   - Corrección de enlaces absolutos y rutas de disco a rutas relativas en notas de análisis QA y bitácora.
   - Sustitución de correos de prueba por dominios estándar (`example.com` / `example.org`) y eliminación de referencias personales.

## Verificación

- `node --test scripts/contexto/lib.test.js` → 7 pruebas superadas al 100% (0 fallos).
- `node scripts/contexto/build-index.js` → Genera `INDEX.md` (10.183 B <= 10.240 B) e `INDEX-bitacora.md` (1.738 B).
- `node scripts/contexto/verificar.js` → Salida con código 0, 0 errores, 295 notas analizadas.

## Lo que no cambió

- No se modificó el código fuente de la aplicación (`CapaUI`, `CapaDatos`, `CapaDominio`, `CapaAplicacion4`).
- No se alteraron ramas ajenas ni la base de datos de producción.

## Pendientes

- Revisión por parte de Fernando del reporte `contexto/.control/mapeo-autores-pendiente.md`.
- Implementación de R3 (dieta del contexto de arranque en rama `chore/contexto-arranque`).

## Relaciones

- [[AGENTS]]
- [[INDEX]]
- [[INDEX-bitacora]]
- [[Convenciones de código]]
