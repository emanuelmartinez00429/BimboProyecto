---
title: Plan — Preparar Bóveda Multi-Agente (AGENTS.md + protocolo)
type: sesion
status: vigente
tags:
  - plan
  - multi-agente
  - onboarding
  - documentacion
  - infraestructura
date: 2026-07-23
updated: 2026-07-23
summary: "Fase 1 (aditiva) y Fase 2 (movimiento) completas. La bóveda vive ahora en BimboProyecto/contexto/. Creados AGENTS.md (raíz), contexto/AGENTS.md (protocolo),…"
scope: []
symbols:
  - BaseModel
  - Fabricante
  - INotifyPropertyChanged
  - RepositorioBase
  - Result
  - TryAsync
estado: ejecutado
---

> [!success] Ejecutado 2026-07-23
> Fase 1 (aditiva) y Fase 2 (movimiento) completas. La bóveda vive ahora en `BimboProyecto/contexto/`. Creados `AGENTS.md` (raíz), `contexto/AGENTS.md` (protocolo), `_templates/`, y punteros para CLAUDE/Copilot/Cursor/Windsurf. `/Documentacion` absorbida, `CLAUDE.md` padre convertido en redirección, `.atl`/`.codegraph` desindexados (P-020 ✅). Build `dotnet build BimboProyecto.sln` → 0 errores. **Pendiente:** commit por el usuario.

# Plan — Preparar la bóveda para cualquier agente (Codex, opencode, Antigravity, Copilot, Claude…)

## Contexto

Actualmente **todo lo que un agente externo necesita para trabajar bien vive FUERA del repositorio git** `BimboProyecto`:

| Recurso | Ubicación actual | Problema |
|---|---|---|
| Contexto de código (`CLAUDE.md`) | `…\Proyecto de BIMBO\CLAUDE.md` (carpeta **padre** del repo) | No viaja en `git clone` |
| La bóveda completa (`ProyectoBimboContexto/`) | Carpeta **hermana** del repo, no versionada | No viaja en `git clone` |
| Protocolo de cómo documentar | Implícito, solo en la cabeza del agente que lo aprendió | No existe como archivo |
| `.github/copilot-instructions.md` | Existe pero es basura autogenerada (reglas Azure) | Inútil |
| `/Documentacion/Detector-de-Conexion.md` | Dentro del repo | Convención de docs paralela y fragmentada |
| `.atl/`, `.codegraph/` | Commiteados al repo (deuda P-020) | Ruido de tooling en git |

Además, las skills de Claude (`wiki-query`, `wiki-capture`) dependen de config local + Python y **no las tienen** Codex/opencode/Antigravity. Por eso el protocolo debe funcionar con **solo leer/escribir/grep sobre markdown** — las skills quedan como vía rápida opcional para Claude.

**Objetivo:** que cualquier agente que haga `git clone` reciba, sin configuración extra, (1) el contexto del sistema, (2) las reglas para no romperlo, y (3) el protocolo exacto para leer, clasificar y guardar conocimiento nuevo de forma uniforme.

**Decisiones tomadas (usuario, 2026-07-23):**
- La bóveda se mueve a `BimboProyecto/contexto/` (dentro del repo).
- Se absorben `/Documentacion` y el `CLAUDE.md` padre en la bóveda / `AGENTS.md`.

---

## Principio rector

**UNA fuente de verdad, muchos punteros finos.** Un solo `AGENTS.md` canónico (estándar cross-tool que leen Codex, opencode, Antigravity, Cursor, Zed, Aider). Cada archivo específico de herramienta es un puntero de 2 líneas hacia él → no hay instrucciones que diverjan entre herramientas.

---

## Estructura destino

```
BimboProyecto/                         (repo git)
├── AGENTS.md                          ← NUEVO. Punto de entrada universal (código + reglas + build)
├── CLAUDE.md                          ← NUEVO (puntero). "Ver AGENTS.md"
├── .github/copilot-instructions.md    ← REEMPLAZAR basura Azure → puntero a AGENTS.md
├── .cursor/rules/bimbo.mdc            ← NUEVO (puntero) para Cursor
├── .windsurfrules                     ← NUEVO (puntero) para Windsurf
├── .gitignore                         ← EDITAR: ignorar .atl/ .codegraph/ .vs/
├── contexto/                          ← LA BÓVEDA (movida desde ProyectoBimboContexto/)
│   ├── AGENTS.md                      ← NUEVO. Protocolo de la bóveda (anidado; Codex lo mergea)
│   ├── 00 - MOC/
│   │   └── Conocimiento Principal.md  ← EDITAR: enlazar el protocolo
│   ├── 10 - Arquitectura/
│   ├── 20 - Patrones/
│   ├── 30 - Casos de Uso/
│   ├── 40 - Proyecto Bimbo/
│   ├── 45 - Decisiones/
│   ├── 50 - Referencia/
│   │   └── Detector-de-Conexion.md    ← MOVIDO desde /Documentacion
│   ├── 70 - Bitácora de Cambios/
│   ├── _templates/                    ← NUEVO. Plantillas copy-paste
│   │   ├── plantilla-sesion.md
│   │   ├── plantilla-adr.md
│   │   ├── plantilla-patron.md
│   │   ├── plantilla-referencia.md
│   │   └── plantilla-deuda.md
│   └── CLAUDE.md                      ← se mantiene (contexto de código para Claude)
└── (Documentacion/ eliminada tras mover su contenido)
```

