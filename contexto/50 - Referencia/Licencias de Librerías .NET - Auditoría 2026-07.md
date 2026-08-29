---
title: Licencias de Librerías .NET — Auditoría 2026-07
type: referencia
status: vigente
tags:
  - referencia
  - licencias
  - gobernanza
  - nuget
  - legal
date: 2026-07-26
updated: 2026-07-26
summary: Durante la investigación del motor de reportes (ADR-006 - Motor de Reportes y Exportación) se detectó que un informe técnico recomendaba QuestPDF con licencia…
scope: []
symbols:
  - ClosedXML
  - MediatR
  - Serilog
lifecycle: verified
---

# Licencias de Librerías .NET — Auditoría 2026-07

> [!danger] Regla de gobernanza del proyecto
> **Ninguna dependencia NuGet nueva puede tener umbral de ingresos ni restricción por cotización en bolsa.** El ERP se despliega en Bimbo Honduras, filial de Grupo Bimbo (empresa que cotiza en la BMV). Las licencias tipo "gratis para empresas pequeñas" **no aplican** a este proyecto, aunque el código las acepte sin error.

## Por qué existe esta nota

Durante la investigación del motor de reportes ([[ADR-006 - Motor de Reportes y Exportación]]) se detectó que un informe técnico recomendaba QuestPDF con licencia Community — una licencia que **este proyecto no puede usar legalmente**. El error era invisible: el código compila, corre y no muestra advertencia alguna.

Esta nota fija el criterio para que no vuelva a pasar.

## Los tres tipos de restricción a vigilar

1. **Umbral de ingresos** (ej. QuestPDF: < USD 1M). Ojo: se suele medir **consolidado entre entidades bajo control común** — una filial se evalúa por la facturación de la matriz, no la propia.
2. **Exclusión de empresas que cotizan en bolsa.** Aunque la facturación fuera baja, cotizar descalifica.
3. **Licencia por desarrollador** (ej. EPPlus, DevExpress). Cada persona del equipo necesita su licencia; no se comparten.

## Tabla de licencias verificadas

Verificado contra NuGet, GitHub y sitios oficiales el **2026-07-26**.

### ✅ Aptas — sin restricciones para este proyecto

| Librería | Licencia | Estado | Uso |
|---|---|---|---|
| `PDFsharp-MigraDoc` 6.2.4 | **MIT** | Activo (2026-01-06) | Motor PDF — Fase 9 |
| `ClosedXML` 0.105.0 | **MIT** | Activo (2025-05) | Excel con formato — Fase 9 |
| `DocumentFormat.OpenXml` 3.x | **MIT** (Microsoft / .NET Foundation) | Activo | Excel masivo SAX — Fase 9 |
| `ZXing.Net` 0.16.11 | **Apache-2.0** | Activo | Códigos de barras / QR — Fase 9 |
| `CommunityToolkit.Mvvm` 8.4.0 | MIT | Activo | MVVM — en uso |
| `MediatR` 12.4.1 | Apache-2.0 | Activo | CQRS — en uso |
| `Serilog` 4.3.1 | Apache-2.0 | Activo | Logging — en uso |
| `Supabase` 1.1.1 | MIT | Activo | Backend — en uso |

> Apache-2.0 permite uso comercial; solo exige conservar el aviso de copyright y la nota de licencia.

### ❌ Descartadas — restricción incompatible

| Librería | Licencia | Por qué no aplica |
|---|---|---|
| **QuestPDF** (Community) | Community / dual | Umbral < USD 1M **consolidado**; excluye explícitamente *publicly traded companies* |
| **EPPlus** (v5+) | Polyform Noncommercial | Uso comercial exige licencia de pago **por desarrollador** |
| **iText 8** | AGPLv3 / comercial | AGPLv3 obliga a liberar el código fuente de la aplicación completa |
| **IronPDF** | Comercial | Por desarrollador y por servidor, sin versión gratuita de producción |
| **DevExpress / FastReport / Stimulsoft / Telerik / Syncfusion** | Comerciales | Suscripción por desarrollador |

## Cómo auditar antes de agregar una dependencia

1. Abrir la página del paquete en nuget.org y leer el campo **License** (no confiar en blogs ni en resúmenes de IA).
2. Si dice "Community", "Free for small business", "Noncommercial" o similar → **leer las condiciones completas en el sitio del fabricante**, buscando específicamente umbral de ingresos y exclusión de empresas públicas.
3. Si es MIT, Apache-2.0, BSD o MS-PL → apta sin más trámite.
4. Ejecutar la verificación de vulnerabilidades:

```bash
dotnet list package --vulnerable --include-transitive
```

## Relaciones

- [[ADR-006 - Motor de Reportes y Exportación]] — decisión donde se detectó el problema
- [[PDFsharp MigraDoc - Referencia]]
- [[Plan de Seguridad - Roadmap 10-10]] — gobernanza de dependencias
