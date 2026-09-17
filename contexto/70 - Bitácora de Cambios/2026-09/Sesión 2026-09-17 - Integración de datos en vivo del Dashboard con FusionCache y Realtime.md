---
title: "Sesión 2026-09-17 — Integración de datos en vivo del Dashboard con FusionCache y Realtime"
tags:
  - sesion
  - dashboard
  - wpf
  - fusioncache
  - realtime
  - supabase
  - rpc
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente) — Sesión Fernando
---

# Sesión 2026-09-17 — Integración de datos en vivo del Dashboard con FusionCache y Realtime

> [!success] Resultado
> Se conectó la vista existente del Dashboard (`DashboardView.xaml`) a datos reales en vivo desde Supabase, reemplazando el 100% de los datos mock en `DashboardVM.cs`. La arquitectura implementa el Approach A: repositorio dedicado `DashboardRepository` con caché L1 `FusionCache` etiquetada para catálogos (ADR-026), política zero-cache para pesajes/mermas, una función RPC PostgreSQL `consultar_kpis_pesajes` optimizada en una sola pasada con `FILTER (WHERE ...)`, y suscripción reactiva Realtime para el feed de pesajes recientes.

---

## Problema / motivo

`DashboardView.xaml` (614 líneas) contaba con una interfaz completa y estilizada bajo la ruta de Reportería, pero su ViewModel (`DashboardVM.cs`) contenía datos fijos (mock) documentados en `Plan de Tests Unitarios.md` como pendiente. No existían contratos de aplicación, repositorio de datos ni endpoints en base de datos para alimentar sus 6 tarjetas de KPI, la gráfica de mermas y la lista de pesajes en tiempo real.

---

## Cambios aplicados

### 1. Base de datos (Supabase)
- **`supabase/migrations/20260917132500_consultar_kpis_pesajes.sql`**:
  - Función RPC `public.consultar_kpis_pesajes(p_fecha_desde_actual, p_fecha_hasta_actual, p_fecha_desde_anterior, p_fecha_hasta_anterior)` con `SECURITY INVOKER` y `search_path = ''`.
  - Agrupa movimientos y entradas de pesaje no anuladas (`id_estado <> 9`) computando en una sola pasada con `FILTER (WHERE ...)`:
    - Conteo de pesajes del período actual y anterior (`pesajes_actual`, `pesajes_anterior`).
    - Suma de peso neto actual y anterior (`neto_actual`, `neto_anterior`).
    - Peso teórico manifestado y peso recibido para cálculo de merma en el período actual.
  - Concesión de `EXECUTE` a `authenticated`, `anon` y `service_role`.
  - Aplicada en base viva de Supabase vía herramienta MCP `apply_migration`.

### 2. Capa de Aplicación (`CapaAplicacion4`)
- **`CapaAplicacion4/Dashboard/Dtos/DashboardDtos.cs`**:
  - `PeriodoDashboard`: Enum con valores `Hoy`, `Semana`, `Mes`.
  - `KpisInventarioDto`: Conteos de productos activos, proveedores activos, fabricantes activos y deltas opcionales.
  - `KpisPesajesDto`: Conteos y pesos netos con porcentajes de cambio `decimal?` (`null` cuando no hay historial previo).
  - `MermaProductoDto`: Nombre, porcentaje de merma y kilos de merma por producto.
  - `UltimoPesajeDto`: Identificador, código, nombre, kilos netos, hora y bandera de alerta.
- **`CapaAplicacion4/Dashboard/Interfaces/IDashboardRepository.cs`**:
  - Contrato desacoplado con métodos asíncronos y soporte para `CancellationToken`:
    - `ObtenerKpisInventarioAsync(CancellationToken)`
    - `ObtenerKpisPesajesAsync(PeriodoDashboard, CancellationToken)`
    - `ObtenerTopMermaAsync(PeriodoDashboard, int top = 5, CancellationToken)`
    - `ObtenerUltimosPesajesAsync(int cantidad = 5, CancellationToken)`