---

## Archivos a crear — contenido detallado

### 1. `AGENTS.md` (raíz del repo) — entrada universal

Secciones:
1. **Qué es** — 1 párrafo (portal interno Bimbo Honduras, .NET 8 WPF, Supabase) + link a `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`.
2. **Reglas de oro (no negociables):**
   - Solo trabajar en `CapaUI`. **Nunca** tocar `BimboPesaje` (referencia histórica).
   - `CapaAplicacion` **nunca** referencia `CapaDatos` (la flecha va al revés).
   - Columnas BD `snake_case` ↔ C# `camelCase` con `[Column]`; PK `[PrimaryKey]`; estado `1`=activo `2`=inactivo.
   - `Fabricante` **no** hereda `BaseModel` → nunca `client.From<Fabricante>()`.
   - MVVM con CommunityToolkit (`[ObservableProperty]`/`[RelayCommand]`), clase `partial`. Nunca `INotifyPropertyChanged` manual.
   - Repos nuevos: heredar `RepositorioBase` + `TryAsync` + `Result`.
3. **Build & verificación:** `dotnet build BimboProyecto.sln` (debe dar 0 errores). Sin harness de tests UI — verificación visual manual documentada por módulo.
4. **Orden de lectura para entrar en frío (onboarding):**
   1. este `AGENTS.md`
   2. `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`
   3. `contexto/CLAUDE.md` (convenciones de código)
   4. la nota del módulo que vas a tocar (`contexto/40 - Proyecto Bimbo/Módulo *.md`)
   5. `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` (qué está roto/pendiente)
5. **Antes de terminar:** documentar lo que hiciste → ver `contexto/AGENTS.md`.

### 2. `contexto/AGENTS.md` — protocolo de la bóveda (el corazón del plan)

El "cómo trabajo yo", explícito. Secciones:

**a) Taxonomía de carpetas — qué va en cada una:**

| Carpeta | Qué va aquí | Ejemplo |
|---|---|---|
| `10 - Arquitectura` | Conceptos arquitectónicos generales | Clean Architecture, SOLID |
| `20 - Patrones` | Patrones reutilizables del proyecto | Result Pattern, Base Repository con TryAsync |
| `30 - Casos de Uso` | Recetas end-to-end | CRUD con Paginación |
| `40 - Proyecto Bimbo` | Estado vivo del proyecto | Arquitectura Actual, Módulo X, Deuda Técnica |
| `45 - Decisiones` | ADRs (decisiones con trade-offs) | ADR-003 Disolución de CapaServicios |
| `50 - Referencia` | Hechos externos (SDKs, APIs, bugs de librerías) | Supabase .NET, Bug Filter OR |
| `70 - Bitácora de Cambios/AAAA-MM` | Notas de sesión con fecha | Sesión 2026-07-23 - … |

**b) Frontmatter obligatorio** (schema por tipo) — `title`, `tags`, `date`; sesiones agregan `branch`, `autor_cambios`; ADRs agregan `estado`.

**c) Convención de nombres:**
- Sesión: `Sesión AAAA-MM-DD - Título descriptivo.md`
- ADR: `ADR-NNN - Título.md` (NNN correlativo)
- Deuda: ítem `P-NNN` dentro de `Deuda Técnica - Pendientes.md` (no archivos sueltos)

**d) Reglas de enlazado:** todo nota relevante enlaza con `[[wikilink]]` y cierra con sección `## Relaciones`.

**e) Regla anti-duplicados (crítica para multi-agente):** ANTES de crear una nota, `grep`/buscar por el concepto en nombres de archivo y en `00 - MOC/Conocimiento Principal.md`. Si ya existe → **actualizar**, no duplicar.

**f) Casos de uso — "hice X → va en Y":**

