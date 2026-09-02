---
title: "ADR-024 - Rol Administrador inmutable con acceso total"
tags: [bimbo, arquitectura, seguridad, rbac, auditoria]
date: 2026-09-02
estado: aceptado
---

# ADR-024 - Rol Administrador inmutable con acceso total

## Contexto

El rol marcado con `roles.es_sistema = true` representa al Administrador global. El núcleo parcial definido en ADR-023 evitaba perder la administración de roles y usuarios, pero todavía permitía retirar permisos operativos. Eso contradice el requisito vigente de que Administrador tenga acceso permanente a todos los módulos.

## Decisión

El rol Administrador es completamente inmutable para cualquier sesión:

- no se puede renombrar, desactivar, eliminar ni perder `es_sistema`;
- no se puede retirar, desactivar ni reasignar ninguna de sus acciones;
- toda acción nueva se le asigna automáticamente y activa mediante un trigger sobre `public.acciones`;
- `reemplazar_permisos_rol_seguro` rechaza cualquier solicitud dirigida al rol de sistema, aunque incluya el catálogo completo;
- la UI conserva el catálogo visible, marca todas las acciones con candado y deshabilita edición individual y masiva.

La garantía se identifica por `es_sistema`, no por nombre ni ID. Las sesiones abiertas deben renovarse para recargar permisos incorporados después del login.

## Alternativas consideradas

- Proteger solamente un núcleo administrativo: no garantiza acceso a todos los módulos.
- Bloquear únicamente a una cuenta Administrador: otro rol privilegiado todavía podría modificar el rol de sistema.
- Proteger solo la UI: una llamada directa a la RPC o una escritura privilegiada podría omitir la restricción visual.
- Exigir asignación manual al crear acciones: permite que una migración incompleta deje al Administrador sin acceso al módulo nuevo.

## Consecuencias

- Administrador conserva todas las acciones actuales y futuras.
- Los demás roles continúan configurables mediante el flujo auditado existente.
- Cambiar el alcance del Administrador requiere una nueva decisión arquitectónica y una migración explícita; no puede hacerse desde la aplicación.
- El trigger de `acciones_roles` y la RPC constituyen la frontera de integridad, mientras la UI comunica el estado inmutable.

## Riesgos

- Una acción creada durante una migración se asigna en base de datos inmediatamente, pero las sesiones ya abiertas y el caché de catálogo requieren reinicio o nuevo login para reflejarla.
- El borrado físico de una acción protegida puede requerir una migración coordinada por la relación con `acciones_roles`; no forma parte de la gestión normal desde la aplicación.

## Referencias

- Migración Supabase `hacer_inmutable_rol_administrador_todos_permisos` (`20260902032735`).
- `CapaUI/Formularios/Principal/Pantallas/Roles/RolesViewModel.cs`.
- `CapaUI/Formularios/Principal/Pantallas/Roles/RolesResources.xaml`.

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Usuarios]]
- [[ADR-023 - Rol Administrador de sistema con nucleo de permisos protegido]]
- [[Sesión 2026-09-02 - Administrador inmutable con acceso total]]
