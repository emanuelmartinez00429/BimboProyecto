---
title: AGENTS.md — Protocolo de la Bóveda (Proyecto Bimbo)
type: protocolo
status: vigente
tags:
  - nota
date: 2026-07-23
updated: 2026-08-23
summary: "Protocolo de la bóveda: taxonomía, frontmatter obligatorio, nombres, anti-duplicados, concurrencia entre agentes, hooks y skills."
summary_fijo: true
scope:
  - CapaUI/Formularios/Principal/Pantallas/Pesaje
symbols:
  - PostToolUse
---

# AGENTS.md — Protocolo de la Bóveda (Proyecto Bimbo)

> **Para cualquier agente** (Claude, Codex, opencode, Antigravity, Copilot, Cursor…).
> El contrato del proyecto está en [`../AGENTS.md`](../AGENTS.md). **Este archivo dice cómo clasificar y guardar** lo que hagas.
> Funciona con solo leer, escribir y hacer grep sobre markdown — no requiere herramientas especiales.
> Leelo cuando vayas a **escribir** en la bóveda. Para *leer*, usá [`INDEX.md`](INDEX.md).

---

## 1. Taxonomía — qué va en cada carpeta

| Carpeta | Qué va aquí | NO va aquí |
|---|---|---|
| `10 - Arquitectura` | Conceptos arquitectónicos generales (Clean Architecture, SOLID) | Estado específico del proyecto |
| `20 - Patrones` | Patrones **reutilizables** del proyecto (Result Pattern, TryAsync) | Decisiones puntuales |
| `30 - Casos de Uso` | Recetas end-to-end (CRUD con paginación) | — |
| `40 - Proyecto Bimbo` | **Estado vivo**: Arquitectura Actual, notas de Módulo, Deuda Técnica | Hechos externos genéricos |
| `45 - Decisiones` | **ADRs** — decisiones con trade-offs y alternativas descartadas | Cambios sin decisión de fondo |
| `50 - Referencia` | Hechos **externos** (SDKs, APIs, bugs de librerías, WPF) | Lógica del proyecto |
| `60 - Revisiones QA` | Planes y hallazgos de QA | Notas de sesión normales |
| `70 - Bitácora de Cambios/AAAA-MM` | **Notas de sesión** con fecha | Conocimiento atemporal |
| `_templates` | Plantillas copy-paste | Contenido de trabajo |
| `.control/` | Archivos de control que leen los hooks. **No es parte del grafo.** | Notas |

**Regla de decisión rápida:**
¿Hice algo hoy? → **nota de sesión** en `70`. · ¿Reveló una decisión de fondo? → además un **ADR** en `45`.
¿Descubrí un patrón que se repetirá? → además una nota en `20`. · ¿Encontré algo roto sin arreglar? → **ítem `P-NNN`** en Deuda Técnica.
¿Aprendí un hecho externo (bug de SDK)? → nota en `50`.

---

## 2. Frontmatter obligatorio

Toda nota empieza con frontmatter YAML. **Es lo que la convierte en un nodo indexable** — sin él, la nota es
invisible para `INDEX.md` y ningún agente puede saber que existe sin abrirla entera.

```yaml
---
title: "Título legible"
type: sesion            # sesion | adr | patron | referencia | modulo | deuda | caso | arquitectura | qa | moc
status: vigente         # vigente | superseded | archivado
tags: [tag1, tag2]
date: 2026-08-23        # cuándo nació. Fechas absolutas, nunca "hoy" ni "ayer"
updated: 2026-08-23     # última vez que se tocó el contenido
summary: "Una frase declarativa: qué resuelve esta nota."
scope:                  # rutas de código que esta nota describe
  - CapaUI/Formularios/Principal/Pantallas/Pesaje
symbols:                # clases, interfaces y servicios que esta nota declara
  - PesajeViewModel
  - IPesajeRepository
---
```

Campos extra por tipo:

- **Sesión:** `branch:`, `autor_cambios:` (quién hizo el cambio) y `revisor:` si es QA.
- **ADR:** `estado:` (propuesto / aceptado / reemplazado) y `supersedes: [ADR-00N]` si reemplaza a otro.
- **Referencia / Patrón:** `lifecycle:` opcional (draft / verified / archived).

