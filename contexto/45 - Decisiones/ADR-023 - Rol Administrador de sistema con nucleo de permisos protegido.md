---
title: "ADR-023 - Rol Administrador de sistema con núcleo de permisos protegido"
tags: [bimbo, arquitectura, seguridad, rbac, auditoria]
date: 2026-09-01
estado: reemplazado
---

# ADR-023 - Rol Administrador de sistema con núcleo de permisos protegido

> [!warning] Reemplazado el 2026-09-02
> [[ADR-024 - Rol Administrador inmutable con acceso total]] sustituye el núcleo parcial por la protección de todas las acciones actuales y futuras.

## Contexto

Los permisos pertenecen a roles compartidos, no a usuarios individuales. Permitir que una sesión administrativa retire todas las capacidades de administración del rol Administrador puede dejar la instalación sin una vía normal de recuperación. Bloquear cualquier cambio impediría adaptar permisos operativos como Pesaje, Productos o Reportería.

## Decisión

`roles.es_sistema` identifica de forma estable el único rol Administrador del sistema. No depende del nombre ni del identificador después de la migración inicial.

El rol de sistema no se puede renombrar, desactivar, eliminar ni desmarcar. Sí puede agregar o retirar permisos operativos, pero debe conservar activas estas acciones:

- Consultar Rol
- Crear Rol
- Modificar Rol
- Asignar Permisos a Rol
- Consultar Usuario
- Crear Usuario
- Asignar Rol a Usuario

`Eliminar Rol` no es parte del núcleo porque no es necesario para recuperar el control administrativo. La restricción se aplica en UI y PostgreSQL mediante RPC autorizadas, triggers de invariantes y auditoría en `bitacora`.

## Alternativas consideradas

- Bloquear todos los permisos del Administrador: impide configurar su alcance operativo.
- Proteger por nombre o `id_rol`: depende de detalles mutables o del despliegue.
- Aplicar solo validación visual: un cliente directo podría omitirla.

## Consecuencias

- El Administrador sigue siendo configurable para módulos operativos.
- Siempre queda un núcleo suficiente para administrar roles y usuarios.
- Los cambios afectan a todas las cuentas que comparten el rol y requieren renovar sesión para refrescar permisos.
- Las escrituras directas sobre `roles` y `acciones_roles` quedan revocadas a clientes; las mutaciones pasan por RPC auditadas.

## Relaciones

- [[Módulo Usuarios]]
- [[Arquitectura Actual]]
- [[ADR-020 - Defensa en profundidad contra autoadministracion de usuarios]]
- [[Sesión 2026-09-01 - Gestión auditable de roles]]
- [[ADR-024 - Rol Administrador inmutable con acceso total]]
