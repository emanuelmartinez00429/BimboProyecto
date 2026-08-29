---
title: "Sesión 2026-07-26 — Módulo Bitácora (auditoría, solo lectura)"
type: sesion
status: vigente
tags:
  - sesion
  - bitacora
  - auditoria
  - ui
date: 2026-07-26
updated: 2026-07-26
summary: "Fernando pidió la parte visual de Bitácora, pero primero verificar si la tabla existía en la base. Verificado vía MCP de Supabase: public.bitacora existe, RLS…"
scope: []
symbols:
  - BitacoraCrudRepository
  - BitacoraModel
  - BitacoraVM
  - BitacoraView
  - BitacoraViewModel
  - ConstructionVM
  - DatePicker
  - IBitacoraRepository
  - SuggestionSearchBox
  - Usuarios
branch: feat/fase7-GestióndeUsuarios
autor_cambios: "Claude (Sonnet 5), dirigido por Fernando"
---

# Sesión 2026-07-26 — Módulo Bitácora (auditoría, solo lectura)

## Pedido y verificación previa

Fernando pidió la parte visual de Bitácora, pero **primero verificar si la tabla existía en la base**. Verificado vía MCP de Supabase: `public.bitacora` existe, RLS habilitado, con 6 registros reales desde 2026-05-12. Cero código C# para ella hasta esta sesión. Con eso confirmado, se planificó el alcance con él.

## Decisiones tomadas con Fernando (antes de escribir código)

| Tema | Decisión |
|---|---|
| Alcance | Lista + filtros, **sin modal de detalle** — es un log de auditoría de solo lectura |
| Filtros | Usuario + Módulo + Acción + rango Desde/Hasta, todo server-side (la tabla crecerá a miles de registros) |
| Columna Usuario | `alias_usuario` (un solo join), no nombre de empleado |
| Buscador de texto | Sí, `SuggestionSearchBox` como los demás módulos |
| Chips de stats | Solo TOTAL (no hay concepto de activo/inactivo) |

Se dejaron **fuera** de los filtros `tabla_afectada` e `id_registro_afectado`: los datos existentes tienen calidad dispareja (ej. el literal `'Sin registro'` con comillas incluidas dentro del string), así que no son criterio confiable de búsqueda todavía. Documentado en [[Módulo Bitácora]].

## Qué se construyó

Detalle completo en [[Módulo Bitácora]]. Resumen:

- `Bitacora.cs` (modelo Supabase) con embeds `usuarios`/`acciones`/`modulos` — verificados contra la BD antes de dar por bueno el mapeo (los tres joins resuelven a nombres legibles).
- `IBitacoraRepository` + `BitacoraCrudRepository`: paginado DESC por `fecha_hora`, sugerencias, y tres lookups para poblar los dropdowns de filtro.
- `BitacoraViewModel` + `BitacoraView`: filtros combinables con cascada Módulo→Acción, `DatePicker` (primer uso en el proyecto), paginación, chip TOTAL, sin ningún botón de acción ni modal.
- `Routes.Bitacora` ya existía apuntando a un placeholder `ConstructionVM` → ahora apunta a `BitacoraVM` real.

## Tropiezos y cómo se resolvieron

**1. Colisión de namespace (otra vez).** `namespace CapaDatos.Repositories.Bitacora` + `using CapaDatos.Modelados.Usuarios;` rompió con `CS0118` al referenciar `Usuarios` sin calificar — el namespace hermano `CapaDatos.Repositories.Usuarios` gana la resolución sobre el `using`. Es el mismo problema que apareció con Empleados en la sesión anterior, pero **acá se resolvió distinto**: con aliases (`BitacoraModel`, `UsuariosModel`) en vez de renombrar el namespace, que es lo que ya hacía `UsuarioRepository.cs`. Ambas soluciones son válidas; la de aliases es más localizada.

**2. Advertencias de build "fantasma".** El primer build reportó `0 Advertencia(s)` y el siguiente `50` — no había regresión: el build incremental no recompilaba `CapaDatos`, así que ocultaba advertencias **preexistentes** (CS8618 en los modelos de Productos, Roles, Empleados, etc.). Con `--no-incremental` se ven todas. De esas 50, **3 eran mías** (los embeds de `Bitacora.cs`); se corrigieron marcándolos nullable, que además es lo correcto porque `id_accion`/`id_modulo` son nullable en BD y el Select de conteo no trae embeds. Las otras 47 son deuda preexistente, no se tocaron.

## Verificación

- `dotnet build BimboProyecto.sln --no-incremental` → 0 errores, 0 advertencias nuevas atribuibles a este módulo.
- Embeds verificados por SQL directo contra Supabase antes de escribir la UI.
- **No se pudo probar en runtime** (requiere login, no automatizado). Pendiente que Fernando confirme: lista ordenada por fecha descendente, los 4 filtros funcionando combinados, la cascada Módulo→Acción, el buscador, y que no haya ningún botón de acción ni modal.

## Nota de proceso

Trabajo hecho **directo en la carpeta principal** (la que Fernando tiene abierta en VS Code, rama `feat/fase7-GestióndeUsuarios`), commit local, **sin push** — Fernando sube cuando quiera.

## Relaciones

- [[Módulo Bitácora]]
- [[Módulo Empleados]] — mismo tropiezo de namespace, resuelto renombrando en vez de con aliases
- [[Arquitectura Actual]]
