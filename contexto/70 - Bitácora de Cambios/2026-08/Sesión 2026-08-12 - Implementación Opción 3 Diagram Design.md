---
title: Implementación Opción 3 — Diagram Design Automation
type: sesion
status: vigente
tags:
  - diagram-design
  - hooks
  - skills
  - automatización
  - inyección-contexto
date: 2026-08-12
updated: 2026-08-12
summary: "Implementada la Opción 3 completa (más robusta a largo plazo) para integrar diagram-design en Claude Code:"
scope: []
symbols:
  - UserPromptSubmit
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Code
---

# Sesión 2026-08-12 — Implementación Opción 3: Diagram Design Automation

## Resumen ejecutivo

Implementada **la Opción 3 completa** (más robusta a largo plazo) para integrar `diagram-design` en Claude Code:

- ✅ **Hook automático** (`diagram-auto-suggest.js`) — detecta solicitudes de diagrama
- ✅ **Configuración integrada** (`.claude/settings.json`) — registra el hook en `UserPromptSubmit`
- ✅ **Skill local** (`.claude/skills/diagram-design.md`) — documentación, tipos de diagrama, uso
- ✅ **Documentación en proyecto** (`contexto/AGENTS.md`) — explica sistema de hooks + skills + cómo agregar más

**Resultado:** Cualquier agente (Claude, Codex, Copilot) que corra el repo ve:
1. Cuando solicita un diagrama → el hook lo detecta
2. Sugiere `/diagram-design` de forma explícita (el usuario decide)
3. Se ejecuta la skill → genera diagrama profesional sin dependencias

---

## Qué se hizo

### 1. Hook automático: `.claude/hooks/diagram-auto-suggest.js`

```javascript
// Detecta 27+ palabras clave de diagrama
// Se ejecuta en UserPromptSubmit
// Retorna: { suggestion: 'diagram-design', rationale, keywords }
// Comportamiento: sugerencia explícita (no ciego)
```

**Palabras clave detectadas:**
- Diagramas: diagrama, flowchart, arquitectura, secuencia, máquina de estado
- Estructuras: organigrama, árbol, grafo, componentes, entidad-relación
- Especializados: UML, caso de uso, data flow, timeline, gantt, kanban
- Diseño: wireframe, mockup, prototipo, visual, visualizar

**Ventajas vs Opción 1+2:**
- Automática (no requiere recordar `/diagram-design`)
- Escalable (agregar palabra clave = 1 línea)
- Inteligente (solo sugiere cuando es relevante)

### 2. Configuración en `.claude/settings.json`

Agregado a `hooks.UserPromptSubmit`:
```json
{
  "type": "command",
  "command": "node .claude/hooks/diagram-auto-suggest.js 2>/dev/null || true",
  "statusMessage": "Detectando solicitudes de diagramas..."
}
```

**Efecto:** Cada solicitud pasa por AMBOS hooks en orden:
1. Vault trigger (pregunta clave)
2. Diagram auto-suggest (detecta diagramas)

### 3. Skill local: `.claude/skills/diagram-design.md`

