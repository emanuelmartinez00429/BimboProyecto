---
title: Sesión 2026-07-26 — Investigación y Decisión del Motor de Reportes
type: sesion
status: vigente
tags:
  - sesion
  - reportes
  - licencias
  - investigacion
  - fase9
date: 2026-07-26
updated: 2026-07-26
summary: "Fernando aportó un informe técnico externo sobre el subsistema de reportes del ERP, que recomendaba QuestPDF (PDF) + ClosedXML/OpenXmlWriter (Excel). Pidió:…"
scope: []
symbols:
  - HeadingFormat
  - IReportStrategy
branch: feat/fase7-GestióndeUsuarios
autor_cambios: "Claude (Opus 5), dirigido por Fernando"
---

# Sesión 2026-07-26 — Investigación y Decisión del Motor de Reportes

## Pedido

Fernando aportó un informe técnico externo sobre el subsistema de reportes del ERP, que recomendaba **QuestPDF** (PDF) + **ClosedXML/OpenXmlWriter** (Excel). Pidió: verificar esa investigación contra fuentes oficiales por cuenta propia, elegir la mejor opción, documentar la decisión y dejar el plan armado **para la Fase 9** — porque la Fase 8 se dedica a Pesajes, que sigue incompleto.

## Hallazgo principal: la recomendación de PDF del informe era inviable legalmente

El informe proponía QuestPDF y su código de ejemplo fijaba `LicenseType.Community`. Verificado en la documentación oficial de QuestPDF, esa licencia **no aplica a este proyecto**:

- Exige ingresos brutos anuales < USD 1M, medidos *"on a consolidated basis across entities under common control"* → una filial se evalúa por la facturación de la matriz.
- Excluye explícitamente a las *publicly traded companies* → Grupo Bimbo cotiza en la BMV.

Se le preguntó a Fernando el destino real del sistema. **Confirmó: uso real en la operación de Bimbo Honduras**, no proyecto académico. Con eso, usar la Community License sería incumplimiento; la vía legal serían USD 1,999 (Professional) o USD 4,999 (Enterprise).

Agravante: QuestPDF **no valida la licencia en runtime ni recopila telemetría**. El incumplimiento no genera error, warning ni marca de agua — es puramente legal, y por eso pasa desapercibido.

## El informe descartó la mejor opción con datos desactualizados

El informe descartó PDFsharp/MigraDoc por *"velocidad de actualización lenta"*. Verificado en NuGet y GitHub oficiales, eso ya no es cierto: **v6.2.4 estable de enero 2026**, v7.0.0 Preview de marzo 2026, targets net8/9/10, 8.6M descargas, licencia **MIT**.

Y MigraDoc tiene justo lo que un ERP necesita: tablas con encabezado repetido entre páginas (`HeadingFormat`), headers/footers por página par/impar, saltos de página y numeración automáticos, gráficos de negocio, y salida PDF **y RTF**.

Fernando eligió **PDFsharp/MigraDoc**.

## Lo que sí se confirmó del informe

La estrategia de Excel es correcta y se adopta sin cambios: **ClosedXML** (MIT) para < 50k filas con formato, **OpenXmlWriter/SAX** (MIT, Microsoft) para exportación masiva. También acertó al descartar EPPlus por su licencia Polyform Noncommercial.

## Errores del informe que habrían roto el build

El código C# del informe no compilaría contra este repo:

| El informe usa | El proyecto real usa |
|---|---|
| `Result<T>.Success()` / `.Failure()` | `Result<T>.Ok()` / `.Fail()` |
| `result.IsSuccess` | `result.Success` |
| `namespace Bimbo.CapaAplicacion.*` | `namespace CapaAplicacion.*` |
| `.NET 8/9` | `net8.0` / `net8.0-windows` |
| Asume proyecto de pruebas | **No existe ninguno en la solución** |

A favor del informe: **MediatR sí está en uso** y registrado, así que su flujo CQRS encaja con lo que ya existe.

Los benchmarks que citaba (0,5 s/100k filas, 40 MB/1M) venían con marcadores `[cite: 18]` y **no se pudieron verificar** con fuente independiente. Quedan como orientativos, a medir en Fase 9.

## Documentación generada

- [[ADR-006 - Motor de Reportes y Exportación]] — decisión, hallazgo de licencia, alternativas descartadas, trade-off de PDF/A aceptado
- [[PDFsharp MigraDoc - Referencia]] — capacidades y limitaciones verificadas, fuentes oficiales
- [[Licencias de Librerías .NET - Auditoría 2026-07]] — tabla de licencias + **nueva regla de gobernanza**: ninguna dependencia nueva con umbral de ingresos o restricción por cotización en bolsa
- [[Plan Fase 9 - Subsistema de Reportes]] — arquitectura por capas, sub-fases y verificación

## Limitación aceptada

No se pudo confirmar soporte **PDF/A** en PDFsharp con fuente oficial. Se documentó como trade-off explícito: si Contabilidad lo exige, la abstracción `IReportStrategy` permite sumar una estrategia con otro motor solo para esos reportes.

## Estado

**Nada de código implementado** — esta sesión produjo únicamente la investigación y la documentación. La implementación arranca en Fase 9, después de Pesajes (Fase 8).

## Relaciones

- [[ADR-006 - Motor de Reportes y Exportación]]
- [[Plan Fase 9 - Subsistema de Reportes]]
- [[Licencias de Librerías .NET - Auditoría 2026-07]]
- [[PDFsharp MigraDoc - Referencia]]
