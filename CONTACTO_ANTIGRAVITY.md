---
title: "Contacto Rápido — Antigravity"
description: "Acceso directo para avisar pendientes, hallazgos o cambios requeridos a Antigravity"
tags:
  - operativo
  - protocolo
  - antigravity
---

# ⚡ Avisar a Antigravity

> **Doble canal — van JUNTOS, nunca uno solo:**

## 1️⃣ Engram (lo revisa PRIMERO)
Proyecto: **`antigravity`**
- `mcp__engram__mem_save` con `project: "antigravity"`
- Rápido, revisor de Antigravity, auditable en tiempo real

## 2️⃣ Bóveda (fuente de verdad)
Ubicación: **`contexto/60 - Revisiones QA/`**
- Nota en markdown, visible en el repo
- Lo lee Antigravity **de seguido** después de Engram
- Queda registrado para auditorías futuras

---

## Plantilla rápida

**En Engram (proyecto `antigravity`):**
```
Pendiente: [descripción breve]
Archivo: [path]
Línea: [N o rango]
Causa: [por qué se hace ahora]
```

**En la bóveda (`contexto/60 - Revisiones QA/`):**
```markdown
---
title: "Hallazgo/Pendiente — [descripción]"
date: 2026-XX-XX
asignado_a: Antigravity
prioridad: [P-NNN o Crítico/Mayor/Menor]
---

## Descripción
[qué se encontró o qué hay que hacer]

## Dónde
- Archivo: `ruta/archivo.cs`
- Línea: NN
- Contexto: [código relevante o stack]

## Por qué
[razonamiento técnico]

## Resolución propuesta
[cómo arreglarlo, si se propone una]
```

---

## Referencia
- [[canal-comunicacion-antigravity]] — política de doble canal
- [[engram-first-on-status]] — Engram es el acceso directo para cualquier agente
