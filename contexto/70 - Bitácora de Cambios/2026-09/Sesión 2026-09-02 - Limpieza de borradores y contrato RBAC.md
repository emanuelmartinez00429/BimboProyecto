---
title: "Sesión 2026-09-02 - Limpieza de borradores y contrato RBAC"
tags: [sesion, bimbo, rbac, supabase, pruebas]
date: 2026-09-02
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex
---

# Sesión 2026-09-02 - Limpieza de borradores y contrato RBAC

## Objetivo

Retirar SQL y parches provisionales rastreados y evitar divergencias entre el contrato RBAC tipado, las rutas/RPC y `public.acciones`.

## Trabajo realizado

- Se eliminaron del versionado `out.sql`, `diff.patch`, `temp_0.sql` a `temp_5.sql` y `20260902140000_hotfix_notificaciones_seguridad_rbac_supermin.sql`.
- `.gitignore` bloquea esos borradores; la prueba de migraciones exige el formato nuevo `yyyyMMddHHmmss_nombre.sql` y enumera de forma explícita las tres migraciones históricas que no se pueden renombrar sin alterar historial.
- `PermisoCatalogo` ahora deriva códigos, etiquetas y resolución inversa de una sola lista de `DefinicionPermiso`. La cobertura exige un valor enum, código y etiqueta únicos.
- `SesionPermisos.Tiene` niega y registra un permiso tipado sin definición, en lugar de propagar una excepción durante la sesión.
- Se agregaron pruebas locales y de integración opcional. Al establecer `BIMBO_POSTGRES_CONNECTION_STRING`, validan los códigos frente a `public.acciones`, su unicidad y la definición remota de `cambiar_estado_usuario_seguro`.

## Contrato de estado de usuario

La migración legible vigente usa `USUARIOS_MODIFICAR` para activar y `USUARIOS_ELIMINAR` para desactivar; `USUARIOS_ACTIVAR` no pertenece al catálogo. La prueba local bloquea una regresión de ese patrón.

## Verificación pendiente

- La prueba remota no se ejecutó en esta sesión porque no había conexión de PostgreSQL de integración configurada; debe correrse con `BIMBO_POSTGRES_CONNECTION_STRING` antes de declarar sincronizado el entorno remoto.
- Sigue pendiente QA autenticado: Administrador debe recibir `CATEGORIAS_CONSULTAR`; un rol no administrativo solo al tener la acción asignada y tras renovar sesión.

## Relaciones

- [[Módulo Usuarios]]
- [[Arquitectura Actual]]
- [[ADR-024 - Rol Administrador inmutable con acceso total]]
- [[Sesión 2026-09-02 - Auditoría Notificaciones y fix permiso fantasma USUARIOS_ACTIVAR]]
