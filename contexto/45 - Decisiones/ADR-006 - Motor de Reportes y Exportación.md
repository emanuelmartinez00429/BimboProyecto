---
title: "ADR-006 — Motor de Reportes y Exportación"
tags: [adr, reportes, pdf, excel, licencias, fase9]
date: 2026-07-26
estado: aceptado
---

# ADR-006 — Motor de Reportes y Exportación

## Contexto

La Fase 9 construirá el subsistema de reportes del ERP: PDF, Excel, CSV, impresión, códigos de barras/QR y gráficos. Se recibió un informe técnico externo que recomendaba **QuestPDF** para PDF y una estrategia híbrida **ClosedXML + OpenXmlWriter** para Excel.

Se verificó ese informe contra fuentes oficiales (NuGet, GitHub, sitios de los fabricantes) el 2026-07-26. La verificación **confirmó la estrategia de Excel** pero encontró un **problema bloqueante de licencia en la recomendación de PDF**.

## Hallazgo bloqueante: QuestPDF Community no aplica a este proyecto

El informe proponía usar `QuestPDF.Settings.License = LicenseType.Community`. Según la [guía oficial de selección de licencia de QuestPDF](https://www.questpdf.com/license/guide.html), la Community License **no aplica** aquí:

| Condición oficial | Situación del ERP BIMBO |
|---|---|
| Ingresos brutos anuales < USD 1,000,000 | ❌ Grupo Bimbo factura miles de millones |
| Se mide *"on a consolidated basis across entities under common control"* | ❌ Bimbo Honduras es filial → se mide por la matriz |
| Inelegibles: *"publicly traded companies"* | ❌ Grupo Bimbo cotiza en la BMV |

El destino confirmado del sistema es **uso real en la operación de Bimbo Honduras**. Usar la Community License sería un incumplimiento; la vía legal sería Professional (USD 1,999) o Enterprise (USD 4,999), ambas perpetuas con 1 año de updates.

> [!danger] Trampa silenciosa
> QuestPDF **no valida la licencia en runtime ni recopila telemetría** (confirmado en su [documentación de configuración](https://www.questpdf.com/license/configuration.html)). El incumplimiento no produce error, warning ni marca de agua — es una obligación puramente legal. Por eso es fácil de cometer sin darse cuenta y difícil de detectar en una auditoría de código.

## Decisión

**PDF: PDFsharp/MigraDoc** (licencia MIT).
**Excel con formato (< 50.000 filas): ClosedXML** (MIT).
**Excel masivo (≥ 50.000 filas): DocumentFormat.OpenXml / `OpenXmlWriter`, modelo SAX** (MIT).
**CSV: `StreamWriter`** (BCL).
**Códigos de barras y QR: ZXing.Net** (Apache-2.0).

Todo detrás de una abstracción `IReportStrategy` resuelta por `ReportFormat`, replicando el patrón que ya usa `SearchStrategyRegistry` con `ISearchStrategy`.

## Por qué PDFsharp/MigraDoc

El informe lo descartó por *"velocidad de actualización lenta"* y falta de características modernas. **Ese dato estaba desactualizado.** Verificado:

- **MIT**: sin umbral de ingresos, sin restricción por cotizar en bolsa, sin costo, sin riesgo de cambio de términos comerciales a 10 años.
- **Activo**: v6.2.4 estable (2026-01-06); v7.0.0 Preview 1 (2026-03-24). 8.6M descargas.
- **Frameworks**: net8.0, net9.0, net10.0, netstandard2.0 — cubre el `net8.0` actual y la migración futura.
- **MigraDoc** aporta lo que un ERP necesita: DOM de documento (secciones, párrafos, tablas, estilos), **tablas con encabezado repetido entre páginas** (`HeadingFormat = true`), headers/footers por página par/impar y primera página, **saltos de página y numeración automáticos**, gráficos de negocio, TOC/bookmarks, y salida a PDF **y RTF**.

### Comparación honesta

QuestPDF es superior en dos puntos reales: API fluida más ergonómica y **soporte nativo de PDF/A y PDF/UA**.

| Criterio | PDFsharp/MigraDoc | QuestPDF |
|---|---|---|
| Licencia para este caso | ✅ MIT, sin costo | ❌ Requiere pago |
| Code-First / testeable | ✅ C# puro (DOM) | ✅ Fluent API |
| Tabla con header repetido | ✅ `HeadingFormat` | ✅ |
| Saltos de página automáticos | ✅ (MigraDoc) | ✅ |
| Gráficos de negocio | ✅ Básicos | ✅ |
| PDF/A · PDF/UA | ⚠️ **No confirmado** | ✅ Nativo |
| Salida RTF | ✅ | ❌ |
| Riesgo de licencia a 10 años | Ninguno | Cambio de términos |

## Trade-off aceptado

**No se pudo confirmar soporte PDF/A en PDFsharp con fuente oficial.** Se acepta conscientemente. Si Contabilidad llegara a exigir archivado PDF/A o facturación electrónica EN 16931, la abstracción `IReportStrategy` permite agregar una estrategia con otro motor **solo para esos reportes específicos**, sin tocar el resto del subsistema ni la lógica de negocio. Ese es justamente el propósito del patrón Strategy acá.

## Alternativas descartadas

| Alternativa | Razón del descarte |
|---|---|
| **QuestPDF Community** | Incumplimiento de licencia para este despliegue (ver arriba) |
| **QuestPDF Professional/Enterprise** | Viable técnicamente, pero USD 1,999–4,999 y dependencia comercial que MIT evita sin pérdida funcional relevante |
| **iText 8** | AGPLv3 obliga a liberar el código fuente de la aplicación, o pagar licencia comercial de costo elevado |
| **IronPDF** | Comercial por desarrollador/servidor + Chromium embebido → consumo de RAM desproporcionado para lotes masivos |
| **DevExpress / FastReport / Stimulsoft / Telerik / Syncfusion** | Comerciales por desarrollador; plantillas propietarias (REPX/FRX/MRT) que el compilador no valida, ensucian los diffs de Git y dificultan las pruebas automatizadas |
| **RDLC / ReportViewer** | Evolución casi nula en .NET moderno |
| **Crystal Reports** | Dependencias legacy COM, sin soporte nativo .NET moderno |
| **EPPlus** | Licencia Polyform Noncommercial → exige licencia comercial **por desarrollador** |

## Consecuencias

- ✅ Cero costo de licencias y cero riesgo legal en el subsistema de reportes.
- ✅ Todas las dependencias nuevas son MIT o Apache-2.0.
- ✅ Los reportes son código C# fuertemente tipado: refactorizables, versionables con diffs legibles y testeables sin instanciar WPF.
- ⚠️ Sin PDF/A confirmado (mitigable con una estrategia adicional si se requiere).
- ⚠️ La API de `OpenXmlWriter` es de bajo nivel y verbosa — es el precio del consumo de RAM constante en exportaciones masivas.
- 📌 Se establece una **regla de gobernanza**: ninguna dependencia nueva del proyecto puede tener umbral de ingresos ni restricción por cotización en bolsa. Ver [[Licencias de Librerías .NET - Auditoría 2026-07]].

## Relaciones

- [[Licencias de Librerías .NET - Auditoría 2026-07]] — tabla completa de licencias verificadas
- [[PDFsharp MigraDoc - Referencia]] — capacidades y limitaciones del motor elegido
- [[Plan Fase 9 - Subsistema de Reportes]] — plan de implementación
- [[ADR-002 - CQRS y Strategy para Buscador Universal]] — el patrón Strategy que se replica acá
- [[ADR-001 - Result Pattern en Repositorios]] — las estrategias devuelven `Result<byte[]>`
- [[Sesión 2026-07-26 - Investigación y Decisión del Motor de Reportes]]
