---
title: "Sesión 2026-08-09 — Implementación RBAC visual y gestión de roles"
tags:
  - sesion
  - bimbo
  - rbac
  - seguridad
date: 2026-08-09
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex
---

# Sesión 2026-08-09 — Implementación RBAC visual y gestión de roles

> [!success] Resultado
> El RBAC cargado desde Supabase ya se refleja en la navegación y en las acciones visuales del cliente WPF. También quedó implementada la pantalla para consultar y modificar las acciones asignadas a cada rol.

## Objetivo

Continuar la implementación existente de RBAC tanto en la interfaz como en la capa de acceso a datos, reutilizando `IUsuarioSesionService`, `SesionPermisos`, el enum `Permiso` y las tablas ya modeladas.

## Trabajo realizado

- Se reemplazó la ruta temporal de Roles por `RolesView` y `RolesViewModel`.
- Se agregó `IRolPermisoRepository`/`RolPermisoRepository` para consultar el catálogo, leer asignaciones y activar o desactivar filas de `acciones_roles`.
- Se agregó `ModuloAccionesDto` para transportar el catálogo agrupable sin exponer modelos de PostgREST a la UI.
- Se registraron repositorio y ViewModel en el contenedor de dependencias.
- El menú principal oculta módulos y submódulos según sus acciones disponibles.
- `MainViewModel` rechaza navegación directa o programática hacia rutas no autorizadas.
- Los botones de crear, modificar y eliminar quedaron protegidos con `PermisoBehavior.Requiere`.
- Los ViewModels y los manejadores code-behind repiten la validación antes de comandos, modales y eliminaciones.
- `PermisoBehavior` ahora falla cerrado ante una configuración inválida y la registra en Serilog.

## Asociaciones funcionales

- `modulos` agrupa las acciones que se presentan en la pantalla de Roles.
- `acciones` define el contrato nominal que coincide con `Permiso`.
- `acciones_roles` asocia una acción con un rol; `id_estado` indica si la asociación está activa.
- `roles` se asocia a los usuarios y determina qué acciones carga `UsuarioSesionService` al iniciar sesión.
- `SesionPermisos` conecta la sesión Singleton con XAML y con las verificaciones imperativas.

## Pruebas y validaciones

- `dotnet build BimboProyecto.sln`: 0 errores, 44 warnings preexistentes de nulabilidad.
- Auditoría inicial: 22 valores técnicos en `Permiso`, 22 nombres referenciados desde XAML y 0 referencias inválidas respecto de ese contrato provisional.
- `git diff --check`: sin errores de espacios.

## Límites verificados

- No se cambió el esquema de Supabase ni se ejecutaron migraciones.
- No se modificaron políticas RLS; su auditoría continúa siendo necesaria para confirmar la autorización ante clientes directos.
- Los cambios de asignaciones requieren volver a iniciar sesión para recargar permisos.
- No se modificó el cambio previo del usuario en `contexto/.obsidian/graph.json`.

## Decisiones

No se creó un ADR nuevo. La implementación aplica [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]] y [[ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML]] sin cambiar la arquitectura decidida.

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Usuarios]]
- [[Sesión 2026-08-09 - Alineación de modelados RBAC]]
- [[Roadmap de Seguridad y Calidad]]

## Corrección posterior — nomenclatura real de acciones

Se confirmó que `acciones.nombre_accion` contiene 28 nombres descriptivos con espacios, ordenados por `id_accion` del 1 al 28. Se reemplazó el contrato provisional (`Productos_Ver`, etc.) por un catálogo completo que conserva esos valores literalmente.

- `Permiso` contiene 28 identificadores tipados válidos para C#.
- `PermisoCatalogo` asocia cada identificador con su nombre exacto de Supabase.
- `SesionPermisos` compara mediante `NombreBaseDatos()`, no mediante `enum.ToString()`.
- `PermisoBehavior.Requiere` utiliza directamente nombres como `Consultar Producto` y `Modificar Configuración`.
- Categorías y administración de Roles se asocian a `Modificar Configuración`, porque el catálogo real no contiene acciones específicas de categoría o rol.
- Desactivar usuarios/empleados usa `Eliminar Usuario`/`Eliminar Empleado`; cancelar una descarga usa `Cancelar Pesaje`.

Validación posterior:

- Catálogo: 28 acciones.
- Acciones distintas referenciadas desde XAML: 28.
- Referencias inválidas: 0.
- `dotnet build BimboProyecto.sln --no-restore`: 0 errores y 0 advertencias.
