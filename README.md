# Proyecto Bimbo Honduras

Portal interno de gestión (.NET 8 · WPF · Supabase). El **contexto completo del proyecto y su base de conocimiento viajan dentro de este repo**, en [`contexto/`](contexto/), para que cualquier persona —y cualquier agente de IA— trabaje con la misma información.

---

## 🚀 Empezar aquí (humanos y agentes)

**La regla #1: antes de tocar código, se lee la bóveda.** Todo lo necesario está en **[`AGENTS.md`](AGENTS.md)**.

### 1. Clonar el repo (esto ya incluye la bóveda)

```bash
git clone https://github.com/warthunderlover/BimboProyecto.git
cd BimboProyecto
```

No hay que clonar la bóveda por separado: **es la carpeta `contexto/` dentro del repo.**

### 2. Abrir la bóveda en Obsidian (opcional, para leerla cómodo)

1. Obsidian → **"Abrir carpeta como bóveda"** (*Open folder as vault*).
2. Elegí la carpeta **`BimboProyecto/contexto/`**.
3. Listo — el grafo, los `[[enlaces]]` y las notas funcionan.

> No es obligatorio usar Obsidian: la bóveda son archivos `.md` normales; cualquiera los lee con un editor de texto o `grep`.

### 3. Arrancar CUALQUIER agente de IA (Claude, Codex, opencode, Antigravity, Copilot, Cursor…)

**Pegá este mensaje como PRIMER prompt de la sesión, siempre, antes de pedir cualquier tarea:**

```
Antes de hacer absolutamente nada: leé AGENTS.md (raíz) y contexto/AGENTS.md.
Resumime en 3–4 líneas las reglas de oro y el orden de lectura de onboarding.
Recién después de eso empezamos con la tarea.
```

El resumen que te devuelva confirma que **sí leyó** la bóveda antes de actuar. Si no menciona las reglas de oro (solo `CapaUI`, `CapaAplicacion` no referencia `CapaDatos`, etc.), volvé a pedírselo antes de darle trabajo.

### Lectura automática por herramienta

La mayoría de los agentes ya leen su archivo de reglas al arrancar (todos apuntan al mismo `AGENTS.md`):

| Herramienta | Archivo que lee solo |
|---|---|
| Codex, opencode, Antigravity, Zed, Aider | `AGENTS.md` |
| Claude Code | `CLAUDE.md` → `AGENTS.md` |
| GitHub Copilot | `.github/copilot-instructions.md` |
| Cursor | `.cursor/rules/bimbo.mdc` |
| Windsurf | `.windsurfrules` |

Aun así, **el prompt de arranque del paso 3 es la garantía** — no todos los agentes leen su archivo de forma 100% confiable en cada sesión.

---

## 🛠️ Build

```bash
dotnet build BimboProyecto.sln
```

Debe dar **0 errores** (hay warnings preexistentes de nullable, no bloquean).

---

## 📚 Dónde está cada cosa

| Necesito… | Ir a… |
|---|---|
| Reglas + build + onboarding | [`AGENTS.md`](AGENTS.md) |
| Arquitectura vigente | `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md` |
| Convenciones de código | `contexto/CLAUDE.md` |
| Qué está roto/pendiente | `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` |
| **Cómo documentar lo que hago** | `contexto/AGENTS.md` (+ plantillas en `contexto/_templates/`) |
