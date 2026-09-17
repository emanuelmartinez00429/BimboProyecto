# Handoff: Adversarial Review of Dashboard Live Integration

## Review Summary
- **Reviewer**: teamwork_preview_reviewer
- **Date**: 2026-09-17
- **Target**: Live Data Integration for Dashboard View (R1–R5)
- **Status**: Verified & Fixed

---

## 1. Issues Identified in Prior Attempt

### Issue 1: Scope Violation (Unapproved File Modification)
- **Input**: Working tree state inspect (`git status`).
- **Expected**: ONLY approved files from plan modified.
- **Actual**: `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadosView.xaml` had modified column widths.
- **Root Cause**: Unintended working tree modification leftover from previous unrelated editing.
- **Resolution**: Reverted with `git restore CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadosView.xaml`.

### Issue 2: Unhandled Async Exception & Indeterminate State in Period Switching
- **Input**: User clicks "Semana" or "Mes" toggle button, executing `SelectPeriodoCommand`.
- **Expected**: `CambiarPeriodoAsync` tracks `IsLoading = true/false`, catches exceptions, reports error states, and respects `Disposed`.
- **Actual**: `CambiarPeriodoAsync` had no `try-catch-finally`, did not set `IsLoading`, and did not check `Disposed`. Because `RelayCommand` wraps an `async void` lambda, any network failure would throw an unhandled exception crashing the process.
- **Root Cause**: Omission of defensive async error handling in `CambiarPeriodoAsync`.
- **Resolution**: Added `if (Disposed) return;`, `IsLoading = true`, `try-catch` setting `HasError = true; ErrorMessage = ex.Message;`, and `finally { IsLoading = false; }`.

### Issue 3: Thread Affinity & Unfrozen Freezable Allocation in `MermaItemVM`
- **Input**: Merma progress bar item rendering or property access outside UI dispatcher thread.
- **Expected**: Thread-safe brushes with zero cross-thread affinity risk and minimal allocations.
- **Actual**: `BarBrush` instantiated new unfrozen `LinearGradientBrush` on every getter invocation, risking `InvalidOperationException` across threads and excessive GC pressure.
- **Root Cause**: Instantiating non-frozen Freezable brushes on property read without freezing.
- **Resolution**: Created static frozen brushes (`CriticalBrush.Freeze()`, `WarningBrush.Freeze()`, `FallbackBrush.Freeze()`).

### Issue 4: Negative Percentage Handling in Merma Bar
- **Input**: `Pct < 0` (received weight > theoretical weight).
- **Expected**: `BarPercent` clamped to `[0.0, 100.0]` for WPF ProgressBar.
- **Actual**: `BarPercent` returned negative values, violating `ProgressBar.Minimum = 0`.
- **Root Cause**: Using `Math.Min(100.0, ...)` without clamping lower bound.
- **Resolution**: Replaced with `Math.Clamp(Pct / _maxPct * 100.0, 0.0, 100.0)`.

### Issue 5: Regional Culture Dependency & Array/Object Parsing in `DashboardRepository`
- **Input**: Supabase RPC payload returning numeric values formatted as strings (e.g. `"97.970"`), or returning single `JObject` rather than `JArray`.
- **Expected**: Culture-invariant decimal/int parsing resilient to Spanish comma decimal separators and both JArray/JObject payload shapes.
- **Actual**: Directly invoking `row?["..."]?.Value<decimal>()` and assuming array shape `token.Children<JObject>()`.
- **Root Cause**: Postgrest serializes Postgres `numeric` as string literals to preserve arbitrary precision.
- **Resolution**: Implemented culture-invariant `ParseDecimal` and `ParseInt` helper methods, and pattern matched `token` for both `JObject` and `JArray`.

### Issue 6: UI ObservableCollection Cross-Thread Modification Risk
- **Input**: Background task continuation updating `TopMerma` or `Ultimos`.
- **Expected**: Atomic UI dispatch via `EjecutarEnUI`.
- **Actual**: Direct collection mutation without ensuring Dispatcher execution.
- **Resolution**: Implemented `EjecutarEnUI` helper dispatching via `Application.Current?.Dispatcher`.

---

## 2. Verification Record
- **Full Test Suite**: `dotnet test BimboProyecto.sln` -> Passed 514 / 514 tests.
- **Solution Compilation**: `dotnet build BimboProyecto.sln` -> 0 Errors, 0 Warnings (CapaDominio, CapaAplicacion4, CapaDatos, CapaUI, BimboProyecto.Tests).
- **Adversarial Unit Tests Added**:
  - `SupabaseJsonParsing_CadenasNumericas_IndependenciaCultural` (forced `es-ES` culture with string numbers)
  - `SupabaseJsonParsing_AdmiteObjetoIndividual` (JObject vs JArray payload handling)
  - `FormateoDeltas_SignosYSimbolos` (positive, negative, zero delta formatting)
  - `MermaClamping_Limites` (negative and overflow clamping)
  - `CalculoFechas_LimitesMes` (leap years, February limits, year transition in January)

---

## 3. Strict Scope Compliance
Checked git status:
- ONLY approved files modified/added.
- `EmpleadosView.xaml` restored.
- No other views or business flows touched.
