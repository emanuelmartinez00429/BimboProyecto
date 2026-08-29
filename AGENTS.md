# AGENTS.md — Proyecto Bimbo Honduras

> **Punto de entrada único para cualquier agente de IA** (Claude, Codex, opencode, Antigravity, Copilot, Cursor, Windsurf…).
> Este archivo es el contrato. La base de conocimiento vive en [`contexto/`](contexto/) y se consulta **por índice, no de corrido**.

## Qué es este proyecto

Portal interno de gestión para **Bimbo Honduras**: catálogo de productos, pesajes, usuarios y empleados.
Escritorio **.NET 8 · WPF** con backend **Supabase**. Ejecutable único: **`CapaUI`**.

## Reglas de oro (no negociables)

1. **Todo el trabajo nuevo va en `CapaUI`.** Nunca tocar `BimboPesaje` (referencia histórica, fuera de la solución).
2. **`CapaAplicacion` nunca referencia `CapaDatos`.** La dependencia va al revés: `CapaDatos` implementa contratos de `CapaAplicacion`.
3. Columnas BD `snake_case` ↔ C# `camelCase` con `[Column("...")]`; PK `[PrimaryKey("id_...")]`; estado `id_estado = 1` (activo) / `2` (inactivo).
4. `Fabricante` **no** hereda `BaseModel` → nunca `client.From<Fabricante>()`; cargarlo vía join.
5. **MVVM con CommunityToolkit** (`[ObservableProperty]`, `[RelayCommand]`), clase `partial`. Nunca `INotifyPropertyChanged` manual ni `RelayCommand` locales.
6. Repositorios nuevos: heredar `RepositorioBase`, usar `TryAsync`, devolver `Result`/`Result<T>`.
7. La sesión y los permisos se leen vía `IUsuarioSesionService`. No hay estado estático de sesión.

Detalle completo de convenciones: [`contexto/CLAUDE.md`](contexto/CLAUDE.md) y [`contexto/50 - Referencia/Convenciones C#.md`](contexto/50%20-%20Referencia/Convenciones%20C%23.md).

## Build y verificación

```bash
dotnet build BimboProyecto.sln
```

Debe terminar en **0 errores** (hay warnings preexistentes de nullable en `CapaDatos`, no bloquean).
No hay harness de tests de UI: la verificación funcional es **build limpio + prueba visual manual** del flujo tocado.

## Cómo entrar en frío — recuperá, no leas todo

La bóveda tiene ~185 notas y 1,2 MB. **No existe un orden de lectura obligatorio.** Abrí solo lo que la tarea necesita:

1. **Este archivo** (ya lo estás leyendo). Es el contrato.
2. **[`contexto/INDEX.md`](contexto/INDEX.md)** — índice generado: una línea por nota con tipo, fecha y resumen. Leelo entero, es barato (~15 KB). El hook de arranque ya te lo inyecta.
3. **Elegí 3–6 notas** del índice y abrí **solo esas**.
4. Si el índice no alcanza: `grep -ri "<término>" contexto/ --include="*.md" -l` y volvé al paso 3.

Hay dos índices más, que **no** se leen al arrancar — se abren cuando hacen falta:

- **[`contexto/INDEX-codigo.md`](contexto/INDEX-codigo.md)** — mapa `código → notas` y símbolos compartidos. Abrilo **antes de modificar un archivo**: te dice qué notas lo describen y cuáles hay que actualizar después.
- **[`contexto/INDEX-bitacora.md`](contexto/INDEX-bitacora.md)** — historial de las 95 sesiones por mes. Abrilo para «¿en qué íbamos?» o «¿cuándo se hizo X?».

Atajos por tipo de tarea:

| Vas a… | Empezá por |
|---|---|
| Tocar un módulo | `contexto/40 - Proyecto Bimbo/Módulo *.md` del módulo |
| Cambiar arquitectura o capas | `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md` |
| Arreglar algo roto o pendiente | `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` |
| Entender por qué algo es así | `contexto/45 - Decisiones/ADR-*.md` |
| Replicar un módulo existente | `contexto/40 - Proyecto Bimbo/Checklist - Replicar Módulo con Realtime.md` |
| Pelear con la SDK, WPF o Postgrest | `contexto/50 - Referencia/` |

