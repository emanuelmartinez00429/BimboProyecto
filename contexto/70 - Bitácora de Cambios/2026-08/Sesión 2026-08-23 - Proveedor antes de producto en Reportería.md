---
title: "Sesión 2026-08-23 — Proveedor antes de producto en Reportería"
tags: [sesion, reporteria, catalogos-encadenados, wpf]
date: 2026-08-23
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-08-23 — Proveedor antes de producto en Reportería

## Objetivo

Corregir el orden y la dependencia de los filtros del reporte de entrada de
materia prima: primero proveedor, después uno de sus productos y al final el
rango de fechas.

## Trabajo realizado

- El formulario muestra Proveedor → Producto → Desde → Hasta.
- La lupa de Producto queda deshabilitada hasta seleccionar un proveedor.
- El selector de productos recibe el ID del proveedor y reutiliza el puente
  `productos.id_fabricante → fabricante.id_proveedor` del catálogo compartido.
- Cambiar o limpiar el proveedor elimina el producto anterior y la vista previa.
- No se modificaron esquema, RLS, RPC ni contratos de reportes.

## Archivos modificados

- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Reporteria/ReporteriaViewModel.cs`
- `contexto/40 - Proyecto Bimbo/Módulo Reportería.md`

## Decisiones

Se reutilizó `Catalogos.Productos(repo, idProveedor)` en lugar de crear otra
consulta o una RPC. Incluso cuando exista un solo producto, el usuario lo
confirma mediante la lupa.

## Pruebas y validaciones

- `dotnet build BimboProyecto.sln --no-restore --no-incremental` — 0 errores y 60
  advertencias preexistentes; `NU1900` no pudo consultar el índice de
  vulnerabilidades de NuGet.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-build` — 113
  pruebas superadas, 0 fallidas y 0 omitidas.
- `git diff --check` — correcto; solo avisos informativos LF/CRLF.

## Pendientes

- Prueba visual autenticada del encadenamiento y del estado vacío.

## Relaciones

- [[Módulo Reportería]]
- [[Selector de Catálogo - Selector genérico y multiselección]]
- [[Sesión 2026-08-23 - Selectores compactos y paginados en Reportería]]