### Por qué `scope` y `symbols` importan

Son el enlace **código ↔ nodo**. Cuando cambiás la estructura del código, el hook `PostToolUse` cruza los
archivos que tocaste contra estos campos y te dice qué notas quedaron desactualizadas. Si los dejás vacíos,
esa detección no funciona y la nota se desincroniza en silencio. `summary` es la línea que publica el índice:
escribila una vez, bien, y se reutiliza en cada consulta.

---

## 3. Convención de nombres

| Tipo | Formato | Ejemplo |
|---|---|---|
| Sesión | `Sesión AAAA-MM-DD - Título descriptivo.md` | `Sesión 2026-07-23 - Revisión QA Módulo Usuarios.md` |
| ADR | `ADR-NNN - Título.md` (NNN correlativo) | `ADR-004 - GhostTextBox.md` |
| Deuda | ítem `P-NNN` **dentro** de `Deuda Técnica - Pendientes.md` | `P-013 · Regresión auditoría` |
| Patrón / Referencia | Título descriptivo directo | `Base Repository con TryAsync.md` |

La deuda técnica **nunca** son archivos sueltos: se registra como ítem numerado `P-NNN` dentro del documento
maestro, con su entrada en la tabla de historial al final. Los ítems ya resueltos se archivan en
[[Deuda Técnica - Resueltas 2026]] para que el documento abierto no cargue historia.

Para nombres **nuevos**, evitá acentos y caracteres especiales en la ruta: los wikilinks salen escapados
(`%C3%A9`) y complica el grep entre Windows, WSL y CI. Las notas existentes no se renombran de golpe —
romperían enlaces en cascada.

---

## 4. Enlazado y relaciones

- Enlazá conceptos con `[[wikilink]]` (nombre del archivo sin extensión).
- **Toda nota cierra con una sección `## Relaciones`** listando los `[[enlaces]]` relevantes.
- Enlazá liberalmente: un `[[nombre]]` que aún no existe marca algo por escribir, no es un error.

---

## 5. Regla anti-duplicados (CRÍTICA en multi-agente)

**Antes de crear una nota, buscá si ya existe.** Varios agentes trabajan sin conocerse entre sí; la duplicación es el riesgo #1.

1. Buscá el concepto en [`INDEX.md`](INDEX.md) — está el título y el `summary` de las 185 notas, es una sola lectura.
2. Si ya existe una nota del tema → **actualizala** y subí `updated:`, no crees una nueva.
3. Si dudás entre dos nombres para el mismo concepto → usá el que ya exista.

---

## 6. Casos de uso — «hice X → va en Y → con formato Z»

| Hice… | Va en… | Cómo |
|---|---|---|
| Arreglé un bug | Nota de sesión (`70`) | `plantilla-sesion`; si revela deuda → agregá `P-NNN` |
| Tomé una decisión arquitectónica | ADR (`45`) | `plantilla-adr`; enlazala desde `Arquitectura Actual` |
| Descubrí un patrón reutilizable | `20 - Patrones` | `plantilla-patron` |
| Encontré deuda que no arreglé | `Deuda Técnica` `P-NNN` | `plantilla-deuda` |
| Aprendí un hecho externo (bug SDK, quirk de API) | `50 - Referencia` | `plantilla-referencia` |
| Agregué o cambié un módulo | `Arquitectura Actual` **+** la nota del módulo | editar + actualizar `symbols` y `updated` |
| Revisé un PR o commit ajeno (QA) | Nota de sesión «Revisión QA» | `plantilla-sesion` con `revisor:` |
| Refactoricé | Nota de sesión **+** actualizá el patrón o arquitectura afectado | — |
| Resolví un `P-NNN` | Movelo a `Deuda Técnica - Resueltas 2026` y actualizá la tabla de historial | — |
| **Cambio puramente visual** (espaciados, estilos XAML) | **Nada.** Solo código y commit | Salvo que revele un patrón o un gotcha: eso sí va, en `20` o `50` |