| Hice… | Va en… | Cómo |
|---|---|---|
| Arreglé un bug | Nota de sesión (`70`) | plantilla-sesion; si revela deuda → P-NNN |
| Tomé una decisión arquitectónica | ADR (`45`) | plantilla-adr; enlazar desde Arquitectura Actual |
| Descubrí un patrón reutilizable | `20 - Patrones` | plantilla-patron |
| Encontré deuda sin arreglar | `Deuda Técnica` P-NNN | plantilla-deuda |
| Aprendí un hecho externo (bug SDK) | `50 - Referencia` | plantilla-referencia |
| Agregué/cambié un módulo | Actualizar Arquitectura Actual + nota del módulo | edición + `## Relaciones` |
| Revisé un PR/commit (QA) | Nota de sesión tipo "Revisión QA" | como la sesión de revisión de Emanuel |
| Refactoricé | Nota de sesión + actualizar patrón/arch afectado | — |

**g) Seguridad multi-agente concurrente:**
- Notas de sesión = archivo con fecha en el nombre → append-only, sin conflictos de merge entre agentes.
- Archivos compartidos (Deuda Técnica, Arquitectura Actual, Conocimiento Principal) = puntos calientes → ediciones chicas, un P-NNN por agente, insertar en los puntos documentados.
- Commit: cambios de docs junto al código que documentan, o claramente separados.

**h) Vía rápida opcional (solo Claude):** las skills `wiki-query`/`wiki-capture` existen; los demás agentes ignoran esta sección y usan markdown plano.

### 3. `contexto/_templates/*.md` — 5 plantillas copy-paste

Cada una con frontmatter correcto y secciones esqueleto, listas para que un agente las copie y rellene (sesión, ADR, patrón, referencia, deuda).

### 4. Punteros finos (2 líneas c/u)

- `CLAUDE.md` (raíz): "Este proyecto usa AGENTS.md como fuente única. Ver `./AGENTS.md` y `./contexto/AGENTS.md`."
- `.github/copilot-instructions.md`: reemplazar Azure junk por el mismo puntero.
- `.cursor/rules/bimbo.mdc`: front-matter `alwaysApply: true` + puntero.
- `.windsurfrules`: puntero.

---

## Operaciones de movimiento (parte física, requiere confirmación)

1. `git mv`/mover `ProyectoBimboContexto/*` → `BimboProyecto/contexto/` (la bóveda no es git aún → mover carpeta y `git add`).
2. Mover `BimboProyecto/Documentacion/Detector-de-Conexion.md` → `contexto/50 - Referencia/` y borrar `/Documentacion`.
3. Convertir el `CLAUDE.md` padre en el nuevo `AGENTS.md` del repo (su contenido de código va a `contexto/CLAUDE.md`, que ya existe y está más actualizado → consolidar, no duplicar).
4. `.gitignore`: agregar `.atl/`, `.codegraph/`, `.vs/`; luego `git rm -r --cached .atl .codegraph`.

> [!warning] Precauciones antes de mover
> - **Cerrar Obsidian** si tiene la bóveda abierta (mover un vault abierto puede corromper `.obsidian/workspace`).
> - La carpeta está en **OneDrive** → esperar a que sincronice tras el move.
> - Si algún agente usa `~/.obsidian-wiki/config`, actualizar la ruta (hoy apunta a otro vault, así que no bloquea).

---

## Orden de ejecución

1. **Aditivo primero (reversible, sin riesgo):** crear `AGENTS.md`, `contexto/AGENTS.md` (temporalmente en la ruta actual de la bóveda), `_templates/`, punteros, editar `.gitignore`.
2. **Movimiento (con confirmación):** mover bóveda → `contexto/`, absorber `/Documentacion` y `CLAUDE.md` padre.
3. **Ajuste de rutas:** corregir cualquier ruta absoluta en las notas que apunte a `ProyectoBimboContexto`.
4. **Commit** en una rama nueva (no directo a `master`).

## Verificación

- `git clone` en una carpeta limpia → confirmar que `AGENTS.md`, `contexto/` y las plantillas están presentes.
- Simular "agente en frío": seguir solo el orden de lectura del `AGENTS.md` y verificar que se puede llegar a Arquitectura Actual → módulo → deuda sin conocimiento previo.
- `dotnet build BimboProyecto.sln` sigue en 0 errores (el move de docs no toca código).
- `git status` limpio salvo los archivos intencionales; `.atl/`/`.codegraph/` ya no aparecen.

## Relaciones

- [[Arquitectura Actual]] — contexto de código que el AGENTS.md referenciará
- [[Deuda Técnica - Pendientes]] — P-020 (artefactos tooling) se resuelve aquí; onboarding la usará
- [[CLAUDE]] — se consolida en AGENTS.md
- [[Conocimiento Principal]] — dashboard que enlazará el protocolo