### 3. Capa de Datos (`CapaDatos`)
- **`CapaDatos/Repositories/Dashboard/DashboardRepository.cs`**:
  - Hereda de `RepositorioBase` con telemetría y verificación de conexión `IConexionMonitor`.
  - **Alineación con ADR-026**:
    - Conteos de catálogos (`productos`, `proveedores`, `fabricante`) cacheados con `ICacheService` (FusionCache L1) por 5 minutos bajo el tag `TagsCache.CatalogosRaiz`, invalidándose automáticamente al llegar eventos WAL de catálogos.
    - **Zero-Cache**: Métricas de pesajes (vía RPC `consultar_kpis_pesajes`), reporte de mermas (vía RPC `consultar_reporte_productos_merma`) y últimos 5 pesajes se consultan directamente sin almacenamiento en caché.
- **`CapaDatos/DependencyInjection.cs`**:
  - Registro de `IDashboardRepository` como `Singleton` en el contenedor de servicios.

### 4. Capa de Presentación (`CapaUI`)
- **`CapaUI/Formularios/Dashboard/DashboardVM.cs`**:
  - Refactorizado para heredar de `RealtimeAwareViewModel` (gestión de conexión y reconexión limpia).
  - Inyección de dependencias de `IDashboardRepository`, `IRealtimeService` y `IConexionMonitor`.
  - Propiedades reactivas con notificación de cambio (`DateLabel`, `ProdCount`, `ProvCount`, `MarcaCount`, `PesajeCount`, `TotalNeto`, `PctMerma`, `TopMerma`, `Ultimos`, `IsLoading`, `HasError`).
  - Formateo de badges con fallback `"—"` cuando no existe historial previo para calcular la tendencia.
  - Comando `RefreshCommand` para actualización manual desde el botón de la UI.
  - Comando `SelectPeriodoCommand` que conmuta períodos (`Hoy`, `Semana`, `Mes`) recargando exclusivamente pesajes y mermas sin invalidar ni reconsultar los catálogos.
  - Suscripción en tiempo real a la tabla `entradas_producto` para insertar automáticamente nuevas pesadas en la colección `Ultimos` manteniendo el tope de 5 elementos.
- **`CapaUI/Formularios/Dashboard/DashboardView.xaml`**:
  - Enlace de indicadores de carga y estado.
- **`CapaUI/Formularios/Principal/MainViewModel.cs`**:
  - Ruta `[Routes.Dashboard]` actualizada para resolver la instancia a través del contenedor: `() => App.CrearVm<Dashboard.DashboardVM>()`.
- **`CapaUI/App.xaml.cs`**:
  - Registro de `DashboardVM` como `Transient` en la configuración de servicios.

### 5. Suite de Pruebas (`BimboProyecto.Tests`)
- **`BimboProyecto.Tests/Dashboard/DashboardIntegrationTests.cs`**:
  - Cobertura de mapeo de DTOs, cálculos de merma, comportamiento de fallback `"—"` en ausencia de datos históricos y lógica del ViewModel.

---

## Verificación

1. **Compilación en Release**:
   - `dotnet build BimboProyecto.sln -c Release` → 0 errores, 0 advertencias.
2. **Ejecución de Pruebas Unitarias**:
   - `dotnet test BimboProyecto.sln --no-restore -c Release` → **525 pruebas aprobadas**, 0 fallidas, 0 omitidas.
3. **Base de Datos**:
   - Migración aplicada y verificada en el proyecto Supabase `bzmmrifjgzlvsphctais`.
4. **Disciplina de Scope**:
   - Se verificó que ningún archivo fuera del alcance aprobado del plan de implementación fuera alterado.

---

## Lo que NO cambió

- La pantalla de inicio al iniciar sesión sigue siendo `WelcomeScreen.xaml` (no se alteró la bienvenida).
- La ubicación del Dashboard en el sidebar se mantiene intacta bajo el grupo Reportería.
- Los umbrales visuales de merma (< 3% normal, 3–4% alerta, >= 4% crítico) permanecen fijos en UI (se evaluará parametrización por empresa en futuras fases).

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Dashboard]]
- [[Módulo Reportería]]
- [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]]
