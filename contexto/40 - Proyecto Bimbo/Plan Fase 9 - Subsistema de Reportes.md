---
title: "Plan Fase 9 — Subsistema de Reportes"
tags: [plan, reportes, fase9, arquitectura]
date: 2026-07-26
estado: en_curso_parcial
---

# Plan Fase 9 — Subsistema de Reportes

> [!success] Primer flujo vertical implementado el 2026-08-16
> Bitácora ya genera PDF y Excel desde filas seleccionadas, registra la operación mediante `ingresar_reporte_tabla_bitacora` y entrega el archivo solo después del éxito de la RPC. El resto del subsistema general continúa pendiente.

> [!success] Reportería operativa implementada el 2026-08-17
> Cuatro consultas tipadas reutilizan el motor PDF/Excel: entrada de materia prima, proveedor, merma y primeros 10 productos. Incluyen vista previa, filtros por selectores de catálogo, branding de empresa y registro auditado previo a escribir el archivo. Ver [[Módulo Reportería]].

> [!success] Reporte de Pesaje de Insumos BES implementado el 2026-08-21
> La pantalla de Pesajes cuenta con generación directa en PDF y Excel desde el botón "Imprimir reporte", consolidando pesajes por producto frente a lo manifestado y calculando diferencias (kg y %), con soporte individual y multigestión de camiones. Ver [[Sesión 2026-08-21 - Reporte de Pesaje de Insumos BES (PDF y Excel)]].

> [!success] Selectores compactos y paginados en Reportería — implementado 2026-08-23
> Producto, Proveedor y Categoría se alojan en una tarjeta modal de 820×650,
> siguiendo el marco visual de `ProductoModal`, con paginación forzada de 50
> registros por página. Autor de la implementación: **Codex (sesión gestionada por Emanuel)**.
> Build y 113 pruebas automatizadas correctas; queda pendiente la comprobación visual autenticada.

Rama prevista: `feat/fase9-reportes` (convención existente `feat/faseN-nombre`).

## Objetivo

Infraestructura de generación, visualización, impresión y exportación de información: reportes financieros, auditorías de inventario, movimientos de almacén, bitácoras, facturación con QR/códigos de barras y gráficos. **No** es un conversor de datos a PDF: es un subsistema con abstracción propia, intercambiable y testeable.

## Stack decidido

