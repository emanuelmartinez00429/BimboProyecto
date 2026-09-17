---
title: "Sesión 2026-09-17 — Filtros independientes en Dashboard"
tags:
  - sesion
  - dashboard
  - wpf
  - filtros
  - merma
  - realtime
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (agente) — Sesión Fernando
---

# Sesión 2026-09-17 — Filtros independientes en Dashboard

> [!success] Resultado
> El selector desplegable del encabezado y los RadioButtons del Top 5 de mermas funcionan de manera independiente. Fernando probó el flujo en la aplicación y aprobó el resultado visual y funcional.

---

## Problema / motivo

El Dashboard compartía una única propiedad `Periodo` y un único comando para dos necesidades distintas. Elegir un período para revisar los KPI de pesajes también modificaba el período del Top 5 de mermas, impidiendo consultar, por ejemplo, los productos registrados durante los últimos 30 días mientras se observaban las mermas del día.

Además, la opción visible como `Mes` representaba el mes calendario. El requisito funcional era consultar una ventana móvil desde la fecha actual hacia atrás.

## Cambios aplicados

### Presentación y ViewModel

- `CapaUI/Formularios/Dashboard/DashboardVM.cs`:
  - Se reemplazó el estado compartido por `PeriodoKpis` y `PeriodoMerma`.
  - Se separaron `SelectPeriodoKpisCommand` y `SelectPeriodoMermaCommand`.
  - Los KPI y el Top 5 tienen cargas, errores y cancelaciones independientes.
  - Las respuestas obsoletas se descartan comparando el período solicitado con el período vigente de su sección.
  - La actualización manual y los eventos Realtime respetan ambas selecciones.
- `CapaUI/Formularios/Dashboard/DashboardView.xaml`:
  - El selector superior quedó enlazado a `PeriodoKpis`.
  - Los RadioButtons quedaron enlazados a `PeriodoMerma`.
  - Los parámetros se tiparon con `{x:Static dashboard:PeriodoDashboard.*}`.
  - La etiqueta `Mes` cambió a `Últimos 30 días`.
- `CapaUI/Formularios/Dashboard/DashboardView.xaml.cs`:
  - El desplegable ejecuta únicamente `SelectPeriodoKpisCommand` con un `PeriodoDashboard` tipado.

### Rangos de fechas

- `CapaDatos/Repositories/Dashboard/DashboardRepository.cs` centraliza el cálculo de períodos para KPI y mermas.
- `Hoy`: fecha actual; su comparación es ayer.
- `Semana`: lunes a domingo; su comparación es la semana anterior.
- `PeriodoDashboard.Mes`: ventana móvil inclusiva desde `hoy - 29` hasta `hoy`; su comparación usa los 30 días inmediatamente anteriores.

### Alcance de cada control

| Sección | Período aplicado |
|---|---|
| Productos, Proveedores y Marcas | Corte actual, sin filtro temporal |
| Total Pesajes, Total Neto y % Merma | Selector desplegable del encabezado |
| Top 5 de productos con merma | RadioButtons propios |
| Últimos cinco pesajes | Global, sin filtro temporal |

### Pruebas

- `BimboProyecto.Tests/Dashboard/DashboardUnitTests.cs` se actualizó para verificar la ventana móvil de 30 días, el período anterior contiguo y el rango enviado al reporte de mermas.

## Verificación

- `dotnet build BimboProyecto.sln --no-restore` → **0 errores y 0 advertencias**.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-build --no-restore` → **526 aprobadas, 0 fallidas, 0 omitidas**.
- Arnés WPF de instanciación: `DashboardView` se creó y midió correctamente a 1400×900.
- `git diff --check` → sin errores de whitespace.
- Prueba manual de Fernando: combinación de períodos distintos entre KPI y mermas, aprobada el 2026-09-17.

## Lo que no cambió

- No se modificaron interfaces públicas, DTOs, esquema de base de datos, migraciones ni RPC.
- Se conserva `PeriodoDashboard.Mes` como valor interno para no romper contratos existentes; solo cambió su semántica y etiqueta visible.
- No se realizaron commit ni push como parte de esta sesión.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Dashboard]]
- [[Sesión 2026-09-17 - Integración de datos en vivo del Dashboard con FusionCache y Realtime]]
- [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]]
