# Protocolo de escritura en `contexto/`

Léelo cuando vayas a **escribir o clasificar notas** en `contexto/`. Para **leer**, entra por [`INDEX.md`](INDEX.md).
Funciona con solo leer, escribir y buscar en markdown: no requiere herramientas especiales ni Obsidian.
Las reglas de código, build y arquitectura del sistema viven en [`../AGENTS.md`](../AGENTS.md).

---

## 1. Taxonomía — qué va en cada carpeta

| Carpeta | Va aquí | No va aquí |
| :-- | :-- | :-- |
| `00 - MOC` | Mapas de contenido y dashboard (`Conocimiento Principal.md`) | Notas detalladas de implementación |
| `10 - Arquitectura` | Conceptos y directrices de arquitectura general | Estado vivo de componentes Bimbo |
| `20 - Patrones` | Patrones reutilizables de UI y código (`Convenciones de código.md`, `Convenciones de UI`) | Decisiones puntuales o historias |
| `30 - Casos de Uso` | Flujos end-to-end y casos de uso operativos | Estado de deuda o bugs |
| `40 - Proyecto Bimbo` | Estado vivo: `Arquitectura Actual.md`, fichas de módulos, `Deuda - Abierta.md` | Notas históricas de sesión |
| `45 - Decisiones` | Registros de decisión arquitectónica (`ADR-NNN`) | Cambios rutinarios sin alternativas |
| `50 - Referencia` | Hechos externos, SDKs, bugs de librerías, guías de configuración | Código de la aplicación |
| `60 - Revisiones QA` | Auditorías de calidad, revisiones entre agentes, hallazgos | Código fuente o notas de sesión rutinarias |
| `70 - Bitácora de Cambios/AAAA-MM/` | Notas de sesión por mes (`AAAA-MM-DD-autor-titulo-corto.md`) y resúmenes mensuales | Conocimiento atemporal |
| `_templates` | Plantillas para crear notas (sesión, ADR, deuda, patrón, referencia) | Notas de trabajo reales |

**Regla de decisión rápida:**
- ¿Hiciste algo hoy? → **nota de sesión** en `70 - Bitácora de Cambios/AAAA-MM/`.
- ¿Reveló una decisión de diseño de fondo? → además un **ADR** en `45 - Decisiones/`.
- ¿Descubriste un patrón reusable? → además una nota en `20 - Patrones/`.
- ¿Encontraste algo roto que no arreglaste? → ítem **`P-NNN`** en `40 - Proyecto Bimbo/Deuda - Abierta.md`.
- ¿Resolviste un `P-NNN`? → mover a `Deuda - Resuelta.md` con fecha y evidencia.
- ¿Aprendiste un hecho externo? → nota en `50 - Referencia/`.

---

## 2. Frontmatter obligatorio

Toda nota empieza con bloque YAML:

```yaml
---
title: "Título descriptivo y legible"
type: sesion           # sesion | resumen | adr | patron | referencia | arquitectura | qa | deuda | moc
status: vigente        # vigente | borrador | reemplazado | archivado
date: AAAA-MM-DD
updated: AAAA-MM-DD
summary: "Frase declarativa concisa que describe el contenido (se publica en el índice)."
autor: alias           # tu id en contexto/.control/equipo.json (fernando, emanuel, carlos)
---
```

Campos obligatorios según el tipo:
- **Sesión (`type: sesion`):** agrega obligatoriamente `agente:` (`claude-code`, `codex`, `antigravity`, `gemini-cli`, `copilot`, `cursor`, `opencode`, `humano`), `rama:`, `commits: [...]`, `tags: [sesion]`. Campo `modelo:` opcional.
- **Resumen mensual (`type: resumen`):** 10 a 15 líneas condensando el mes cerrado (`AAAA-MM-resumen.md`).
- **ADR (`type: adr`):** agrega `estado:` (`propuesto`, `aceptado`, `reemplazado`).
- **Nota reemplazada:** debe incluir `reemplazado_por: [[nombre-de-nota-nueva]]`.

Fechas siempre en formato absoluto `AAAA-MM-DD`. Nunca "hoy" ni "ayer".

---

## 3. Convenciones de nombres e identificadores

- **Sesiones nuevas:** `70 - Bitácora de Cambios/AAAA-MM/AAAA-MM-DD-autor-titulo-corto.md`. Ejemplo: `2026-11-09-fernando-contrato-unico-agentes.md`.
- **ADR:** `ADR-NNN - Título.md` (NNN correlativo de 3 dígitos).
- **Deuda técnica:** ítems `P-NNN` dentro de `Deuda - Abierta.md` o `Deuda - Resuelta.md`. No crear archivos sueltos por P-NNN.
- **Identificadores únicos:** cada `ADR-NNN` y `P-NNN` es único en todo el repositorio.

---

## 4. Búsqueda antes de crear (anti-duplicación)

1. Consulta [`INDEX.md`](INDEX.md) y [`INDEX-bitacora.md`](INDEX-bitacora.md).
2. Si el concepto ya existe, actualiza la nota existente y actualiza `updated: AAAA-MM-DD`.
3. Si dudas entre dos nombres, utiliza el ya existente en el grafo.

---

## 5. Trabajo concurrente y colaborativo

- **Notas de sesión:** son estrictamente append-only. Cada autor/agente crea la suya sin editar sesiones ajenas.
- **Puntos calientes compartidos (`Deuda - Abierta.md`, `Arquitectura Actual.md`):** realizar ediciones atómicas y localizadas.
- **Los índices `INDEX.md` e `INDEX-bitacora.md`:** nunca se resuelven a mano en un merge conflict; se regeneran corriendo `node scripts/contexto/build-index.js`.
- La documentación debe commitearse junto al cambio que documenta.

---

## 6. Enlazado y relaciones

- Enlaza notas usando `[[nombre-del-archivo]]` (sin extensión `.md`).
- Toda nota debe cerrar con una sección `## Relaciones` listando sus vínculos relevantes en viñetas `[[...]]`.

---

## 7. Higiene y ciclo de vida

- Lo que se carga al arrancar debe reflejar lo **vigente**, no la acumulación histórica.
- Si una nota queda obsoleta:
  - Si es reemplazada por otra: marca `status: reemplazado` y `reemplazado_por: [[nueva-nota]]`.
  - Si ya no aplica pero tiene valor histórico: marca `status: archivado`.
- Las notas `reemplazado` y `archivado` son excluidas automáticamente del índice de arranque `INDEX.md`.
- **Datos prohibidos:** nunca escribir rutas absolutas de disco (`C:\`, `D:\`, `/c/`), números telefónicos reales, correos personales no autorizados, ni tokens o secretos (`service_role`, `sb_secret_`, JWT).

---

## 8. Verificación tras editar

Tras cualquier cambio en notas o frontmatter:

```bash
node scripts/contexto/build-index.js   # regenera INDEX.md e INDEX-bitacora.md
node scripts/contexto/verificar.js     # comprueba integridad (0 errores)
```

## Relaciones

- [[Conocimiento Principal]]
- [[Arquitectura Actual]]
- [[Deuda - Abierta]]
- [[Convenciones de código]]
