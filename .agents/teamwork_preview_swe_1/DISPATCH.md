## 2026-09-17T19:12:54Z

Implement live data integration for the existing Dashboard view in BimboProyecto strictly according to the approved implementation plan.

Working directory: C:\Users\Emanuel Lazo\Source\Repos\BimboProyecto
Integrity mode: development

## Context & Scope Constraints
- **Strict Scope Rule**: Touch ONLY the files planned. Do not modify other forms, views, or business flows.
- Dashboard stays under its existing route in Reportería (do NOT change home/welcome screen).
- Trend badges without comparison history must display "-".
- Merma thresholds remain hardcoded for now (note for future configuration).
- ADR-026 Compliance: Cache ONLY inventory catalog counts (5 min, tagged). Zero-cache for pesajes, merma, and real-time feeds.

## Requirements

### R1. Database Migration (RPC `consultar_kpis_pesajes`)
Create a migration in `supabase/migrations/` defining `consultar_kpis_pesajes` with `SECURITY INVOKER` and `search_path = ''`. It must compute in a single pass using PostgreSQL `FILTER (WHERE ...)`:
- Current and previous period weighing counts (`pesajes_actual`, `pesajes_anterior`)
- Current and previous net weight sums (`neto_actual`, `neto_anterior`)
- Manifested theoretical weight and received weight for the current period (excluding state 9 / cancelled)

### R2. Application Contracts & DTOs (`CapaAplicacion4`)
In `CapaAplicacion4/Dashboard/`:
- `DashboardDtos.cs`: Define `PeriodoDashboard` (Hoy, Semana, Mes), `KpisInventarioDto`, `KpisPesajesDto`, `MermaProductoDto`, and `UltimoPesajeDto`. Nullable change percentages/diffs.
- `Interfaces/IDashboardRepository.cs`: Declare methods for:
  - `ObtenerKpisInventarioAsync`
  - `ObtenerKpisPesajesAsync(PeriodoDashboard)`
  - `ObtenerTopMermaAsync(PeriodoDashboard, int top = 5)`
  - `ObtenerUltimosPesajesAsync(int cantidad = 5)`

### R3. Data Repository (`CapaDatos`)
In `CapaDatos/Repositories/Dashboard/DashboardRepository.cs`:
- Inherit from `RepositorioBase`.
- Query catalog tables (`productos`, `proveedores`, `fabricante`) with `FusionCache` (5-minute TTL, tags: `TagsCache.CatalogosRaiz`).
- Call `consultar_kpis_pesajes` for pesajes KPIs (zero-cache).
- Reuse `consultar_reporte_productos_merma` for top merma (zero-cache).
- Query `entradas_producto` joined with products for the 5 most recent weighings (zero-cache).
- Register `IDashboardRepository` as Singleton/Scoped in `CapaDatos/DependencyInjection.cs`.

### R4. ViewModel Refactoring (`CapaUI`)
In `CapaUI/Formularios/Dashboard/DashboardVM.cs`:
- Inject `IDashboardRepository`, `IRealtimeService`, and `IConexionMonitor`.
- Replace mock strings with observable properties supporting live data loading and loading/error states.
- Format trend indicators with "-" when comparison data is null or unavailable.
- Implement asynchronous data loading (`InicializarAsync`, `RefreshCommand`).
- Handle `SelectPeriodoCommand` by updating `Periodo` and reloading pesajes + merma without reloading catalog inventory.
- Connect `IRealtimeService` to stream new entries into `Ultimos`.

### R5. Dependency Injection & Navigation Route
In `CapaUI/Formularios/Principal/MainViewModel.cs`:
- Update `[Routes.Dashboard]` route from direct instantiation `() => new Dashboard.DashboardVM()` to resolution via DI container: `() => App.CrearVm<Dashboard.DashboardVM>()`.
- Register `DashboardVM` in `CapaUI` DI if not already present.

## Acceptance Criteria

### Execution & Build
- [ ] Solution compiles cleanly without C# compiler errors or broken bindings.
- [ ] No files outside the approved list are touched or modified.
- [ ] ADR-026 zero-cache constraints strictly respected on pesajes, merma, and reports.
- [ ] Trend badges display "-" when prior period has no records.
- [ ] Realtime subscription connects to `entradas_producto` to append new weighings live.
