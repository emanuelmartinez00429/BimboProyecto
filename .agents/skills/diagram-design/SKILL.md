---
name: diagram-design
description: Genera diagramas profesionales con diagram-design — 29 tipos visuales sin dependencias externas
tags: [visual, diagrams, architecture, svg, design]
---

# Skill: diagram-design

## Descripción

Sistema de diseño para crear **diagramas editoriales de calidad profesional** sin herramientas externas (Figma, Mermaid complicado, etc.). Proporciona 29 tipos visuales autocontenidos en HTML+SVG que se renderizan en navegador y se exportan a PNG/SVG.

**Fuente:** https://github.com/cathrynlavery/diagram-design

---

## Cuándo usar esta skill

Usá `/diagram-design` automáticamente cuando:
- El usuario pide **un diagrama** (cualquier tipo: arquitectura, flujo, secuencia, etc.)
- Mencioná palabras clave: *diagrama, flowchart, arquitectura, secuencia, organigrama, árbol, grafo, componentes, entidad-relación, UML, caso de uso, data flow, wireframe, timeline, gantt, kanban, visual, visualizar*
- El hook automático `diagram-auto-suggest.js` lo sugiere

**No hay que invocarla manualmente** — el hook la detecta y la sugiere.

---

## Cómo funciona

### 1. Detectar tipo de diagrama
Identifica qué tipo de visualización necesita el usuario:

| Tipo | Ejemplo | Uso |
|---|---|---|
| **Arquitectura** | Sistema de capas, microservicios | Infraestructura, componentes |
| **Flowchart** | Decisiones, flujos de procesos | Workflows, algoritmos |
| **Secuencia** | Interacciones entre actores | Comunicación, eventos |
| **Máquina de estado** | Transiciones | Ciclo de vida, estados |
| **Organigrama** | Jerarquía | Estructura, equipos |
| **Árbol/Grafo** | Relaciones no lineales | Dependencias, redes |
| **Entidad-Relación** | Bases de datos | Esquemas, relaciones |
| **Línea de tiempo** | Cronología | Hitos, fases |
| **Gantt** | Cronograma | Proyectos, tasks |
| **Kanban** | Tareas por estado | Flujo de trabajo |
| **Clase (UML)** | Atributos, métodos | Estructuras OOP |
| **Caso de uso** | Actores, acciones | Especificaciones |
| **Data flow** | Flujo de información | Pipelines, procesos |
| **Wireframe** | Layout | UI, prototipos |
| **Mockup** | Prototipo visual | Diseño, interfaces |
| **Componentes** | Bloques funcionales | Módulos, subsistemas |

### 2. Generar plantilla HTML+SVG
Crear plantilla autocontenida con:
- **SVG inline** — no requiere assets externos
- **CSS embebido** — estilos locales, respetan temas (light/dark)
- **Código limpio** — sin dependencias de librerías
- **Accesibilidad** — roles ARIA, títulos, descripciones

### 3. Aplicar marca Bimbo
Integrar paleta corporativa:
- Colores: blanco, naranja BIMBO (`#FFA500`), gris, azules
- Tipografía: fuentes seguras (Arial, Segoe UI, sans-serif)
- Espaciado y proporciones: profesional, aireado

### 4. Exportar formatos
Entregar en:
- **HTML** — abrirse en navegador, interactivo
- **SVG** — escalable, editable en herramientas
- **PNG** — rasterizado, embedding en documentos

---

## Ejemplo: Diagrama de arquitectura Bimbo

**Solicitud:**
> Necesito un diagrama de la arquitectura actual de Bimbo (CapaUI, CapaAplicacion, CapaDatos)

**Pasos:**
1. Detectar: arquitectura de capas
2. Generar: SVG con 3 cajas verticales: CapaUI → CapaAplicacion ← CapaDatos + CapaDominio
3. Marca: bordes naranja BIMBO, fondo blanco, texto oscuro
4. Exportar: HTML + SVG + PNG

**Resultado:**
```
┌─────────────────────────────────┐
│      CapaUI (WPF)               │  ← Arriba
└──────────────┬──────────────────┘
               │
    ┌──────────▼────────────┐
    │  CapaAplicacion       │  ← Centro
    │  (Interfaces, DTOs)   │
    └──────────────┬────────┘
               ┌───┴────────────┐
          ┌────▼────────┐   ┌───▼──────────┐
          │ CapaDatos   │   │ CapaDominio  │  ← Abajo
          │ (Supabase)  │   │ (Entidades)  │
          └─────────────┘   └──────────────┘
```

---

## Configuración por defecto (convención de esta skill, no una clave real de `.claude/settings.json`)

