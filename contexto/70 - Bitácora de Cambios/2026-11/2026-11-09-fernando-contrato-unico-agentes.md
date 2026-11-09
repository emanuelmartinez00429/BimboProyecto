---
title: "Sesión 2026-11-09 — Contrato único de agentes y protocolo de contexto"
type: sesion
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: antigravity
modelo: gemini-2.5-pro
rama: chore/contrato-agentes
commits: [c518122]
tags: [sesion, devsecops, agentes, contexto]
summary: "Unificación de AGENTS.md raíz en <= 8KB, protocolo de bóveda, stubs de agentes, equipo.json, desindexación de corridas y normalización de gitattributes"
---

# Sesión 2026-11-09 — Contrato único de agentes y protocolo de contexto

## Problema o motivo

Existía duplicación y contradicciones entre `AGENTS.md` (raíz, 11 KB), `contexto/AGENTS.md` (18 KB) y `contexto/CLAUDE.md`:
- `AGENTS.md` excedía el límite de 8 KB.
- Contradicción en el nombre del proyecto Engram (`antigravity` vs `bimboproyecto`).
- Referencias inconsistentes a la rama `master` en vez de `main`.
- Regla obsoleta obligando a prefijar sesiones con "Fernando —".
- Enlace relativo roto a `.agents/rules/process_and_quality_habits.md`.
- `contexto/CLAUDE.md` contenía ruta absoluta de OneDrive, referencias a WinForms `FrmMenuPrincipal` y uso prohibido de Service Locator (`App.Services.GetRequiredService`).
- `.agents/` contenía decenas de carpetas de corridas históricas versionadas en Git.
- Falta de registro formal de integrantes en `contexto/.control/equipo.json`.

## Cambios

1. **`AGENTS.md` (raíz):**
   - Fusionado y condensado a 7.851 bytes (límite estricto <= 8.192 B / 8 KB).
   - Incluye: test de conexión ("pregunta clave"), descripción del proyecto, 16 reglas de oro no negociables, comandos de verificación, arranque en frío (1. AGENTS.md -> 2. INDEX.md -> 3. 3-6 notas -> 4. grep), tabla "Vas a… / Empieza por", tabla "Al terminar: documenta", regla graphify, memoria Engram (`bimboproyecto`) y mapa de archivos de control.
   - Resuelve todas las contradicciones (F-R1-01 a F-R1-06).

2. **Stubs apuntadores:**
   - `CLAUDE.md` y `GEMINI.md`: contienen únicamente `@AGENTS.md`.
   - `.github/copilot-instructions.md`: purgado de reglas obsoletas de `@azure`, apunta únicamente a `AGENTS.md`.
   - `.windsurfrules` y `.cursor/rules/bimbo.mdc`: apuntan únicamente a `AGENTS.md`.

3. **Bóveda y convenciones:**
   - `contexto/AGENTS.md`: reestructurado como protocolo de escritura y taxonomía de la bóveda (equivalente a `LEEME.md` de FOE), sin duplicar reglas de código.
   - `contexto/CLAUDE.md` migrado a `contexto/20 - Patrones/Convenciones de código.md` con frontmatter formal, sin ruta de OneDrive, sin WinForms y con inyección de dependencias pura por constructor.
   - `contexto/.control/equipo.json`: creado con integrantes `fernando`, `emanuel` y `carlos`.
   - `CONTACTO_ANTIGRAVITY.md`: unificado a proyecto `bimboproyecto`.

4. **Configuraciones de repositorio:**
   - `.codex/hooks.json`: rutas absolutas reemplazadas por rutas relativas al repo.
   - `.gitattributes`: configurado con `* text=auto`, y `eol=lf` para `.md`, `.js`, `.json`, `.yml`, `.sql`.
   - `.gitignore`: añadidas `.agents/teamwork/`, carpetas de corridas de agentes y `graphify-out/`.
   - `.agents/`: desindexadas del control de versiones (`git rm -r --cached`) todas las carpetas de corridas históricas (`auditor_*`, `challenger_*`, etc.), conservando `.agents/rules/` y `.agents/skills/`.

5. **Nota de QA:**
   - `contexto/60 - Revisiones QA/2026-11-09 - Pedido DevSecOps y contexto para Antigravity.md`: rutas relativas saneadas y `estado: cerrado`.

## Verificación

- `node -e "console.log(fs.readFileSync('AGENTS.md').length)"`: 7.851 B (<= 8.192 B).
- `git status` y `git ls-files .agents/`: confirmada la desindexación de corridas y preservación de `.agents/rules/` y `.agents/skills/`.
- `dotnet build BimboProyecto.sln`: 0 errores y 0 advertencias.

## Lo que no cambió

- Código fuente de la app (`CapaUI`, `CapaDatos`, `CapaDominio`, `CapaAplicacion4`).
- `App.config`, claves y configuración de producción.

## Pendientes

- R2: Portar validador `verificar.js`, `build-index.js`, tests y normalizar bitácora (rama `chore/contexto-validador`).
- R3: Dieta de contexto de arranque (rama `chore/contexto-arranque`).

## Relaciones

- [[Arquitectura Actual]]
- [[Convenciones de código]]
- [[2026-11-09 - Pedido DevSecOps y contexto para Antigravity]]
