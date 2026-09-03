# Progress — Challenger 1 (ADR-026 Adversarial Stress Testing)

Last visited: 2026-09-03T05:16:00Z
Status: In Progress

## Tasks
- [x] Review mission and dispatch requirements
- [x] Initialize DISPATCH.md, BRIEFING.md, and progress.md
- [x] Investigate codebase for existing implementations:
  - [x] `CatalogoCache.cs` and `SelectorCatalogoModal.xaml.cs`
  - [x] `RealtimeService.cs` (WebSocket reconnection, channel handling)
  - [x] `MainWindow.xaml.cs` (Logout and resource cleanup)
  - [x] `RolPermisoRepository.cs` and session management
  - [x] `RepositorioBase.cs` (Exception handling and cancellation filtering)
- [x] Adversarial stress test 1: Plant shift change & WebSocket disconnect / reconnect
  - Discovered Bug: `SocketState.Reconnect` fires on *attempt* (offline), not when restored (`SocketState.Open`).
  - Discovered Bug: `RemoveByTagAsync("catalogos")` fails to match `catalogos:{tabla}` because FusionCache uses exact string matching.
- [x] Adversarial stress test 2: High concurrency (10 concurrent modals, single-flight, cancellation)
  - Discovered Bug: Caller cancellation throws `OperationCanceledException` out of `RepositorioBase`, uncaught in `SelectorCatalogoModal.CargarInicialAsync()`, causing crash to desktop via `async void OnLoaded`.
- [x] Adversarial stress test 3: Cross-user data leakage and P-048 mitigation completeness
  - Discovered Fatal Bug: `RealtimeService.DesconectarAsync()` wipes `_suscriptores.Clear()`. Singleton `InvalidadorCacheRealtime` loses all subscriptions on logout and never receives WAL invalidations for subsequent users.
- [x] Adversarial stress test 4: Observation of non-published tables (P-049)
  - Confirmed: PostgREST / Realtime opens channel with status "ok", but PostgreSQL emits zero WAL events. Zero-Cache policy is mandatory.
- [x] Adversarial stress test 5: Deep analysis of all 12 traps + missing traps/edge cases
- [x] Formulate findings, empirical evidence, and verdict (REJECT with mandatory mitigations)
- [/] Write comprehensive handoff report (`handoff.md`)
- [ ] Send coordination message to parent
