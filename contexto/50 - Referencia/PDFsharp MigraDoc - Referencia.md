---
title: "PDFsharp / MigraDoc — Referencia"
tags: [referencia, pdf, reportes, dotnet, migradoc]
date: 2026-07-26
lifecycle: verified
---

# PDFsharp / MigraDoc — Referencia

Motor de generación de PDF elegido para el subsistema de reportes ([[ADR-006 - Motor de Reportes y Exportación]]). Licencia **MIT**.

## PDFsharp vs MigraDoc — cuál usar

Son dos bibliotecas complementarias del mismo proyecto:

| | PDFsharp | MigraDoc |
|---|---|---|
| Nivel | Bajo: dibujo directo (similar a GDI+) | Alto: modelo de objetos de documento |
| Se piensa en | Coordenadas, páginas, trazos | Secciones, párrafos, tablas, estilos |
| Saltos de página | **Manuales** | **Automáticos** |
| Salidas | PDF | PDF **y RTF** |

> [!important] Para reportes de ERP se usa **MigraDoc**, no PDFsharp directo
> La documentación oficial es explícita: *"Only basic text layout is supported by PDFsharp, and page breaks are not created automatically."* Un reporte tabular que abarca varias páginas necesita el cálculo automático de saltos y la repetición de encabezados — eso solo lo da MigraDoc. PDFsharp queda para casos de dibujo fino o post-procesamiento de un PDF ya generado.

## Capacidades verificadas

- **Modelo de documento**: un documento tiene *secciones*, y las secciones contienen el contenido. Las páginas se crean al renderizar.
- **Tablas**: se definen columnas, luego se agregan filas y contenido a las celdas. Marcando `HeadingFormat = true` en la primera fila, **ese encabezado se repite automáticamente cuando la tabla abarca varias páginas** — clave para reportes largos.
- **Headers y footers**: admiten párrafos, tablas, imágenes y gráficos. Se pueden definir **headers/footers distintos para páginas pares, impares y la primera página** de cada sección.
- **Numeración de páginas**: MigraDoc calcula saltos de página, headers, footers y números de página al renderizar.
- **Gráficos de negocio**: soporte incorporado (`MigraDoc.DocumentObjectModel.Shapes.Charts`) para gráficos básicos — barras, líneas, pastel.
- **Otros**: bookmarks para enlaces, tabla de contenidos, índices, imágenes.
- **Salida dual**: el mismo documento se renderiza a PDF o a RTF.

## Limitaciones conocidas

> [!warning] PDF/A y PDF/UA no confirmados
> No se pudo confirmar con fuente oficial que PDFsharp genere PDF/A (archivado de largo plazo) ni PDF/UA (accesibilidad). Si el negocio llegara a exigirlo — archivado contable, facturación electrónica EN 16931 — la abstracción `IReportStrategy` permite sumar una estrategia con otro motor **solo para esos reportes**, sin tocar el resto. Trade-off aceptado explícitamente en [[ADR-006 - Motor de Reportes y Exportación]].

- **Gráficos básicos**: cubren barras/líneas/pastel. No hay interactividad ni ejecución de JavaScript (irrelevante para PDF impreso, pero conviene saberlo si alguien pide un gráfico muy elaborado).
- **Sin motor HTML**: no convierte HTML/CSS a PDF. El diseño se define en C#.

## Versiones y compatibilidad

| | |
|---|---|
| Paquete NuGet | `PDFsharp-MigraDoc` |
| Estable | **6.2.4** (2026-01-06) |
| Preview | 7.0.0 Preview 1 (2026-03-24), con C# 12 |
| Frameworks | net8.0, net9.0, net10.0, netstandard2.0 |
| Descargas | ~8.6 millones |
| Plataformas | Windows, Linux, macOS |

Cubre el `net8.0` actual del proyecto y la eventual migración a .NET 9/10 sin cambiar de paquete.

## Fuentes oficiales

- [GitHub — empira/PDFsharp](https://github.com/empira/PDFsharp)
- [NuGet — PDFsharp-MigraDoc](https://www.nuget.org/packages/PDFsharp-MigraDoc)
- [Documentación técnica](https://docs.pdfsharp.net/)
- [Introducción: PDFsharp vs MigraDoc](https://docs.pdfsharp.net/General/Overview/Introduction.html)
- [Tablas](https://docs.pdfsharp.net/MigraDoc/DOM/Contents/Tables.html)
- [Headers & Footers](https://docs.pdfsharp.net/MigraDoc/DOM/Contents/HeadersAndFooters.html)
- [Estructura del documento](https://docs.pdfsharp.net/MigraDoc/DOM/Document/DocumentStructure.html)

## Relaciones

- [[ADR-006 - Motor de Reportes y Exportación]]
- [[Licencias de Librerías .NET - Auditoría 2026-07]]
- [[Plan Fase 9 - Subsistema de Reportes]]
