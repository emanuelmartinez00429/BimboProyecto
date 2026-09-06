---
title: "Sesión 2026-07-26 - CRUD Completo Módulo Empleados"
tags: [sesion, bimbo, empleados, crud, supabase]
date: 2026-07-26
branch: main
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-07-26 - CRUD Completo Módulo Empleados

## Resultado

Se completó la funcionalidad CRUD del módulo de Empleados: crear, editar, y cambiar estado. El módulo pasó de ser solo lectura a ser funcional, siguiendo el patrón canónico de Productos.

## Problema

El módulo de Empleados estaba en estado "solo lectura" desde su creación (Sesión 2026-07-26 - Módulo Empleados). El `BtnGuardar_Click` era un no-op con MessageBox, `ToggleEstado` mostraba un mensaje de error, y no existían métodos de escritura en el repositorio.

## Cambios realizados

### Archivos modificados (5)

| Archivo | Cambio |
|---------|--------|
| `IEmpleadoRepository.cs` | Agregados `CreateAsync`, `UpdateAsync`, `CambiarEstadoAsync` |
| `EmpleadoCrudRepository.cs` | Implementados los 3 métodos con TryAsync + Supabase Insert/Update |
| `EmpleadoModal.xaml.cs` | Evento `Guardado` agregado, `BtnGuardar_Click` reemplazado con lógica real de validación + DTO + repo call |
| `EmpleadosView.xaml.cs` | Suscripción a `modal.Guardado` en `AbrirModalNuevo` y `AbrirModalEditar`, handler `OnEmpleadoGuardado` |
| `EmpleadosViewModel.cs` | `ToggleEstado` reemplazado con `ToggleEstadoAsync` real que llama `CambiarEstadoAsync` |

## Patrón seguido

Replicado del módulo de Productos:
1. Interface con Create/Update/CambiarEstado → `TryAsync` + `Result<T>`
2. Repository con Supabase `client.From<Model>().Insert()` / `.Set().Update()`
3. Modal con `Guardado` event → View cierra + RefrescarDatos
4. ViewModel con `[RelayCommand(CanExecute)]` + async Task

## Verificación

- `dotnet build BimboProyecto.sln` → **0 errores** (46 warnings preexistentes de nullable en CapaDatos)

## Notas

- El doble clic para editar ya existía en el XAML y code-behind — no requirió cambios
- `ToggleEstadoAsync` genera `ToggleEstadoCommand` automáticamente (CommunityToolkit strip "Async")
- No se necesitaron nuevos registros DI — `IEmpleadoRepository` ya estaba registrado

## Relaciones

- [[Módulo Empleados]] — módulo completado
- [[Módulo Productos]] — patrón canónico replicado
- [[Repositorio Base con TryAsync]] — patrón de error handling
- [[Result Pattern]] — Result<T> en todas las operaciones
