---
title: "Sesión 2026-11-08 — Resolución P-067 (Pkcs 10.0.14)"
type: sesion
status: vigente
date: 2026-11-08
updated: 2026-11-08
autor: por-confirmar
agente: humano
rama: main
tags: [sesion, deuda-tecnica, nuget, seguridad, velopack]
summary: "La resolución se hizo justo antes de publicar la próxima release desde `main` (workflow `publish-velopack-release.yml`, versión `1.0.<run_number>`, po"
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

## Validación del ciclo de actualización remota

El commit `5239085` se pusheó a `main`, lo que disparó el workflow de publicación.

| Paso | Evidencia |
|---|---|
| Run de Actions | `42550903322` → `success` en 3m16s |
| Release | `v1.0.3`, publicado, `Latest`, tag `v1.0.3` |
| Assets | `BimboPesaje-1.0.3-full.nupkg` (74,124,419 B), `-delta.nupkg` (154,937 B), `Setup.exe`, `.msi`, `Portable.zip`, `releases.win.json`, `RELEASES` |
| Manifiesto | `releases.win.json` declara `Version 1.0.3`, SHA-256 `6710C149…9D34026` para el full |
| Cliente | instalación per-user en `%LOCALAPPDATA%\BimboPesaje` (HKCU, sin UAC) sobre la 1.0.2 local |
| Descarga | `%LOCALAPPDATA%\BimboPesaje\packages\BimboPesaje-1.0.3-full.nupkg` (74,124,419 B, paquete **completo** porque no había base) |
| Resultado | `sq.version` = `1.0.3`, `CapaUI.exe` = `1.0.3+5239085c…`, app relanzada desde `current` |

Se lanzó la app con el stub `Bimbo Pesaje Honduras.exe`; a los pocos segundos ya estaba el paquete completo en `packages`, y el proceso se reemplazó solo (el PID cambió de 17696 a 7960) sin pedir elevación. El banner de actualización no se llegó a observar visualmente porque el ciclo cerró antes de la interacción; la versión instalada es la prueba dura de que se aplicó.

Observación sobre P-067: la DLL `System.Security.Cryptography.Pkcs.dll` de la 1.0.2 instalada (build local, **sin** el pin) ya era `10.0.1226.42308`, es decir, la del runtime .NET 10; el `8.0.1` vulnerable existía en el grafo NuGet, no en el binario enviado. Con el pin, la 1.0.3 quedó con `10.0.1426.52326`. El fix corrige la superficie de auditoría (`dotnet list --vulnerable`, NU1902/NU1903) y el grafo de dependencias, no un runtime comprometido.

## Nota sobre identidad de artefacto

Existen dos artefactos distintos con el número `1.0.2`: el empaquetado localmente el 11-07 (74,091,909 B, SHA-256 `A47A304F…CC2A8C`, commit `33d9bbf`) y el publicado por Actions (74,124,427 B, SHA-256 `2598CC78…7D71C6`, commit `41bd2e0`). El updater compara números, no bytes, así que no rompió la prueba; pero para el piloto se recomienda tratar `Releases/` local como taller y el asset publicado como fuente de verdad, sin reutilizar un número de versión ya publicado.

## Separación entre validación y publicación

Con la release `v1.0.3` publicada, quedó visible un problema de disparo: `on: push` a `main` sin filtros significa que **cualquier commit** —incluida una nota de bitácora o un ajuste de skill de un agente— publica una versión descendable de ~74 MB.

Decisión aplicada:

| Archivo | Disparo |
|---|---|
| `publish-velopack-release.yml` | Lista blanca de solo código: `BimboProyecto.sln`, `CapaUI/**`, `CapaDatos/**`, `CapaDominio/**`, `CapaAplicacion4/**`, `scripts/publish-release.ps1`; más `workflow_dispatch` |
| `ci.yml` (nuevo) | Push a `main`/`master` y PRs sobre esos mismos proyectos, `BimboProyecto.Tests/**` y `.github/workflows/**` |

Así, un cambio en el propio YAML ejecuta validación (restore + build Release + 734 pruebas) sin emitir release, y la publicación solo ocurre ante cambios reales del binario o un disparo manual.

Verificación local de los comandos exactos del CI antes de pushear: `dotnet restore` → `dotnet build -c Release --no-restore` (0 errores, 0 advertencias) → `dotnet test -c Release --no-build` (734/734).

Limitación conocida: `workflow_dispatch` solo aparece en la UI cuando el archivo existe en la rama por defecto (`master`, hoy desactualizada respecto de `main`). Y el job de CI no certifica integración: las pruebas que dependen de Supabase se omiten sin `BIMBO_POSTGRES_CONNECTION_STRING`.

## Relaciones

- [[Deuda Técnica - Pendientes]]
- [[Arquitectura Actual]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[Sesión 2026-11-06 - Integración inicial de Velopack en CapaUI]]
- [[Sesión 2026-11-07 - Merge y MSI de actualizaciones Velopack]]
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
