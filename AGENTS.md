# AGENTS.md — Proyecto Bimbo Honduras

> Punto de entrada único para cualquier agente de IA (Antigravity, Claude Code, Codex, Gemini, Copilot, Cursor, opencode) y colaboradores del equipo.
> Este archivo es el contrato. El conocimiento vive en [`contexto/`](contexto/) y se consulta **por índice, no de corrido**.

---

## 🔴 Test de conexión — "pregunta clave"

Si el usuario hace una pregunta tipo **"Yo soy a los animales como...?"** (o variante de acertijo/identidad): **no respondas de memoria, no preguntes qué quiso decir, no la trates como ambigua.**
Ve directo a **[`contexto/00 - MOC/Conocimiento Principal.md`](contexto/00%20-%20MOC/Conocimiento%20Principal.md)**, léelo completo, busca el bloque **"Pregunta Clave"** y responde **exactamente** lo que dice ese archivo en ese momento. Nunca memorices la respuesta: hay que leerla en vivo cada vez.

---

## Qué es este proyecto

Portal interno de gestión para **Bimbo Honduras**: catálogo de productos, pesajes, usuarios y empleados. App de escritorio **.NET 10 · WPF** con backend **Supabase** (Postgres, Auth, Storage). Ejecutable único: **`CapaUI`**.
Equipo: `fernando` (integrador), `emanuel` (dueño del repo, integrador), `carlos`. Ver `contexto/.control/equipo.json`.

---

## Reglas de oro (no negociables)

1. **Trabajo nuevo solo en `CapaUI`.** Nunca tocar `BimboPesaje` (histórico, fuera de solución).
2. **`CapaAplicacion4` nunca referencia `CapaDatos`.** `CapaDatos` implementa contratos de aplicación.
3. **Columnas BD `snake_case` ↔ C# `camelCase`** con `[Column("...")]`; PK `[PrimaryKey("id_...")]`; estado `id_estado = 1` (activo) / `2` (inactivo).
4. **`Fabricante` no hereda `BaseModel`:** nunca `client.From<Fabricante>()`; cargar siempre vía join.
5. **MVVM con CommunityToolkit:** `[ObservableProperty]`, `[RelayCommand]`, clase `partial`. Prohibido `INotifyPropertyChanged` manual o `RelayCommand` locales.
6. **Repositorios nuevos:** heredar `RepositorioBase` + usar `TryAsync` + devolver `Result`/`Result<T>`.
7. **Sesión y permisos:** leer vía `IUsuarioSesionService` (sin estado estático).
8. **Traducción conceptual idiomática a WPF:** nunca usar conceptos literales de WinForms/HTML (ej. `SizeToDisplayedCells`). Usar primitivas nativas de WPF.
9. **Inyección de Dependencias pura (Sin Service Locator en UI):** ventanas, páginas y modales reciben dependencias por constructor. Prohibido `App.Services.GetRequiredService<T>()` en clases resueltas por el contenedor.
10. **Caché centralizada (`ICacheService`):** purga global cubre catálogos; no inyectar dependencias redundantes.
11. **Antes de tocar `.xaml`:** leer [`contexto/20 - Patrones/Convenciones de UI (WPF) — leer antes de tocar XAML.md`](contexto/20%20-%20Patrones/Convenciones%20de%20UI%20(WPF)%20%E2%80%94%20leer%20antes%20de%20tocar%20XAML.md). Ctor sin parámetros en controles, converters singleton por `{x:Static}`, prohibido `{DynamicResource}` en `TargetNullValue`/`FallbackValue`. Verificar con arnés de instanciación.
12. **Zero-Shader Layout:** prohibido `DropShadowEffect` dentro de contenedores con `ClipToBounds` o `DataGrid` (usar Borde Hermano). `RowBackground="White"`, `ClearTypeHint="Enabled"`, y `po:Freeze="True"` en geometrías.
13. **RadioButtons con Enums y CTS Concurrente:** omitir `GroupName` en RadioButtons enlazados a enums. Peticiones cancelables con `Interlocked.Exchange(ref _cts, cts)`.
14. **Dirty Tracking Tipado:** en modales de edición usar siempre `ChangeTracker<TSnapshot>` con `record` posicional privado de campos editables. Prohibido comparar campos a mano con cadenas `OR`.
15. **Política para agentes de IA:** Supabase de producción es de solo lectura (SELECT). Migraciones solo como archivo `.sql` en `supabase/migrations/` aplicadas por un integrador. Un agente no hace merge, force push ni modifica configuración de GitHub. Cero secretos o datos personales en notas/commits.
16. **Flujo de git:** ramas `tipo/descripcion-corta` integradas a `main` por PR. Revisión por persona distinta de quien lanzó al agente. `.agents/teamwork/` nunca se versiona.