**Después de escribir, regenerá el índice:** `node scripts/build-index.js`

---

## 7. Seguridad multi-agente concurrente

- **Notas de sesión** = archivo con fecha en el nombre → cada agente crea el suyo, append-only, sin conflictos de merge. Preferí crear una sesión nueva antes que editar la de otro.
- **Archivos compartidos** (`Deuda Técnica`, `Arquitectura Actual`, `Conocimiento Principal`) son puntos calientes: ediciones chicas y localizadas, **un `P-NNN` por agente** (no reserves rangos), insertá al final de la lista o de la tabla.
- **`INDEX.md` es generado.** Nunca lo edites a mano ni resuelvas conflictos en él: regeneralo.
- **Commit:** la documentación va junto al código que documenta, o en un commit separado con mensaje `docs: …`.

---

## 8. Colores del grafo (Obsidian)

`.obsidian/graph.json` está **versionado** a propósito: cualquiera que clone el repo y abra `contexto/` como
bóveda ve el grafo coloreado por carpeta sin configurar nada.

`00 - MOC` azul · `10 - Arquitectura` naranja · `20 - Patrones` rojo · `30 - Casos de Uso` teal ·
`40 - Proyecto Bimbo` verde · `45 - Decisiones` amarillo · `50 - Referencia` morado ·
`70 - Bitácora de Cambios` rosa · `_templates` gris.

Si agregás una carpeta de primer nivel nueva, sumale una entrada a `colorGroups` con un color no usado.
El skill `graph-colorize` lo automatiza. Los backups `graph.json.backup-*` están gitignoreados.

---

## 9. Hooks y skills

Tres niveles de inyección de contexto, compartidos por todos los agentes vía `.claude/` y su espejo `.codex/`:

| Hook | Evento | Qué hace |
|---|---|---|
| `session-context.js` | SessionStart | Inyecta `.control/handshake.md` + `INDEX.md`. **No** inyecta la bóveda entera. |
| `vault-trigger.js` | UserPromptSubmit | Detecta preguntas del handshake y relee `.control/handshake.md` en vivo. |
| `diagram-auto-suggest.js` | UserPromptSubmit | Detecta solicitudes de diagrama y sugiere `/diagram-design`. |

**Para agregar una automatización nueva:** creá `.claude/hooks/NOMBRE.js`, registralo en `.claude/settings.json`,
copialo a `.codex/hooks/` y registralo en `.codex/hooks.json`, y sumá la fila a esta tabla. Un hook es
determinístico; una instrucción en markdown depende de que el modelo se acuerde.

### Skills — son para todos los agentes, no solo para Claude

Las skills viven en `~/.agents/skills/` (agent-agnósticas) y en `.claude/skills/` (del proyecto). **No son un
atajo opcional de Claude: son el camino estándar** para leer y mantener la bóveda. Las más relevantes acá:

| Skill | Para qué |
|---|---|
| `wiki-context-pack` | Genera un paquete de contexto acotado por tokens sobre un tema. **Es el paso 0 de cualquier tarea.** |
| `wiki-query` | Responde preguntas sobre la bóveda con citas, sin abrir todo |
| `wiki-lint` | Audita: wikilinks rotos, notas huérfanas, frontmatter faltante, contradicciones |
| `wiki-dedup` | Detecta y fusiona notas que cubren el mismo concepto con nombres distintos |
| `cross-linker` | Agrega las referencias cruzadas que faltan tras una ingesta grande |
| `memory-bridge` | Compara qué sabe cada herramienta: «qué sabe Codex que Claude no sabe» |

Un agente que no soporte skills hace lo mismo a mano siguiendo las reglas de arriba: el resultado es el mismo,
solo que más lento.

---

## Relaciones

- [[Conocimiento Principal]] — dashboard para humanos
- [[Arquitectura Actual]] — estado vivo del sistema
- [[Deuda Técnica - Pendientes]] — deuda abierta `P-NNN`
- [[Deuda Técnica - Resueltas 2026]] — archivo histórico de deuda cerrada
- [[CLAUDE]] — convenciones de código detalladas