> ⚠️ Esto **no** es JSON que el harness de Claude Code lea — `.claude/settings.json` solo reconoce
> `hooks` y un puñado de claves fijas; agregar una clave arbitraria como `diagramDefaults` ahí no
> tiene ningún efecto. Este bloque es la convención que el agente debe aplicar **a mano** al generar
> cada diagrama (colores, formatos, tema) — se documenta acá como valores por defecto, no como config
> parseada por ninguna herramienta.

```json
{
  "theme": "bimbo-corporate",
  "brand": {
    "primaryColor": "#FFA500",
    "secondaryColor": "#FFFFFF",
    "accentColor": "#333333"
  },
  "autoExport": ["html", "svg"],
  "responseFormat": "artifact"
}
```

**Significado:**
- `theme: "bimbo-corporate"` — paleta Bimbo
- `autoExport: ["html", "svg"]` — formatos por defecto a ofrecer
- `responseFormat: "artifact"` — devolver en Artifact para visualizar en vivo

---

## Integración con flujo de trabajo

### El flujo típico

1. **Usuario solicita diagrama** → *"Necesito un diagrama de flujo de pesaje"*
2. **Hook `diagram-auto-suggest.js` lo detecta** → sugiere `/diagram-design`
3. **Claude abre la skill** → identifica tipo (flowchart)
4. **Genera HTML+SVG** → embebido en Artifact
5. **Usuario ve resultado** → puede descargar PNG/SVG o editar

### Automatización en el repo

El hook está **siempre activo**:
- Se ejecuta en cada `UserPromptSubmit`
- **No invade** — solo sugiere, el usuario decide
- **No es ciego** — explícita, con rationale
- **Es escalable** — agregar más palabras clave sin cambiar código

### Ventajas

| Aspecto | Beneficio |
|---|---|
| **Consistencia** | Todos los diagramas siguen la marca Bimbo |
| **Automatización** | El usuario no tiene que recordar `/diagram-design` |
| **Escalabilidad** | Un hook maneja N módulos, N tipos de diagrama |
| **Mantenibilidad** | Un solo archivo a actualizar si cambian palabras clave |
| **Reversibilidad** | Fácil de desactivar o ajustar sin tocar código de negocio |

---

## Debugging / Mantenimiento

### Verificar que el hook se ejecuta
El hook lee el prompt por STDIN (mismo contrato que `vault-trigger.js`) e imprime
`{hookSpecificOutput: {...}}` en STDOUT solo si detecta palabras clave de diagrama:
```bash
echo '{"prompt":"hazme un diagrama de flujo del login"}' | node .claude/hooks/diagram-auto-suggest.js
```
Si no imprime nada con un prompt que sí menciona un diagrama, el hook está roto — revisar que lea
`process.stdin` y llame a `console.log(JSON.stringify(...))`, no que solo declare `module.exports`.

### Agregar más palabras clave
Editar array `diagramKeywords` en `.claude/hooks/diagram-auto-suggest.js` — no requiere reiniciar nada.

### Desactivar temporalmente
En `.claude/settings.json`, comentar la línea del hook en `UserPromptSubmit`:
```json
// "command": "node .claude/hooks/diagram-auto-suggest.js 2>/dev/null || true",
```

### Cambiar tema/marca
Editar `diagramDefaults` en `.claude/settings.json` — aplica a todos los diagramas futuros.

---

## Relaciones

- [[Arquitectura Actual]] — diagramas visualizan la arquitectura
- [[AGENTS.md]] — protocolo de documentación de skills
- [[diagram-design en GitHub]](https://github.com/cathrynlavery/diagram-design) — repo oficial

---

## Changelog

| Fecha | Evento |
|---|---|
| 2026-08-12 | Skill creada, hook automático configurado |
| 2026-08-12 | Fix de instalación: la skill vivía como archivo suelto `.claude/skills/diagram-design.md`, sin la carpeta contenedora que requiere el harness (`<nombre>/SKILL.md`) — por eso no aparecía como invocable. Se movió a `.claude/skills/diagram-design/SKILL.md`. Además `.claude/skills/` estaba gitignoreado por completo (`.claude/*` sin whitelist), así que ni siquiera viajaba con `git clone` — se agregó `!.claude/skills/` y `!.claude/skills/**` a `.gitignore`. El hook `diagram-auto-suggest.js` tampoco funcionaba: solo declaraba `module.exports` sin leer STDIN ni imprimir nada, así que `node .claude/hooks/diagram-auto-suggest.js` no producía ninguna salida — se reescribió siguiendo el mismo contrato que `vault-trigger.js` (lee `process.stdin`, imprime `{hookSpecificOutput:{hookEventName,additionalContext}}`). También se corrigió la sección "Configuración" del doc, que presentaba `diagramDefaults` como si fuera una clave real de `.claude/settings.json` — no lo es, esa clave no existe en el schema del harness. |