---

## Verificación

```bash
dotnet build BimboProyecto.sln -c Release   # 0 errores y 0 advertencias
node scripts/contexto/build-index.js        # regenera INDEX.md e INDEX-bitacora.md
node scripts/contexto/verificar.js          # 0 errores en contexto
```

---

## Cómo entrar en frío

1. **Este archivo (`AGENTS.md`).**
2. **[`contexto/INDEX.md`](contexto/INDEX.md):** una línea por nota con tipo, estado, fecha y resumen.
3. **Elige de 3 a 6 notas** relevantes para la tarea y abre solo esas.
4. Si el índice no alcanza: `grep -ril "término" contexto/` y vuelve al paso 3.

| Vas a… | Empieza por |
| :-- | :-- |
| Diseñar o modificar arquitectura | `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md` |
| Ver convenciones C# / patrones MVVM | `contexto/20 - Patrones/Convenciones de código.md` |
| Crear o editar vistas/controles XAML | `contexto/20 - Patrones/Convenciones de UI (WPF) — leer antes de tocar XAML.md` |
| Entender decisiones arquitectónicas | `contexto/45 - Decisiones/` (ADR-NNN) |
| Consultar o resolver deuda técnica | `contexto/40 - Proyecto Bimbo/Deuda - Abierta.md` |
| Conocer detalles de un módulo | `contexto/40 - Proyecto Bimbo/Módulo *.md` |
| Consultar APIs y especificaciones | `contexto/50 - Referencia/` |
| Revisar hallazgos de calidad QA | `contexto/60 - Revisiones QA/` |

---

## Al terminar: documenta

| Hiciste… | Va en… |
| :-- | :-- |
| Una sesión de trabajo o un fix | Nota de sesión en `contexto/70 - Bitácora de Cambios/AAAA-MM/` |
| Una decisión con alternativas | ADR en `contexto/45 - Decisiones/` |
| Un patrón reutilizable | Nota en `contexto/20 - Patrones/` |
| Algo roto que no arreglaste | Ítem `P-NNN` en `contexto/40 - Proyecto Bimbo/Deuda - Abierta.md` |
| Resolviste o mitigaste un `P-NNN` | Mover a `Deuda - Resuelta.md` con fecha y evidencia de verificación |
| Un hecho externo aprendido | Nota en `contexto/50 - Referencia/` |
| Un cambio puramente cosmético | Nada: solo commit |

**Reglas de documentación:**
- **Feature interactiva/visual:** avisa: *"No se ha documentado aún porque debes probar la feature y aprobarla; una vez aprobada, documentamos."*
- **Cambio técnico/refactor/fix:** documenta en automático antes de cerrar la sesión.
- **Deuda técnica:** obligatorio marcar `[x] Resuelto`, documentar fecha, enlace a sesión y evidencia en BD viva o test.
- Ver detalles en [`.agents/rules/process_and_quality_habits.md`](.agents/rules/process_and_quality_habits.md).

---

## Grafo del repo (graphify)

Para preguntas sobre el código: `graphify query "<pregunta>"` antes de abrir archivos; lo que diga el grafo se comprueba en el archivo antes de afirmarlo. `graphify-out/` es un índice local nunca versionado.

---

## Memoria entre sesiones (Engram)

Proyecto Engram: **`bimboproyecto`**.
Al retomar: `mem_context`, `mem_search`. En cada hito (plan, rama, PR, decisión, hallazgo): `mem_save` con `project: "bimboproyecto"` y `topic_key: bimboproyecto/<tema>` (o `bimboproyecto/rama/<nombre>`), formato What/Why/Where/Learned.

---

## Mapa de archivos de control

| Archivo | Qué es |
| :-- | :-- |
| `AGENTS.md` (este) | Contrato único para agentes y colaboradores. Tope 8 KB. |
| `CLAUDE.md`, `GEMINI.md`, copilot, cursor, windsurf | Apuntadores exclusivos a este archivo. |
| `contexto/INDEX.md`, `INDEX-bitacora.md` | Índices generados automáticamente. No editar a mano. |
| `contexto/AGENTS.md` | Protocolo de escritura y taxonomía de la bóveda. |
| `contexto/.control/equipo.json` | Registro de personas del equipo y roles integradores. |
| `.github/workflows/` | CI/CD (`ci.yml`, `contexto.yml`, `publish-velopack-release.yml`). |
| `.gitleaks.toml`, `CODEOWNERS`, `dependabot.yml` | Reglas de DevSecOps y gobernanza del repo. |
