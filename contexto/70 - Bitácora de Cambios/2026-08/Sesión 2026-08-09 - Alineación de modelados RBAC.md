---
title: "Sesión 2026-08-09 — Alineación de modelados RBAC"
tags:
  - sesion
  - bimbo
  - rbac
  - modelados
date: 2026-08-09
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex
---

# Sesión 2026-08-09 — Alineación de modelados RBAC

> [!success] Resultado
> Los cuatro modelos de persistencia del RBAC quedaron alineados con el esquema confirmado para `modulos`, `acciones`, `roles` y `acciones_roles`. La solución compila con 0 errores.

---

## Problema / motivo

La vault documentaba `Accion`, `AccionRol` y `Modulo`, mientras el repositorio contenía también `Roles`. Los modelos existentes permitían cargar permisos, pero no representaban las columnas de auditoría del esquema y todavía suponían que `roles` tenía `id_estado`.

## Cambios aplicados

- `CapaDatos/Modelados/Usuarios/Modulo.cs`: agregado `created_at` nullable.
- `CapaDatos/Modelados/Usuarios/Accion.cs`: agregado `created_at` nullable.
- `CapaDatos/Modelados/Usuarios/Roles.cs`: agregados `created_at` y `updated_at`; eliminado el mapeo inexistente `id_estado`; inicializado `nombreRol`.
- `CapaDatos/Modelados/Usuarios/AccionRol.cs`: agregados `created_at` y `updated_at` nullable; conservado `id_estado`.
- `CapaAplicacion4/Usuarios/Dtos/RolDto.cs`: eliminado `IdEstado`, ya que no pertenece a `roles`.
- `CapaDatos/Repositories/Usuarios/RolRepository.cs`: actualizado el mapeo al DTO.
- `contexto/40 - Proyecto Bimbo/Módulo Usuarios.md`: registrada la cobertura de los cuatro modelos.

No se añadieron paquetes: se reutilizaron `BaseModel`, `[Table]`, `[PrimaryKey]` y `[Column]` de Supabase.Postgrest, siguiendo los modelos existentes.

## Verificación

- `dotnet build BimboProyecto.sln`
- Resultado: 0 errores y 46 warnings preexistentes de nulabilidad y acceso a archivos durante la compilación.
- `git diff --check`: sin errores de espacios.

## Lo que NO cambió

- No se modificó el esquema de Supabase ni se ejecutaron migraciones.
- No se implementó todavía la aplicación de permisos en XAML.
- No se cambió la carga de permisos de `UsuarioSesionService`.
- No se modificó el cambio previo existente en `contexto/.obsidian/graph.json`.
- No se creó ADR porque se documentó correspondencia con un esquema proporcionado, no una nueva decisión arquitectónica.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Usuarios]]
- [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]]
