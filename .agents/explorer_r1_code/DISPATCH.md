# Explorer 1 Dispatch

Assigned Role: teamwork_preview_explorer
Focus: R1 - Auditoría adversarial y contraste con el código existente
Working Directory: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\explorer_r1_code
Original Request: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md

## 2026-09-03T04:59:15Z
Execute R1: Auditoría adversarial y contraste profundo con el código vivo de BimboProyecto.

TASKS:
1. Examine existing catalog cache and repository implementations:
   - `CatalogoCache.cs`
   - `CatalogoRepository.cs`
   - `SelectorCatalogoModal.xaml.cs`
   - `RolPermisoRepository.cs`
   - `EmpresaRepository.cs`
   Examine exact lines, data structures, locks, dictionaries, methods, and how catalogs are fetched and cached today under ADR-015.
2. Examine concurrency & execution order:
   - `RealtimeService.OnCambioRecibido` (how events are received, thread dispatching to UI thread vs background threads)
   - How `InvalidadorCacheRealtime.OnCambio` will interact with `RemoveByTag` and decoupled log processing (e.g. via `Channel<T>`).
3. Examine application lifecycle:
   - `MainWindow.xaml.cs` (`OnLoaded` and `LimpiarRecursosAsync` or logout/session end)
   - `App.xaml.cs` and DI container (`App.Services` root service provider, static lifetime)
   - Investigate the concrete cross-user data leakage mechanism when multiple operators/users use the same physical plant computer: why `CatalogoCache` and `RolPermisoRepository._catalogoCache` leak data across user sessions if `InvalidarTodo()` is not called or if root provider is static.
4. Document all findings, exact file paths, line numbers, and technical evidence in `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\explorer_r1_code\analysis.md` and write your completion handoff in `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\explorer_r1_code\handoff.md`.
