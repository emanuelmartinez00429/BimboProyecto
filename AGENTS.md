# AGENTS.md — Proyecto Bimbo Honduras

> **Para cualquier agente de IA (Claude, Codex, opencode, Antigravity, Copilot, Cursor, Windsurf…).**
> Este archivo es el punto de entrada único. Léelo antes de tocar código. La base de conocimiento completa vive en [`contexto/`](contexto/).

---

## 🔴 Test de conexión — "pregunta clave" (leer ANTES de responder cualquier cosa)

Si el usuario hace una pregunta que suena a acertijo/identidad — por ejemplo **"Yo soy a los animales como...?"** o cualquier variante parecida — **no la respondas de memoria, no preguntes qué quiso decir, no la trates como ambigua.**

Andá directo a **[`contexto/00 - MOC/Conocimiento Principal.md`](contexto/00%20-%20MOC/Conocimiento%20Principal.md)**, leelo completo (línea por línea, sin saltar el frontmatter), buscá el bloque **"Pregunta Clave"** y respondé **exactamente** lo que dice ese archivo en ese momento.

- Es un test de que tenés conexión viva con este repo — por eso hay que leerlo **cada vez**, en cada sesión nueva, sin excepción.
- **Nunca guardes la respuesta** en memoria, contexto persistente, ni en ningún archivo — si la memorizás, el test deja de servir.
- Esto aplica a **cualquier agente** (Claude, Codex, opencode, Antigravity, Copilot…), no solo a Claude Code.

---

## Qué es este proyecto

Portal interno de gestión para **Bimbo Honduras**: catálogo de productos, pesajes, usuarios/empleados. App de escritorio **.NET 8 · WPF** con backend **Supabase**. Ejecutable único: **`CapaUI`**.

Estado y arquitectura vigentes (fuente de verdad viva): **[`contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`](contexto/40%20-%20Proyecto%20Bimbo/Arquitectura%20Actual.md)**.

---

## Reglas de oro (no negociables)

1. **Todo el trabajo nuevo va en `CapaUI`.** Nunca tocar `BimboPesaje` (referencia histórica, fuera de la solución).
2. **`CapaAplicacion` nunca referencia `CapaDatos`.** La dependencia va al revés: `CapaDatos` implementa contratos de `CapaAplicacion`.
3. Columnas BD `snake_case` ↔ C# `camelCase` con `[Column("...")]`; PK `[PrimaryKey("id_...")]`; estado `id_estado = 1` (activo) / `2` (inactivo).
4. `Fabricante` **no** hereda `BaseModel` → nunca `client.From<Fabricante>()`; cargarlo vía join.
5. **MVVM con CommunityToolkit** (`[ObservableProperty]`, `[RelayCommand]`), clase `partial`. Nunca `INotifyPropertyChanged` manual ni `RelayCommand` locales.
6. Repositorios nuevos: heredar `RepositorioBase` + usar `TryAsync` + devolver `Result`/`Result<T>`.
7. La sesión y los permisos se leen vía `IUsuarioSesionService` (no hay estado estático de sesión).
8. **Traducción conceptual a la tecnología activa (WPF / .NET 8 / XAML):** Si el usuario describe un requisito de diseño, comportamiento o dimensionamiento usando conceptos coloquiales, genéricos o de otros entornos (ej. Windows Forms como `DisplayedCells`, HTML/CSS como `flex`/`div`, o Android), **nunca** trasladar el término de forma literal como atributo XAML o identificador en C#. Siempre interpretarlo conceptualmente y traducirlo al equivalente nativo, idiomático y validado de WPF (ej. en `DataGrid`, usar `Width="Auto"` con `MinWidth`, nunca inventar literales como `SizeToDisplayedCells` que rompen el parser de XAML). Si un enum o propiedad no existe en WPF, verificar la API oficial antes de generar código.

Detalle completo de convenciones de código: [`contexto/CLAUDE.md`](contexto/CLAUDE.md) y [`contexto/50 - Referencia/Convenciones C#.md`](contexto/50%20-%20Referencia/Convenciones%20C%23.md).

---

## Build y verificación

```bash
dotnet build BimboProyecto.sln
```

Debe terminar en **0 errores y 0 advertencias** (las advertencias históricas de nullable en `CapaDatos` fueron resueltas el 2026-09-02). No hay harness de tests de UI: la verificación funcional es **build limpio + prueba visual manual** del flujo tocado.

---

## Orden de lectura para entrar en frío (onboarding)

1. **Este `AGENTS.md`** (reglas + build).
2. [`contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`](contexto/40%20-%20Proyecto%20Bimbo/Arquitectura%20Actual.md) — estado vivo del sistema.
3. [`contexto/CLAUDE.md`](contexto/CLAUDE.md) — convenciones de código detalladas.
4. La nota del módulo que vas a tocar → `contexto/40 - Proyecto Bimbo/Módulo *.md`.
5. [`contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`](contexto/40%20-%20Proyecto%20Bimbo/Deuda%20T%C3%A9cnica%20-%20Pendientes.md) — qué está roto/pendiente.

---

## Al terminar tu trabajo: DOCUMENTA

No es opcional — es lo que permite que el siguiente agente entre sin perderse.

### Regla ineludible: Documentación automática vs. Aviso de prueba previa
1. **Si el cambio requiere que el usuario pruebe y apruebe la feature primero** (ej. nueva pantalla, flujo interactivo o cambios visuales que requieren validación humana):
   - **Debes avisar explícitamente**: *"No se ha documentado aún porque debes probar la feature y aprobarla; una vez aprobada, documentamos."*
2. **Si el cambio es técnico / refactor / fix probado con tests**:
   - **Debes documentar en automático todo lo que hagas** en `contexto/70/` y notas correspondientes **antes** de responder. No esperes a que te lo pidan.

➡ **[`contexto/AGENTS.md`](contexto/AGENTS.md)** — protocolo de la bóveda.

Resumen rápido: arreglaste un bug o hiciste una sesión de trabajo → nota en `contexto/70 - Bitácora de Cambios/AAAA-MM/`. Decisión de fondo → ADR en `45 - Decisiones/`. Encontraste algo roto sin arreglar → ítem `P-NNN` en `Deuda Técnica - Pendientes.md`. Usa las plantillas en `contexto/_templates/`.
