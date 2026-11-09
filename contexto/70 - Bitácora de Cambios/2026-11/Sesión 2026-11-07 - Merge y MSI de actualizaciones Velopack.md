---
title: "Sesión 2026-11-07 — Merge y MSI de actualizaciones Velopack"
type: sesion
status: vigente
date: 2026-11-07
updated: 2026-11-07
autor: por-confirmar
agente: humano
rama: main
tags: [sesion, velopack, despliegue, msi, github-actions]
summary: "- Validar el MSI con el compañero y obtener el texto/código exacto del error previo."
---

# Sesión 2026-11-07 — Merge y MSI de actualizaciones Velopack

## Objetivo

Integrar en `main` los cambios de actualización remota y generar un instalador MSI convencional.

## Trabajo realizado

- Se integró `pruebas-remote-update` en `main` mediante merge local. La rama local de trabajo fue eliminada después de integrarse; la rama remota todavía puede existir.
- Se añadió `.github/workflows/publish-velopack-release.yml`: cada push a `main` compila y publica un Release usando `GITHUB_TOKEN`; la versión del paquete usa `1.0.<run_number>`.
- Se actualizó `scripts/publish-release.ps1` para pasar la versión al publish y empaquetar Setup.exe y MSI con alcance `Either`.
- Se generó localmente el paquete 1.0.2. El MSI está en `Releases/BimboPesaje-win.msi`; el Setup está en `Releases/BimboPesaje-win-Setup.exe`.

## Pruebas y validaciones

- `git diff --check` no reportó errores antes del merge.
- `dotnet publish` Release `win-x64` y `vpk pack` 1.0.2 finalizaron correctamente; Velopack creó MSI, Setup, paquete completo, delta y manifiesto.
- No se pudo descargar el Release anterior por restricción de red del entorno; el empaquetado local se ejecutó con `-SkipDownload` y no se publicó.
- NuGet no pudo acceder a su índice de vulnerabilidades (`NU1900`). Los artefactos no tienen firma digital.
- No se instaló el MSI ni se verificó el flujo en un equipo limpio. La ejecución remota de GitHub Actions queda pendiente.

## Problemas y pendientes

- Validar el MSI con el compañero y obtener el texto/código exacto del error previo.
- Probar una instalación y actualización de extremo a extremo tras confirmar la ejecución de Actions.
- P-067 permanece pendiente por decisión del usuario.

## Relaciones

- [[Arquitectura Actual]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]]
