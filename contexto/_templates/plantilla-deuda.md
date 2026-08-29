---
title: "Plantilla — ítem de deuda técnica"
type: plantilla
status: vigente
tags:
  - plantilla
date: AAAA-MM-DD
updated: AAAA-MM-DD
summary: "Formato de un ítem P-NNN. La deuda no son archivos sueltos: va dentro de Deuda Técnica - Pendientes."
scope: []
symbols: []
---

# Plantilla — ítem de deuda técnica

> [!warning] Esto NO es una nota suelta
> Copiá el bloque de abajo **dentro** de [[Deuda Técnica - Pendientes]], en la sección de severidad que
> corresponda, y agregá su fila en la tabla de historial al final. Un `P-NNN` por agente, sin reservar rangos.

```markdown
### P-NNN · 🔴|🟡|🟢 Título corto del problema

**Detectado en:** [[Sesión AAAA-MM-DD - …]]
**Archivo:** `CapaX/ruta/Archivo.cs`

Qué está mal y por qué importa. Si hay código, mostralo — antes y después.

**Solución de fondo:** qué habría que hacer, no un parche.

**Riesgo:** alto | medio | bajo — y qué pasa si no se arregla.

**Estado:** `[ ] Pendiente`
```

Fila para la tabla de historial:

```markdown
| P-NNN | Descripción corta | `[ ]` Pendiente | [[Sesión AAAA-MM-DD - …]] |
```

---

## Relaciones

- [[Deuda Técnica - Pendientes]]
- [[AGENTS]]
