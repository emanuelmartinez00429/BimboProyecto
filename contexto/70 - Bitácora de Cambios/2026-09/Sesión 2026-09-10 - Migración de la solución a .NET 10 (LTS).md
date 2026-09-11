---
title: "Sesión 2026-09-10 — Migración de la solución a .NET 10 (LTS)"
date: 2026-09-10
tags:
  - bitacora
  - sesion
  - net10
  - arquitectura
aliases:
  - Migración .NET 10
---

# Sesión 2026-09-10 — Migración de la solución a .NET 10 (LTS)

## Resumen

Se incorporó y documentó formalmente la migración de los proyectos activos de `BimboProyecto.sln` hacia **.NET 10** (`net10.0-windows` / `net10.0`). Esta decisión arquitectónica se anticipa al fin del soporte oficial (EOL) de .NET 8 (noviembre de 2026), garantizando soporte, parches de seguridad y compatibilidad de dependencias a largo plazo.

## Proyectos Migrados

| Proyecto | TargetFramework previo | TargetFramework nuevo | Tipo |
|---|---|---|---|
| `CapaUI` | `net8.0-windows` | `net10.0-windows` | WPF ejecutable |
| `CapaAplicacion4` | `net8.0` | `net10.0` | Biblioteca de clases |
| `CapaDatos` | `net8.0` | `net10.0` | Biblioteca de clases |
| `CapaDominio` | `net8.0` | `net10.0` | Biblioteca de clases |
| `BimboProyecto.Tests` | `net8.0` | `net10.0` | xUnit suite |
| `ServicioConexión` | `net8.0-windows` | `net10.0-windows` | WinForms biblioteca |

*Nota: Los proyectos huérfanos/fuera de la solución (`SearchTest`, `ServicioConexión/CapaConexión`) se mantienen fuera del alcance de la migración activa.*

## Compatibilidad de Paquetes NuGet

Se verificó que los paquetes clave de la solución (`Serilog 4.3.1`, `PDFsharp 6.2.4`, `CommunityToolkit.Mvvm 8.4.0`, `ZiggyCreatures.FusionCache 2.0.2`, `Microsoft.Extensions.DependencyInjection 8.0.1`) resuelven de forma limpia bajo `net10.0` directamente o a través de `netstandard2.0`/`netstandard2.1` sin conflictos de versiones transitivas ni requerir actualizaciones disruptivas.

## Verificación

- `dotnet build BimboProyecto.sln`: **0 errores** de compilación.
- `dotnet test`: **344/344 pruebas superadas** exitosamente en `BimboProyecto.Tests.dll (net10.0)`.
- Documentación de la bóveda sincronizada: `Arquitectura Actual.md`, `CLAUDE.md`, `AGENTS.md`, `Plan de CI-CD`, `Plan Offline-First`, `Plan de Tests Unitarios` y `Convenciones de UI`.

## Relaciones

- [[Arquitectura Actual]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[Deuda Técnica - Pendientes]]