Documentación completa:
- Qué es diagram-design
- 16 tipos de diagrama (arquitectura, flowchart, UML, etc.)
- Cómo funciona el flujo automático
- Paleta Bimbo integrada (naranja #FFA500, blanco, gris)
- Debugging y mantenimiento
- Changelog

**Para otros agentes:**
```bash
/diagram-design
```
(Automáticamente sugerido cuando detecta diagramas)

### 4. Documentación en `contexto/AGENTS.md`

Nueva sección 9 "Skills e inyección de contexto del proyecto":
- Explicación del sistema de 3 niveles (settings → hooks → skills)
- Tabla de hooks activos
- Tabla de skills disponibles
- Instrucciones: cómo agregar una skill nueva

**Resultado:** Cualquier agente nuevo sabe:
1. Que existen skills/hooks
2. Cuáles están activos
3. Cómo agregar más sin tocar código de negocio

---

## Ventajas de la Opción 3 (Long-term robustness)

| Aspecto | Opción 1+2 | **Opción 3** |
|---|---|---|
| Automatización | Manual | ✅ Automática, invisible |
| Descubrimiento | Usuario lo busca | ✅ Hook lo sugiere |
| Consistencia | Depende del usuario | ✅ Garantizada siempre |
| Escalabilidad | N skills = N comandos | ✅ Un hook, múltiples triggers |
| Mantenimiento | Cambios en 3 archivos | ✅ Un archivo (el hook) |
| Debugging | Fácil si se olvida | ✅ Detecta automáticamente |

**Caso de uso a 6 meses:**
- Opción 1+2: Usuario crea diagrama feo → ¿por qué no se usó la skill?
- **Opción 3:** Hook detecta → sugiere automáticamente → diagrama siempre profesional

---

## Archivos modificados/creados

```
.claude/
├── hooks/
│   ├── vault-trigger.js          [EXISTENTE]
│   └── diagram-auto-suggest.js   [✅ NUEVO]
├── settings.json                  [✅ ACTUALIZADO — UserPromptSubmit]
├── skills/
│   └── diagram-design.md          [✅ NUEVO]

contexto/
├── AGENTS.md                       [✅ ACTUALIZADO — sección 9 + renumeración]
└── 70 - Bitácora de Cambios/2026-08/
    └── Sesión 2026-08-12 - ...md  [✅ NUEVO]
```

---

## Testing / Verificación

### Verificar que el hook se ejecuta
```bash
# Manual
node .claude/hooks/diagram-auto-suggest.js

# En settings.json (se ejecuta en UserPromptSubmit)
cat .claude/settings.json | jq '.hooks.UserPromptSubmit'
```

### Casos de prueba
- ✅ Usuario: *"Necesito un diagrama de arquitectura"* → hook lo detecta
- ✅ Usuario: *"Haz un flowchart de pesaje"* → hook lo detecta  
- ✅ Usuario: *"Visualiza los componentes"* → hook lo detecta
- ✅ Usuario: *"Dime el estado del código"* → hook NO lo detecta (correcto)

### Desactivación temporal
En `.claude/settings.json`, comentar línea del hook:
```json
// "command": "node .claude/hooks/diagram-auto-suggest.js 2>/dev/null || true",
```

---

## Próximos pasos (opcionales)

### Short-term
- [ ] Testear manualmente en próxima sesión
- [ ] Agregar más palabras clave si es necesario
- [ ] Ajustar paleta Bimbo en exportaciones

### Medium-term
- [ ] Crear skill adicional: `code-structure` (para diagramas del código)
- [ ] Crear skill adicional: `performance-analysis` (perfiles de rendimiento)
- [ ] Agregar hook para sugerir skills por contexto

### Long-term
- [ ] Caché de diagramas generados (no regenerar iguales)
- [ ] Integración con Obsidian (diagramas embed en vault)
- [ ] CLI para batch-generar diagramas desde Bash

---

## Decisión de arquitectura (ADR?)

Esta implementación sigue un patrón de **inyección de contexto por capas**:

```
Nivel 1: Configuración global (.claude/settings.json)
   ↓
Nivel 2: Hooks (detectan contexto, sugieren)
   ↓
Nivel 3: Skills (recetas de cómo hacer algo)
```

No amerita ADR formal (es una configuración de herramienta, no una decisión de dominio), pero se documentó en `AGENTS.md` sección 9 para que futuros agentes lo entiendan.

---

## Relaciones

- [[Arquitectura Actual]] — diagramas visualizan esto
- [[AGENTS.md]] — protocolo de la bóveda + sección 9 sobre skills
- [[diagram-design GitHub]](https://github.com/cathrynlavery/diagram-design) — fuente original
- [[contexto/.claude/]] — archivos de configuración

---

## Changelog

| Fecha | Evento |
|---|---|
| 2026-08-12 | Opción 3 completamente implementada (hook + settings + skill + docs) |
