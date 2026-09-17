# Round 2 Adversarial Review Handoff Report

## Executive Summary
During the adversarial review of the Dashboard live data integration, multiple latent concurrency, state consistency, and error handling defects were discovered in `DashboardVM.cs` and `DashboardRepository.cs`. These defects allowed out-of-order asynchronous responses to corrupt the active view state when switching periods, silently swallowed data loading errors, leaked active background tasks upon ViewModel disposal, and risked cross-thread dispatch deadlocks.

All identified defects have been rectified, and 6 new unit tests were added to `BimboProyecto.Tests/Dashboard/DashboardUnitTests.cs`. All 520 tests in the test suite pass with 0 warnings and 0 errors across all projects.

---

## 1. Defects Discovered & Root Cause Analysis

### Defect 1: Race Condition & State Inconsistency in Period Switching (`DashboardVM`)
- **Input**: User clicks "Semana", and then immediately clicks "Mes" (or network latency makes the "Semana" response arrive after the "Mes" response).
- **Expected**: The ViewModel displays the data corresponding to the selected period (`Periodo = "Mes"`), discarding the stale result from "Semana".
- **Actual**: `CargarPesajesYMermaAsync(PeriodoDashboard.Semana)` awaited `Task.WhenAll`, then unconditionally set `PesajeCount`, `TotalNeto`, `PctMerma`, `TopMerma`, etc., overwriting the "Mes" data with "Semana" data while `Periodo` displayed "Mes". Furthermore, the first request's `finally` block set `IsLoading = false` prematurely while the second request was still in flight.
- **Root Cause**: Missing cancellation token management and period consistency check (`if (periodo != PeriodoActual || ct.IsCancellationRequested || Disposed) return;`).
- **Fix**: Implemented linked `CancellationTokenSource` (`_ctsPeriodo`) per period request, period matching guards before updating observable properties, and guarded the `finally` block to only reset `IsLoading = false` if the active request was not cancelled.

### Defect 2: Missing Resource Cleanup / Cancellation on ViewModel Disposal (`DashboardVM`)
- **Input**: User opens the Dashboard, and immediately navigates to another page (e.g. Pesajes or Empleados), disposing `DashboardVM`.
- **Expected**: In-flight network and database operations are cancelled via `CancellationToken`, and internal CTS instances are disposed in `OnDispose()`.
- **Actual**: `DashboardVM` did not maintain a lifetime `CancellationTokenSource`, did not override `OnDispose()`, and did not pass `CancellationToken` to `_dashboardRepo` methods. Background tasks continued to run and consume network/database connections after disposal.
- **Root Cause**: `DashboardVM` lacked lifetime `CancellationTokenSource` and `OnDispose()` implementation.
- **Fix**: Added `_ctsLifetime`, linked tokens, and implemented `OnDispose()` to cancel and dispose all active CTS instances.

### Defect 3: Silent Error Swallowing in `CargarUltimosPesajesAsync` & Incomplete Error Surfacing (`DashboardVM`)
- **Input**: Database or network error occurred while fetching `ObtenerUltimosPesajesAsync` or `ObtenerTopMermaAsync` during `InicializarAsync`.
- **Expected**: `HasError` is set to `true` and `ErrorMessage` describes the failure, notifying the user/operator that data loading failed.
- **Actual**: In `CargarUltimosPesajesAsync`:
  ```csharp
  var resultado = await _dashboardRepo.ObtenerUltimosPesajesAsync(5);
  if (!resultado.Success || resultado.Value is null)
      return;
  ```
  It silently returned, ignoring `resultado.Error`. The error was completely swallowed. `HasError` remained `false`. Similarly, in `CargarPesajesYMermaAsync`: if `mermaTask` failed, `mermaTask.Result.Success` was false, but nothing checked `mermaTask.Result.Success` or set `HasError` / `ErrorMessage`.
- **Root Cause**: Incomplete error handling branches in `CargarUltimosPesajesAsync` and `CargarPesajesYMermaAsync`.
- **Fix**: Explicitly check `!resultado.Success` and `!mermaTask.Result.Success`, updating `HasError = true` and `ErrorMessage = ...`.

### Defect 4: Potential Dispatcher Deadlock and Shutdown Exception (`DashboardVM`)
- **Input**: Background thread invoked `EjecutarEnUI` while UI thread was shutting down or executing modal operations.
- **Expected**: Clean dispatch without deadlocks or crashes.
- **Actual**: Used synchronous `d.Invoke(action)` unconditionally on non-UI threads without checking `d.HasShutdownStarted`.
- **Root Cause**: Missing check for `d.HasShutdownStarted` and UI thread access guards.
- **Fix**: Added `!d.HasShutdownStarted` check and safe fallback execution.

### Defect 5: Unfrozen Brush Fallback in `MermaItemVM`
- **Input**: Normal merma tone rendered when `Application.Current` was null or missing `EmpresaPrimaryGradientBrush`.
- **Expected**: A frozen, thread-safe linear gradient brush.
- **Actual**: Fell back to a solid brush `FallbackBrush` instead of the primary gradient, and lacked a static frozen primary gradient for cross-thread safety.
- **Root Cause**: Missing frozen `NormalBrush` gradient.
- **Fix**: Added `NormalBrush = CreateFrozenGradient(Color.FromRgb(0x1E, 0x3A, 0x8A), Color.FromRgb(0x3B, 0x82, 0xF6));` and used it as the fallback.

---

## 2. Verification Record
- **Full Solution Build (`dotnet build BimboProyecto.sln`)**:
  - `CapaDominio`, `CapaAplicacion`, `CapaDatos`, `BimboProyecto.Tests`, `CapaUI`.
  - 0 Errors, 0 Warnings.
- **Full Test Suite (`dotnet test BimboProyecto.sln`)**:
  - 520 passed, 0 failed, 0 skipped across the entire solution.
- **Direct Database RPC Execution (`consultar_kpis_pesajes`)**:
  - Verified via Supabase MCP `execute_sql` with zero-record intervals (`2026-09-17`) and active historical intervals (`2026-09-10`).
- **Strict Scope Verification**:
  - Verified `git status`: only planned files modified and added.