> **Regla de fuente viva.** El contenido actual de los archivos es la única fuente de verdad. Tu memoria de
> sesiones anteriores nunca tiene prioridad. Antes de responder sobre el estado del proyecto, releé el archivo
> —aunque creas que ya lo leíste— y si no podés confirmarlo releyendo, decilo en vez de reconstruirlo de memoria.

> **Rama actual, no `master` fijo.** El proyecto avanza en ramas `feat/faseN-…`. Antes de responder sobre
> estado de desarrollo: `git branch --show-current`, compará la divergencia real, y decí qué rama consultaste.

## Al terminar: documentá

No es opcional — es lo que permite que el siguiente agente entre sin perderse.

- Hiciste una sesión de trabajo o arreglaste un bug → nota en `contexto/70 - Bitácora de Cambios/AAAA-MM/`.
- Tomaste una decisión de fondo → ADR en `contexto/45 - Decisiones/`.
- Encontraste algo roto que no arreglaste → ítem `P-NNN` en `Deuda Técnica - Pendientes.md`.
- Descubriste un patrón reutilizable → nota en `contexto/20 - Patrones/`.
- Cambio puramente visual (espaciados, estilos XAML) → **no** genera nota de sesión, solo commit.

Usá las plantillas de `contexto/_templates/` y **rellená el frontmatter completo** — `scope` y `symbols` son
lo que conecta la nota con el código. Después de tocar la bóveda, regenerá el índice:

```bash
node scripts/normalizar-frontmatter.js --write   # completa lo derivable del frontmatter
node scripts/build-index.js                      # regenera los tres índices
node scripts/verificar-boveda.js                 # 0 errores antes de commitear
```

Protocolo completo (taxonomía, frontmatter, nombres, anti-duplicados, concurrencia): **[`contexto/AGENTS.md`](contexto/AGENTS.md)**.

## Mapa de archivos de control

| Archivo | Qué es |
|---|---|
| `AGENTS.md` (este) | Contrato. Fuente única; el resto son apuntadores. |
| `CLAUDE.md`, `GEMINI.md`, `.windsurfrules`, `.cursor/rules/bimbo.mdc`, `.github/copilot-instructions.md` | Apuntadores a este archivo. No duplicar reglas ahí. |
| `contexto/AGENTS.md` | Protocolo de la bóveda: cómo clasificar y guardar. |
| `contexto/INDEX.md` | Índice generado que se lee al arrancar. **No editar a mano.** |
| `contexto/INDEX-codigo.md` | Mapa código → notas y símbolos compartidos. Generado. |
| `contexto/INDEX-bitacora.md` | Historial de sesiones por mes. Generado. |
| `scripts/` | `build-index.js`, `normalizar-frontmatter.js`, `verificar-boveda.js`, `lib-boveda.js`. |
| `contexto/.control/handshake.md` | Test de conexión viva. Lo inyecta el hook de arranque. |
| `contexto/00 - MOC/Conocimiento Principal.md` | Dashboard para humanos. Los agentes entran por el índice. |
| `.claude/hooks/`, `.codex/hooks/` | Automatizaciones deterministas. Ver sección 9 de `contexto/AGENTS.md`. |

## Cómo trabajar con Fernando

- **Toda sesión de este proyecto la gestiona Fernando.** Al nombrar o titular una sesión, prefijá con `Fernando — `.
- **Entender antes de insistir.** Si reformula la misma pregunta más de una vez, la respuesta anterior no dio en el blanco: cambiá de enfoque, no repitas la explicación.
- **Sin parches de compromiso.** Rechazá soluciones «aceptables a medias» (TTLs, ventanas de tolerancia, «funciona casi siempre»). Un bug que ocurre menos seguido sigue siendo el mismo bug. Si hay un trade-off real, presentá la comparación explícita y dejalo elegir.
- **Calibrá las preguntas ambiguas.** Investigá a fondo solo cuando hay señal real (se conecta con un patrón establecido, hay insistencia, hay framing deliberado). Si no hay señal, respondé directo.
- **Automatización nueva = hook, no instrucción.** Cuando un comportamiento tenga que ser determinístico en vez de depender de que el modelo se acuerde: creá `.claude/hooks/NOMBRE.js`, registralo en `.claude/settings.json`, espejalo en `.codex/`, y documentalo en la sección 9 de `contexto/AGENTS.md`.
