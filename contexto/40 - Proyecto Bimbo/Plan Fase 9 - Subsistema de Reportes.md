---
title: "Plan Fase 9 — Subsistema de Reportes"
tags: [plan, reportes, fase9, arquitectura]
date: 2026-07-26
estado: planificado
---

# Plan Fase 9 — Subsistema de Reportes

> [!info] Esta fase NO está en curso
> La **Fase 8 va primero**: Pesajes, que sigue incompleto y con errores pendientes. Este documento existe para que la Fase 9 arranque sin re-investigar. Decisión de herramientas ya tomada en [[ADR-006 - Motor de Reportes y Exportación]].

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

**9.1 — Abstracciones y orquestador.** `CapaDominio/Reportes/` + `CapaAplicacion4/Reportes/` (contratos, DTOs, `ReportGeneratorService`). Sin NuGet nuevos todavía. Build verde.

**9.2 — Motor PDF (MigraDoc).** Paquete + `MigraDocReportStrategy` + componentes reutilizables: encabezado corporativo, pie "Página X de Y", tabla con `HeadingFormat`. Primer reporte real de punta a punta — sugerido **Bitácora o Productos**, que ya tienen repositorio paginado funcionando.

**9.3 — Excel y CSV.** Las tres estrategias restantes. El umbral de conmutación (50.000 filas) va como **constante documentada**, no como número mágico.

**9.4 — Streaming desde Supabase.** Extender repositorios con `IAsyncEnumerable<T>` + keyset pagination para que la exportación masiva no materialice `List<T>`.
> [!warning] Esto es trabajo nuevo, no una adaptación
> Los repositorios actuales devuelven `PagedResult<T>` completo. El streaming real requiere diseñar el recorrido por páginas encadenadas. No subestimar esta sub-fase.

**9.5 — UI, vista previa e impresión.** `ReportViewerViewModel` + vista; `PrintDialog` nativo de Windows; `SaveFileDialog` para guardar.

**9.6 — Códigos de barras/QR + pruebas + benchmarks.** ZXing.Net generando `byte[]` hacia los DTOs.
> [!important] Crear el proyecto de pruebas — no existe
> La solución tiene 5 proyectos y **ninguno de tests**. Crear `BimboProyecto.Tests` (xUnit) cubriendo: resolución de estrategias en `ReportGeneratorService`, y generación de bytes válidos ante DTOs correctos/incompletos. Es la primera vez que el proyecto tendrá pruebas automatizadas.

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
