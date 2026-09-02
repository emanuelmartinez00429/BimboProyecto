---
title: "Sesión 2026-09-02 - Administrador inmutable con acceso total"
tags: [sesion, bimbo, roles, rbac, supabase]
date: 2026-09-02
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-09-02 - Administrador inmutable con acceso total

> [!success] Resultado
> El rol Administrador quedó protegido contra cualquier edición y conserva todas las acciones actuales y futuras en UI y Supabase.

---

## Problema / motivo

La protección anterior cubría siete acciones administrativas, pero permitía retirar permisos operativos. El requisito vigente establece que el rol de sistema siempre debe acceder a todos los módulos y no debe ser editable por ninguna sesión.

## Cambios aplicados

- `RolesViewModel` protege todas las acciones cuando el rol seleccionado tiene `EsSistema`, impide guardar y deshabilita controles individuales, por módulo y masivos.
- `RolesView` y `RolesResources` explican la inmutabilidad total y muestran cada acción obligatoria con candado.
- Supabase aplicó la migración `20260902032735_hacer_inmutable_rol_administrador_todos_permisos`: backfill de asignaciones, bloqueo de INSERT/UPDATE/DELETE inválidos en `acciones_roles`, rechazo de la RPC para el rol de sistema y autoasignación de acciones nuevas.
- ADR-024 reemplaza el núcleo parcial de ADR-023.

## Verificación

- Estado remoto: 34 acciones en catálogo, 34 asignaciones activas de Administrador y 0 faltantes.
- Prueba transaccional con rollback: desactivar y eliminar permisos del Administrador fueron rechazados; la RPC rechazó reemplazarlos; una acción temporal se autoasignó; un rol no-sistema continuó editable; renombrar Administrador fue rechazado.
- Asesores de seguridad ejecutados: no aparecieron hallazgos nuevos para las funciones de esta migración. Permanecen advertencias históricas ajenas sobre exposición, `search_path`, funciones RPC intencionalmente ejecutables y configuración de Auth.
- Compilación previa al cierre documental: 0 errores; las advertencias nullable existentes pertenecen a otros modelados y repositorios.
- Validación visual autenticada pendiente para confirmar candados, mensajes y controles deshabilitados en WPF.

## Lo que NO cambió

No se alteraron los nombres del catálogo de acciones, los permisos de roles no-sistema, la protección del usuario contra cambiar su propio rol/estado ni los flujos funcionales de Empleados, Usuarios o Reportería.

---

## Relaciones

- [[ADR-024 - Rol Administrador inmutable con acceso total]]
- [[ADR-023 - Rol Administrador de sistema con nucleo de permisos protegido]]
- [[Módulo Usuarios]]
- [[Arquitectura Actual]]
