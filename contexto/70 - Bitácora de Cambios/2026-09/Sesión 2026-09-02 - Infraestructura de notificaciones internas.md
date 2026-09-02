---
title: "Sesión 2026-09-02 - Infraestructura de notificaciones internas"
tags: [sesion, bimbo, notificaciones, supabase, realtime, rbac]
date: 2026-09-02
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-09-02 - Infraestructura de notificaciones internas

## Objetivo

Implementar la base segura del submódulo de notificaciones sin SQLite ni comportamiento offline simulado.

## Trabajo realizado

- Se creó el modelo general/destinatario, catálogo de 19 tipos, restricciones, índices, RLS y privilegios mínimos.
- Se incorporaron `codigo_accion`, las acciones del módulo y la etiqueta `Cambiar Estado de Rol`.
- Las cinco RPC de roles ahora reciben `p_id_solicitud`, detectan reintentos/no-op y comparten solicitud entre auditoría y notificación.
- Se conectaron los eventos de pesaje, permisos de rol y cambio de rol.
- Se añadieron RPC para listar, contar, leer, leer todas, archivar y restaurar únicamente las filas propias.
- Se implementaron campana, contador, vista previa, bandeja, filtros, paginación y bloqueo explícito sin conexión.
- Realtime se filtra por usuario interno, se establece antes de la consulta y solo provoca resincronización desde Supabase.
- Se eliminaron las clases legacy de notificaciones en memoria.

## Pruebas y validaciones

- Prueba remota transaccional con dos administradores: destinatarios compartidos y lectura independiente.
- Repetir lectura conserva la primera fecha; usuario sin permiso recibe rechazo.
- Reintento/no-op de permisos no duplica notificación ni auditoría.
- Catálogo, permisos, grants y restricciones fueron inspeccionados después de migrar.
- `dotnet build BimboProyecto.sln --no-restore` finalizó con 0 errores y las 51 advertencias nullable preexistentes.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-build --no-restore` superó 113/113 pruebas.
- `git diff --check` no detectó errores; solo avisos informativos LF/CRLF.

## Pendientes

- Migrar de forma segura los emisores de proveedores, fabricantes, usuarios, productos y categorías; no se añadió un trigger inseguro sobre bitácora como atajo.
- Implementar navegación autorizada al registro de origen.
- QA visual autenticada y multisesión.
- `PRODUCTO_EXISTENCIA_BAJA`, resolución global y retención automática permanecen diferidos.

## Relaciones

- [[Módulo Notificaciones]]
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]]
- [[Módulo Usuarios]]
- [[Arquitectura Actual]]

## 11:20 — Reconciliación de emisores empresariales y navegación

### Trabajo realizado

- Se aplicó una migración forward-only que corrige la activación de usuarios, elimina DML directo de catálogos/usuarios y restringe las RPC empresariales a `authenticated`.
- `actualizar_usuario_seguro` ahora detecta no-op; `private.crear_notificacion` guarda metadata mínima de navegación.
- La UI conserva el `id_solicitud` al reintentar la misma operación y la bandeja navega solo a orígenes conocidos tras una segunda comprobación RBAC.

### Validaciones

- Compilación con 0 errores y 37 advertencias preexistentes.
- 118/118 pruebas automatizadas superadas.
- Verificación remota: sin `USUARIOS_ACTIVAR`, sin DML directo para `anon`/`authenticated`, sin políticas DML en las cinco tablas y `actualizar_usuario_seguro` ejecutable solo por `authenticated`.

### Pendientes

- Falta QA visual autenticada y multisesión; los avisos históricos de asesores fuera del alcance de notificaciones permanecen registrados.
