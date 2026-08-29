---
title: Sesión 2026-08-21 — Reporte de Pesaje de Insumos BES (PDF y Excel)
type: sesion
status: vigente
tags:
  - sesion
  - pesaje
  - reportes
  - pdf
  - excel
  - migraciones
date: 2026-08-21
updated: 2026-08-21
summary: "Se implementó la generación real de reportes en PDF y Excel desde el botón \"Imprimir reporte\" del módulo de Recepción de Materia Prima (Pesaje), reproduciendo…"
scope:
  - BimboProyecto.Tests/Reportes
  - CapaUI/Formularios/Principal/Pantallas/Pesaje
  - CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales
symbols:
  - AbrirReporte
  - ChkTodosCamiones
  - GenerarReportePesajesAsync
  - IReportGeneratorService
  - IReporteRepository
  - LogoEmpresaCache
  - PesajeView
  - ReporteModal
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Fernando / Antigravity (agente)
---

# Sesión 2026-08-21 — Reporte de Pesaje de Insumos BES (PDF y Excel)

> [!success] Resultado
> Se implementó la generación real de reportes en PDF y Excel desde el botón **"Imprimir reporte"** del módulo de Recepción de Materia Prima (Pesaje), reproduciendo fielmente el formato institucional de **"Pesado de Insumos BES"** (Figura 28). Soporta exportación individual de camión o consolidada de múltiples camiones, cálculo consolidado de pesajes por producto frente a lo manifestado, diferencias absolutas y porcentuales, auditoría previa vía RPC y apertura automática del archivo.

---

## Problema / motivo

1. El botón "Imprimir reporte" en `PesajeView` solo abría un modal informativo ficticio (`ReporteModal`) que decía "Reporte generado" pero no emitía ningún documento descargable ni registraba la auditoría.
2. Se requería consolidar todas las entradas individuales de un producto en una sola fila por producto pesado, comparando el peso neto recibido contra el peso manifestado y calculando la diferencia en kilogramos y porcentaje.
3. Se requería soporte para elegir entre **PDF** y **Excel**, así como la opción de emitir el reporte del camión seleccionado o de todos los camiones activos.

---

## Cambios realizados

### 1. `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/ReporteModal.xaml` & `.cs`
- Rediseño con soporte para selección de formato con botones temáticos de **PDF** (azul) y **Excel** (verde).
- Inclusión de casilla de verificación `ChkTodosCamiones` para elegir entre el camión seleccionado o todos los camiones activos.
- Lista interactiva de los camiones involucrados en la exportación.

### 2. `CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeViewModel.cs`
- Inyección de `IReportGeneratorService`, `IReporteRepository` y `LogoEmpresaCache`.
- Implementación de `GenerarReportePesajesAsync`:
  - Agrupación y consolidación de entradas por producto.
  - Cálculo de `Diferencia (KG) = Peso Recibido - Peso Manifestado`.
  - Cálculo de `Diferencia (%) = ((Peso Recibido - Peso Manifestado) / Peso Manifestado) * 100`.
  - Estructuración de columnas: `FECHA ASIG.`, `PLACA`, `PRODUCTO`, `PROVEEDOR`, `BULTOS (APROX)`, `PESO MANIFESTADO`, `PESO BRUTO`, `PESO TARA`, `PESO RECIBIDO`, `DIF. (KG)`, `DIF. (%)`.
  - Totales consolidados de bultos, manifestado, bruto, tara, recibido y diferencias.
  - Registro de auditoría formal mediante `IReporteRepository.RegistrarAsync` (RPC Supabase `ingresar_reporte_tabla_bitacora`).
  - Escritura segura con archivo temporal y reemplazo definitivo.

### 3. `CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml.cs`
- Conexión de `AbrirReporte` al nuevo `ReporteModal`.
- Integración con `Microsoft.Win32.SaveFileDialog` para que el usuario elija la ruta destino `.pdf` o `.xlsx`.
- Apertura automática del archivo generado tras su confirmación de guardado y auditoría.

### 4. `BimboProyecto.Tests/Reportes/ReportStrategyTests.cs`
- Nueva prueba unitaria `Pesaje_GeneraReportePesadoInsumosBes_EnPdfYExcel` que valida la generación exitosa de la estructura de reporte tanto en PDF (MigraDoc) como en Excel (ClosedXML).

---

## Verificación

- `dotnet test BimboProyecto.Tests\BimboProyecto.Tests.csproj` → **113/113 pruebas superadas con éxito (100% ✅)**.
- Compilación limpia de CapaDominio, CapaAplicacion y BimboProyecto.Tests.

## Relaciones

- [[Módulo Reportería]]
- [[Plan Fase 9 - Subsistema de Reportes]]
- [[ADR-006 - Motor de Reportes y Exportación]]
- [[Sesión 2026-08-16 - Reportes PDF y Excel desde Bitácora]]
