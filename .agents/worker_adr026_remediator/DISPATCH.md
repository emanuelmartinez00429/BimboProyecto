## 2026-09-03T05:20:33Z

You are the remediation Worker (teamwork_preview_worker).
Your working directory is: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_adr026_remediator
The project workspace is: d:\Proyectos\Proyecto de BIMBO\BimboProyecto
Path to ORIGINAL_REQUEST.md: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md (MUST read section ## 2026-09-03T04:53:04Z).
Path to DEAD_ENDS.md: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\orchestrator_3\DEAD_ENDS.md
Path to Challenger 1 handoff report: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\challenger_1\handoff.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_preview_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

STRICT WRITE CONSTRAINTS:
You are ONLY permitted to write or modify the following 3 files (touching ANY other file is strictly forbidden):
1. `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md`
2. `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`
3. `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`
ABSOLUTELY FORBIDDEN: Modifying any .cs, .xaml, .csproj, .sln, or sql files. Touching ADR-015 frontmatter is strictly forbidden (`estado: aceptado` remains untouched).

MISSION:
Incorporate the 4 critical remediations identified by Challenger 1 into `ADR-026`:
1. **Re-subscription on Session Lifecycle (Neutralizing the `_suscriptores.Clear()` trap)**:
   In §6 (add as explicit subsection or refine Trampa 1 & Trampa 13) and §8: Document that `RealtimeService.DesconectarAsync()` executes `_suscriptores.Clear()`. Therefore, if `InvalidadorCacheRealtime` is a Singleton, it loses its handlers on logout. Mandate that `InvalidadorCacheRealtime` expose an explicit `Suscribir()` method invoked in `MainWindow.OnLoaded` on every user session, or that `RealtimeService` preserve application-level singleton subscribers.
2. **Composite Tag Registration for Exact String Match**:
   In §5.1 and §6 Trampa 8: Document that `ZiggyCreatures.FusionCache 2.0.2` performs exact string matching on tags without wildcard or prefix expansion. To enable both table-specific invalidation (`RemoveByTag("catalogos:paises")`) AND global catalog purges (`RemoveByTag("catalogos")`), all catalog entries MUST be registered with composite tags:
   `tags: new[] { TagsCache.CatalogosRaiz, $"catalogos:{nombreTabla}" }`.
3. **Correct Reconnect Trigger State (`SocketState.Open` vs `Reconnect`)**:
   In §6 Trampa 8 and §8 Fase 4: Correct the reconnect purge trigger: `SocketState.Reconnect` indicates that reconnection attempts have begun while STILL offline; purging then causes premature cache misses and network errors. The purge must trigger exclusively upon transition to `SocketState.Open` (restored connection) or `IConexionMonitor.Reconectado`.
4. **Defensive Catch for `OperationCanceledException`**:
   In §6 Trampa 3: Document that when a modal's `_ctsVida` cancels, `GetOrSetAsync` throws `OperationCanceledException`. Because `SelectorCatalogoModal` calls `CargarInicialAsync` from `async void OnLoaded`, an unhandled exception crashes the WPF process (*Crash to Desktop*). Mandate that `CachedCatalogoRepository` intercept `OperationCanceledException` and return `Result<PagedResult<FiltroItem>>.Fail("Operación cancelada")` defensively.

Ensure ADR-026 preserves all other sections, diagrams, matrices, and vault conventions.
Write your handoff report in `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\worker_adr026_remediator\handoff.md` and notify orchestrator via send_message when done.