| Necesidad | Herramienta | Licencia |
|---|---|---|
| PDF | PDFsharp/**MigraDoc** 6.2.4 | MIT |
| Excel con formato (< 50k filas) | ClosedXML 0.105.0 | MIT |
| Excel masivo (≥ 50k filas) | `DocumentFormat.OpenXml` (`OpenXmlWriter`, SAX) | MIT |
| CSV | `StreamWriter` (BCL) | — |
| Códigos de barras / QR | ZXing.Net 0.16.11 | Apache-2.0 |

Todos se agregan a **CapaDatos**. Justificación completa y alternativas descartadas en [[ADR-006 - Motor de Reportes y Exportación]].

## Arquitectura por capas

### CapaDominio/Reportes/ — abstracciones puras
Sin dependencias de NuGet de terceros (regla de oro de la capa).

```csharp
namespace CapaDominio.Reportes;

public enum ReportFormat { Pdf, Excel, ExcelMasivo, Csv }

public interface IReportRequest
{
    string ReportTitle { get; }
    ReportFormat Format { get; }
    IReadOnlyDictionary<string, object> Parameters { get; }
}
```

### CapaAplicacion4/Reportes/ — contratos y orquestación

```csharp
namespace CapaAplicacion.Reportes.Interfaces;

public interface IReportStrategy
{
    ReportFormat SupportedFormat { get; }
    Task<Result<byte[]>> GenerateAsync<TData>(TData data, IReportRequest request, CancellationToken ct = default);
}

public interface IReportGeneratorService
{
    Task<Result<byte[]>> ProcessAsync<TData>(TData data, IReportRequest request, CancellationToken ct = default);
}
```

`ReportGeneratorService` recibe `IEnumerable<IReportStrategy>` por DI y resuelve por `SupportedFormat`.

> [!tip] Reutilizar el patrón existente, no inventar uno nuevo
> Esto es exactamente lo que ya hace `SearchStrategyRegistry` con `ISearchStrategy` (`CapaAplicacion4/Search/Registry/`). Copiar ese patrón — ya está probado y documentado en [[ADR-002 - CQRS y Strategy para Buscador Universal]].

También en esta capa: los DTOs de reporte (`ProductoReporteDto`, etc.) y los Queries de MediatR que orquestan obtener datos → pedir generación. MediatR ya está en uso y registrado.

### CapaDatos/Reportes/Strategies/ — implementaciones

- `MigraDocReportStrategy` → PDF
- `ClosedXmlReportStrategy` → Excel con formato
- `OpenXmlSaxReportStrategy` → Excel masivo por streaming
- `CsvReportStrategy` → CSV

Todas envuelven su trabajo en el `TryAsync` de `CapaDatos/Repositories/RepositorioBase.cs`, que ya centraliza el fail-fast de conexión y devuelve `Result<T>`. No reimplementar manejo de errores.

### CapaUI/.../Pantallas/Reportes/
`ReportViewerViewModel` + vista, con el patrón MVVM del proyecto (`[ObservableProperty]`, `[RelayCommand]`, clase `partial`).

### Registro en DI
```csharp
// CapaDatos/DependencyInjection.cs — una línea por estrategia;
// el IEnumerable<IReportStrategy> lo resuelve el contenedor solo.
services.AddTransient<IReportStrategy, MigraDocReportStrategy>();
services.AddTransient<IReportStrategy, ClosedXmlReportStrategy>();
services.AddTransient<IReportStrategy, OpenXmlSaxReportStrategy>();
services.AddTransient<IReportStrategy, CsvReportStrategy>();

// CapaAplicacion4/DependencyInjection.cs
services.AddScoped<IReportGeneratorService, ReportGeneratorService>();

// CapaUI/App.xaml.cs
services.AddTransient<ReportViewerViewModel>();
```

## Flujo de ejecución

```
Vista WPF → comando en ReportViewerViewModel
    ↓ MediatR: GenerarReporteXxxQuery
    ↓ Handler consulta repositorio (CapaDatos) → DTO de reporte
    ↓ IReportGeneratorService resuelve IReportStrategy por ReportFormat
    ↓ Estrategia concreta genera → Result<byte[]>
    ↓ UI: si es PDF → visor; si es Excel/CSV → SaveFileDialog
```

## Sub-fases

**9.1 — Abstracciones y orquestador. ✅ Base implementada.** `CapaDominio/Reportes/` + `CapaAplicacion4/Reportes/` contienen formato, DTO tabular, contratos y `ReportGeneratorService` con resolución Strategy.

**9.2 — Motor PDF (MigraDoc). ✅ Primer caso implementado.** `PdfReportStrategy` genera el reporte tabular de Bitácora con autor, encabezado repetible y pie "Página X de Y". PDFsharp Core usa explícitamente el resolvedor de fuentes de Windows.

**9.3 — Excel y CSV. 🟡 Parcial.** `ExcelReportStrategy` con ClosedXML cubre Bitácora y los cuatro reportes operativos con columnas tipadas, filtros, totales y branding. CSV, OpenXML SAX y el umbral de 50.000 filas siguen pendientes.

**9.4 — Streaming desde Supabase.** Extender repositorios con `IAsyncEnumerable<T>` + keyset pagination para que la exportación masiva no materialice `List<T>`.
> [!warning] Esto es trabajo nuevo, no una adaptación
> Los repositorios actuales devuelven `PagedResult<T>` completo. El streaming real requiere diseñar el recorrido por páginas encadenadas. No subestimar esta sub-fase.

**9.5 — UI, vista previa e impresión. 🟡 Parcial.** `ReporteriaViewModel` y `ReporteriaView` incorporan selector 2×2, subformulario, vista previa paginada y `SaveFileDialog`. Los selectores de Producto, Proveedor y Categoría ya se presentan en una tarjeta compacta de 820×650 con 50 registros por página. Visor PDF interno e impresión siguen pendientes.

**9.6 — Códigos de barras/QR + pruebas + benchmarks.** ZXing.Net generando `byte[]` hacia los DTOs.
> [!success] Proyecto de pruebas creado
> `BimboProyecto.Tests` (xUnit) cubre la resolución de estrategias y la generación básica de PDF y Excel. Pruebas de carga, documentos incompletos y benchmarks continúan pendientes.

## Verificación

- `dotnet build BimboProyecto.sln --no-incremental` → 0 errores tras cada sub-fase.
  > El build **incremental** oculta advertencias preexistentes de `CapaDatos`. Usar siempre `--no-incremental` al medir advertencias.
- `dotnet list package --vulnerable --include-transitive` y confirmar que ninguna dependencia nueva tenga restricción comercial (ver [[Licencias de Librerías .NET - Auditoría 2026-07]]).
- Benchmarks con 100.000 y 1.000.000 de filas midiendo tiempo y RAM.
  > El informe original citaba 0,5 s/100k y 40 MB/1M sin fuente verificable. Tratar como orientativo y **medir en serio**.
- Abrir los PDF generados en un lector real — no basta con verificar que el `byte[]` no esté vacío.
- Verificación visual final: la hace Fernando (la app requiere login contra Supabase, no se automatiza).

## Relaciones

- [[ADR-006 - Motor de Reportes y Exportación]] — la decisión y su justificación
- [[PDFsharp MigraDoc - Referencia]] — capacidades del motor
- [[Licencias de Librerías .NET - Auditoría 2026-07]] — gobernanza de dependencias
- [[ADR-002 - CQRS y Strategy para Buscador Universal]] — patrón a replicar
- [[Arquitectura Actual]]
- [[Módulo Bitácora]]
- [[Sesión 2026-08-16 - Reportes PDF y Excel desde Bitácora]]
- [[Módulo Reportería]]
- [[Sesión 2026-08-17 - Módulo Reportería operativo]]
