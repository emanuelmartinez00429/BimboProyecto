# Regla: Protocolo de Comunicación y Colaboración Bilateral (Antigravity ↔ Claude)

## Propósito
Garantizar la sincronización continua y sin fricciones entre los agentes de IA (Antigravity y Claude Code) que operan sobre Proyecto Bimbo, evitando desalineaciones, duplicación de esfuerzo o pérdida de hallazgos de auditoría.

## Canales Obligatorios (Ambos Requeridos)

Toda comunicación técnica, resolución de hallazgos o aviso entre agentes debe registrarse en **ambos canales simultáneamente**:

### 1. Engram (`mem_save`)
- **Namespace / Proyecto:** Usar explícitamente `project: "bimboproyecto"` (namespace de Claude) al guardar notas para Claude, o actualizar memorias previas con `mem_update`.
- **Tipo de Observación:** `discovery` o `pattern` (referencias de arquitectura/código), `bugfix` o `decision` (resolución de deuda o cambios de diseño).
- **Estructura Requerida:**
  - **Title:** Breve, específico y fácilmente indexable por FTS5 (ej. *"Resueltos pendientes de auditoría: EnumToBooleanConverter y Bitácora"*).
  - **What:** Qué se resolvió o descubrió.
  - **Why:** Contexto o auditoría que lo motivó.
  - **Where:** Archivos o rutas impactadas.
  - **Learned:** Conclusiones, edge cases o estado de pruebas.

### 2. Bóveda Obsidian (`contexto/`)
Registrar la intervención en la carpeta correspondiente donde Claude monitorea:
- **Auditorías y QA:** `contexto/60 - Revisiones QA/` (actualizar la sección de seguimiento del informe de auditoría correspondiente).
- **Deuda Técnica:** `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` (abrir o tachar `P-NNN`).
- **Bitácora de Sesión:** `contexto/70 - Bitácora de Cambios/2026-09/Sesión YYYY-MM-DD - [Título].md`.
- **Mensaje Directo a Claude:**
  ```markdown
  > [!note] Claude, si leés esto:
  > Explicación puntual indicando archivo:línea (ej. `CapaUI/Converters/EnumToBooleanConverter.cs:38`).
  ```
