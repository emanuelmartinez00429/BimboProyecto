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
tags: [sesion, devsecops, contexto, validador]
summary: "Portabilidad de lib.js, verificar.js y build-index.js, normalización de 178 notas de bitácora e índices INDEX.md e INDEX-bitacora.md"
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
   - Atribución rigurosa basada en hechos y sin inventar datos:
     - Autor: ganado de la nota si tenía certeza; de lo contrario inferido del commit git creador (`RedFer855`/`Fernando` → `fernando`, `warthunderlover`/`emanuelmartinez00429` → `emanuel`, `sanchezcarlosvalle-arch` → `carlos`) con marca `autor_fuente: git` y listado en `.control/mapeo-autores-pendiente.md`.
     - Agente: ganado de la nota si lo declaraba; de lo contrario inferido del trailer `Co-Authored-By` del commit (`Claude Opus/Sonnet/Haiku` → `claude-code`, modelo normalizado); si no hay trailer ni mención explícita de agente, se asigna `agente: por-confirmar`. El valor `humano` solo se usa si la nota lo declara explícitamente.
   - El validador `verificar.js` admite `por-confirmar` en `agente` para notas históricas anteriores a 2026-11-10.

#### Conteo de antes y después (Normalización de Bitácora)

| Métrica | Antes (Corrida Inicial) | Después (Corrección Solicitada) |
|---|---|---|
| **`autor: fernando`** | 43 notas | **112 notas** (43 originales + 69 inferidas de git) |
| **`autor: emanuel`** | 31 notas | **38 notas** (31 originales + 7 inferidas de git) |
| **`autor: carlos`** | 0 notas | **1 nota** (inferida de git) |
| **`autor: por-confirmar`** | 105 notas | **28 notas** (desconocidos reales auditados) |
| **`agente: claude-code`** | 20 notas | **59 notas** (20 originales + 39 trailer git) |
| **`agente: codex`** | 30 notas | **37 notas** |
| **`agente: antigravity`** | 16 notas | **19 notas** |
| **`agente: opencode`** | 2 notas | **5 notas** |
| **`agente: por-confirmar`** | 0 notas | **59 notas** (agente desconocido real) |
| **`agente: humano`** | 111 notas (falso default) | **0 notas** (sin afirmación explícita de ausencia de agente) |

4. **Higiene de contexto y seguridad (Saneamiento justificado):**
   - Se sanearon 20 notas históricas que contenían rutas absolutas de disco (unidades locales o esquemas `file:`) o correos personales fuera de dominios de prueba (`@gmail.com`).
   - Justificación: Requisito estricto del validador de portón de calidad (`verificar.js`), que prohíbe rutas de disco absolutas y exposición de datos personales/secretos en cualquier archivo de la bóveda.

## Verificación

- `node --test scripts/contexto/lib.test.js` → 7 pruebas superadas al 100% (0 fallos).
- `node scripts/contexto/build-index.js` → Genera `INDEX.md` (10.165 B <= 10.240 B) e `INDEX-bitacora.md` (1.887 B).
- `node scripts/contexto/verificar.js` → Salida con código 0, 0 errores, 296 notas analizadas.

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
