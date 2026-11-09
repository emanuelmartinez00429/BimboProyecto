---
title: "Sesión 2026-11-08 — Resolución P-067 (Pkcs 10.0.14)"
tags: [sesion, deuda-tecnica, nuget, seguridad, velopack]
date: 2026-11-08
branch: main
autor_cambios: opencode
---

# Sesión 2026-11-08 — Resolución P-067 (Pkcs 10.0.14)

## Objetivo

Cerrar [[Deuda Técnica - Pendientes#P-067|P-067]] antes de publicar la próxima release de Velopack desde `main`.

## Diagnóstico

El paquete vulnerable no tiene `PackageReference` directo: llega por dos padres, y ambos lo fijan en la misma versión.

| Padre | Dependencia declarada |
|---|---|
| `PDFsharp` 6.2.4 | `System.Security.Cryptography.Pkcs` 8.0.1 |
| `PDFsharp-MigraDoc` 6.2.4 | `System.Security.Cryptography.Pkcs` 8.0.1 |

La solución que la nota dejaba como principal —"subir el padre de versión"— **no funciona**: se inspeccionó el `.nuspec` de `PDFsharp` **6.5.0** (última estable al 2026-11-08) y sigue declarando `8.0.1` para `net8.0`, `net9.0`, `net10.0` y `NETStandard2.0`.

Rangos corregidos según los datos de vulnerabilidades de NuGet (`api.nuget.org/v3-vulnerabilities`, snapshot 2026-11-07), para `GHSA-846f-f764-76mx` y `GHSA-2fmv-rv2c-59x7`:

- `>= 8.0.2`
- `>= 9.0.21`
- `>= 10.0.13`

Se eligió **10.0.14** (última publicada) porque el TFM es `net10.0` y porque .NET 8 sale de soporte el 2026-11-10.

## Cambio aplicado

`CapaDatos/CapaDatos.csproj`: `PackageReference` explícito a `System.Security.Cryptography.Pkcs` 10.0.14, con comentario del motivo. Por la regla de resolución de NuGet (referencia directa gana a la transitiva) el pin pisa el 8.0.1 en todos los proyectos que consumen `CapaDatos` por proyecto: `CapaUI` y `BimboProyecto.Tests`.

## Pruebas y validaciones

- `dotnet build BimboProyecto.sln` → **0 errores, 0 advertencias**.
- `dotnet list package --include-transitive --vulnerable` → **"no tiene paquetes vulnerables"** en `CapaDatos`, `BimboProyecto.Tests` y `CapaUI`.
- `CapaDatos/obj/project.assets.json` → `System.Security.Cryptography.Pkcs/10.0.14` (ya no aparece 8.0.1).
- `dotnet test BimboProyecto.Tests` → **734/734** aprobadas. Incluye `ReportStrategyTests.Pdf_GeneraDocumentoConFirmaValida`, que ejecuta PDFsharp en runtime y valida la cabecera `%PDF`: es la evidencia de compatibilidad binaria del salto 8.0.1 → 10.0.14.

## Contexto: prueba del actualizador

La resolución se hizo justo antes de publicar la próxima release desde `main` (workflow `publish-velopack-release.yml`, versión `1.0.<run_number>`, por lo que el próximo push produce `1.0.3`).

Hallazgos de la inspección, relevantes para el piloto:

- El paquete de Velopack se generó con `--instLocation Either`, así que el MSI permite elegir **"solo para usted"** (`%LOCALAPPDATA%`, sin elevación) o **"para todos"** (`Program Files`, HKLM, requiere admin).
- La actualización silenciosa solo funciona sin UAC en la instalación per-user. En per-machine, `WaitExitThenApplyUpdates(..., silent: true)` falla sin mostrar nada si el usuario no está elevado, y el único rastro queda en `%APPDATA%\BimboPesaje\Logs\app-*.log`.
- Los deltas se aplican sobre el paquete base guardado en `%LOCALAPPDATA%\{packId}\packages`; si no está, el cliente baja el paquete completo.

## Relaciones

- [[Deuda Técnica - Pendientes]]
- [[Arquitectura Actual]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[Sesión 2026-11-06 - Integración inicial de Velopack en CapaUI]]
- [[Sesión 2026-11-07 - Merge y MSI de actualizaciones Velopack]]
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
