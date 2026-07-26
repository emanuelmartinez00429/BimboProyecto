---
title: "Sesión 2026-07-26 — Módulo Empleados (solo lectura)"
tags: [sesion, empleados, wip, ui]
date: 2026-07-26
branch: feat/fase7-GestióndeUsuarios
autor_cambios: Claude (Sonnet 5), dirigido por Fernando
---

# Sesión 2026-07-26 — Módulo Empleados (solo lectura)

## Pedido

Fernando pidió crear el módulo de Gestión de Empleados, pero **solo la parte visual y de lectura**: la lista, el buscador, y los modales de Ver (al seleccionar una fila) y Crear (botón Nuevo). Explícitamente **no** quiso que Guardar/Editar quedaran funcionales — necesita revisar algo del modelo de Empleados antes de permitir escritura, aunque los botones de Guardar deben seguir visibles en el modal.

## Qué se construyó

Módulo completo siguiendo el patrón establecido (Categorías/Usuarios), documentado en detalle en [[Módulo Empleados]]:

- `IEmpleadoRepository` + `EmpleadoCrudRepository` — **solo lectura** (`GetPagedAsync`, `BuscarSugerenciasAsync`, `GetPaginaDeRegistroAsync`). No se agregaron `CreateAsync`/`UpdateAsync`/`CambiarEstadoAsync` — a propósito, para que sea imposible escribir por accidente.
- `EmpleadosViewModel` + `EmpleadosView` — lista paginada, buscador `SuggestionSearchBox`, filtro Activos/Inactivos/Todos, stats.
- `EmpleadoModal` — abre y muestra datos reales tanto en Ver como en Crear. `BtnGuardar_Click` es un no-op deliberado (`MessageBox` informativo, sin llamar al repositorio). `ToggleEstadoCommand` en el ViewModel, igual.
- Conectado a la navegación existente: `Routes.Empleados` ya estaba definida (la usa el buscador universal) pero apuntaba a un placeholder `ConstructionVM`; ahora apunta al módulo real.

## Problema encontrado y resuelto: colisión de namespace

Al crear `namespace CapaDatos.Repositories.Empleados`, el build rompió con `CS0118` en dos archivos **que no toqué**: `EmpleadoRepository.cs` (repositorio del buscador universal) y `UsuarioRepository.cs`. Causa: ambos usan el modelo `CapaDatos.Modelados.Usuarios.Empleados` sin calificar (`using CapaDatos.Modelados.Usuarios;`), y C# resuelve un namespace hermano con el mismo nombre **antes** que un `using` — el compilador interpretaba `Empleados` como el namespace nuevo, no como el tipo.

**Fix:** namespace del repositorio nuevo renombrado a `CapaDatos.Repositories.GestionEmpleados` (la carpeta física sigue siendo `Empleados/`). Cero cambios en los archivos que rompieron — el fix fue enteramente de mi lado.

## Verificación

- `dotnet build BimboProyecto.sln` → 0 errores, 0 advertencias.
- **No se pudo probar en runtime** (requiere login contra Supabase, no automatizado por política de seguridad). Pendiente que Fernando confirme: la lista carga, el buscador despliega sugerencias, Ver/Crear muestran el modal con los datos correctos, y Guardar/Cambiar Estado NO hacen nada (solo un aviso).

## Cómo habilitar la escritura después

Documentado en [[Módulo Empleados]] — agregar los 3 métodos de escritura a la interfaz + implementación, y reemplazar el `MessageBox` de `BtnGuardar_Click` por la llamada real (usar `CategoriaModal.xaml.cs` como plantilla).

## Nota de proceso

Este trabajo se hizo **directo en la carpeta principal** (`C:\Users\fbara\...\BimboProyecto`, la que Fernando tiene abierta en VS Code en esta misma rama) en vez del worktree separado de Claude — commit local hecho, **sin push**; Fernando lo sube cuando quiera.

## Relaciones

- [[Módulo Empleados]]
- [[Módulo Productos]] — patrón de referencia
- [[Arquitectura Actual]]
