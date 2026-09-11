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

Portal interno de gestión para **Bimbo Honduras**: catálogo de productos, pesajes, usuarios/empleados. App de escritorio **.NET 10 · WPF** con backend **Supabase**. Ejecutable único: **`CapaUI`**.

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
8. **Traducción conceptual a la tecnología activa (WPF / .NET 10 / XAML):** Si el usuario describe un requisito de diseño, comportamiento o dimensionamiento usando conceptos coloquiales, genéricos o de otros entornos (ej. Windows Forms como `DisplayedCells`, HTML/CSS como `flex`/`div`, o Android), **nunca** trasladar el término de forma literal como atributo XAML o identificador en C#. Siempre interpretarlo conceptualmente y traducirlo al equivalente nativo, idiomático y validado de WPF (ej. en `DataGrid`, usar `Width="Auto"` con `MinWidth`, nunca inventar literales como `SizeToDisplayedCells` que rompen el parser de XAML). Si un enum o propiedad no existe en WPF, verificar la API oficial antes de generar código.
9. **Inyección de Dependencias pura (Sin Service Locator en UI):** Todas las ventanas (`Window`), páginas y modales deben recibir sus dependencias por constructor. Queda prohibido usar parámetros opcionales con fallback a `App.Services.GetRequiredService<T>()` en clases que se resuelven por el contenedor.
10. **Caché centralizada e higiene de dependencias:** Antes de inyectar dependencias para purgar cachés en el cierre de sesión (`LimpiarRecursosAsync`), verificar si la entidad ya usa `ICacheService` (ADR-026). Si la purga global (`_cache.LimpiarTodoAsync()`) ya cubre las etiquetas, no inyectar dependencias redundantes ni introducir código muerto.
11. **Antes de tocar cualquier `.xaml`** (crear o editar un `UserControl`, `Window`, `ResourceDictionary` o su code-behind): leer y aplicar **[`contexto/20 - Patrones/Convenciones de UI (WPF) — leer antes de tocar XAML.md`](contexto/20%20-%20Patrones/Convenciones%20de%20UI%20(WPF)%20%E2%80%94%20leer%20antes%20de%20tocar%20XAML.md)** — ctor sin parámetros, sin DI en el `.ctor`, converters por `{x:Static conv:X.Instancia}`, estilos compartidos en `Styles.xaml`, `Empresa*` solo en `App.xaml`/`DesignTimeResources.xaml`, **prohibido `{DynamicResource}` en `TargetNullValue`/`FallbackValue`**, **usar `FindName` defensivo en UserControls para evitar desincronizaciones de `.g.i.cs`**, y **animar Freezables con `BeginAnimation` directo (no Storyboards huérfanos)**. Con Claude, la skill `ui-conventions` lo resume. Verificar con el arné de instanciación (§8 de ese nodo) + prueba visual manual.
12. **Zero-Shader Layout, ClearType y Freeze:** Nunca anidar `DropShadowEffect` (Pixel Shaders) dentro de contenedores con `ClipToBounds` o `DataGrid` (destruye el batching y satura la GPU). Usar el patrón de Borde Hermano desacoplado; en `DataGrid` declarar `RowBackground="White"` y `RenderOptions.ClearTypeHint="Enabled"`; y aplicar `po:Freeze="True"` a geometrías vectoriales (`presentation/options`).
13. **RadioButtons con Enums y CTS Concurrente en .NET 10:** En RadioButtons ligados a enums con ViewModel, omitir `GroupName` (elimina escaneo $O(N)$ del árbol visual) y usar `{x:Static ...}` en `ConverterParameter`. Para peticiones de red cancelables, usar reemplazo atómico lock-free `Interlocked.Exchange(ref _cts, cts)` con `_ = oldCts.CancelAsync()`.
14. **Dirty Tracking Tipado con `ChangeTracker<T>` y Records:** En todos los modales de edición (`*Modal.xaml.cs`), queda prohibido comparar campos a mano con cadenas `OR` (`!string.Equals(...)`). Usar siempre `ChangeTracker<TSnapshot>` (`CapaUI.Core.Validacion`) con un `record` posicional privado que contenga los campos editables. Esto garantiza igualdad por valor nativa y validación exhaustiva en tiempo de compilación.
15. **Comunicación Bilateral entre Agentes (Antigravity ↔ Claude):** Para sincronizar tareas, auditorías o deudas técnicas con Claude Code, usar siempre ambos canales en tándem: (1) **Engram** con `project: "bimboproyecto"` y formato What/Why/Where/Learned, y (2) **Bóveda (`contexto/`)** documentando en `contexto/60` (auditorías), `contexto/40` (deuda técnica `P-NNN`) o `contexto/70` (bitácora de sesión), usando bloques `> [!note] Claude, si leés esto:` con archivo y línea exactos para avisos directos.
16. **Hábitos de Proceso, Controles Compartidos y Cierre:**
    - **Controles Compartidos (`CapaUI/Core/Controls/`):** Prohibido modificarlos probando solo la pantalla activa. Grepear todos los usos en XAML/C# y garantizar retrocompatibilidad. Comportamientos no estándar deben ser opt-in (default `false`).
    - **Bitácora (`contexto/70`):** Bloques de código multilínea siempre con triple comilla invertida (```` ``` ````) y lenguaje tipado. Verificar que no haya palabras truncadas o mutiladas por reemplazos.
    - **Arquitectura Actual (`contexto/40`):** Al cerrar sesión con trabajo real, agregar siempre el callout `> [!success] Actualizado AAAA-MM-DD — Título` al inicio de `Arquitectura Actual.md` (orden más nuevo primero) enlazando a la nota de sesión en `70/`.

Detalle completo de convenciones de código: [`contexto/CLAUDE.md`](contexto/CLAUDE.md) y [`contexto/50 - Referencia/Convenciones C#.md`](contexto/50%20-%20Referencia/Convenciones%20C%23.md). Reglas de proceso en [`.agents/rules/process_and_quality_habits.md`](../.agents/rules/process_and_quality_habits.md).

---

## Build y verificación

```bash
dotnet build BimboProyecto.sln
```

Debe terminar en **0 errores y 0 advertencias** (las advertencias históricas de nullable en `CapaDatos` fueron resueltas el 2026-09-02). No hay tests de UI automatizados: la verificación funcional es **build limpio + prueba visual manual** del flujo tocado. Para cambios de XAML sumá el **arné de instanciación** (`new Application()` vacío + ctor sin parámetros + `Measure`/`Arrange`) — ver §8 de [`contexto/20 - Patrones/Convenciones de UI (WPF) — leer antes de tocar XAML.md`](contexto/20%20-%20Patrones/Convenciones%20de%20UI%20(WPF)%20%E2%80%94%20leer%20antes%20de%20tocar%20XAML.md).

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
3. **Cierre obligatorio de Deuda Técnica:**
   - Si tu trabajo resuelve o mitiga un ítem `P-NNN` de `Deuda Técnica - Pendientes.md`, es **obligatorio** actualizarlo inmediatamente en ese mismo archivo a `[x] Resuelto`, tachar el título y registrar:
     * La fecha de resolución y enlace a la nota de sesión en `contexto/70/`.
     * La consulta SQL de verificación en base viva (`pg_policy`, `pg_trigger`, `information_schema.columns`) o la prueba unitaria xUnit que certifica el estado.
   - **Nunca** des por finalizada una tarea dejando ítems resueltos marcados como `[ ] Pendiente`.
4. **Verificación rigurosa en Base de Datos Viva:**
   - Para migraciones en Supabase, no alcanza con compilar el código o generar el `.sql`: es mandatorio verificar su aplicación con `list_migrations`, validar el catálogo físico de Postgres, revisar advisors de seguridad (`get_advisors`) y auditar el cableado C# end-to-end (modal → view → viewmodel → repo → RPC).

➡ **[`contexto/AGENTS.md`](contexto/AGENTS.md)** — protocolo de la bóveda.

Resumen rápido: arreglaste un bug o hiciste una sesión de trabajo → nota en `contexto/70 - Bitácora de Cambios/AAAA-MM/`. Decisión de fondo → ADR en `45 - Decisiones/`. Encontraste algo roto sin arreglar → ítem `P-NNN` en `Deuda Técnica - Pendientes.md`. Usa las plantillas en `contexto/_templates/`.
