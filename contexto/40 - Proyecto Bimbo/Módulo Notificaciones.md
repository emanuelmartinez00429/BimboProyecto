---
title: "Módulo Notificaciones"
tags: [bimbo, notificaciones, supabase, realtime, rbac]
date: 2026-09-02
estado: parcial
---

# Módulo Notificaciones

> [!warning] Estado vigente
> La infraestructura, seguridad, campana y bandeja están implementadas. Los emisores de pesaje, permisos de rol y cambio de rol están conectados. Los eventos de proveedores, fabricantes, usuarios, productos y categorías aún requieren migrar sus mutaciones a RPC idempotentes antes de emitir notificaciones. `PRODUCTO_EXISTENCIA_BAJA` permanece deshabilitado hasta existir un modelo real de inventario.

## Responsabilidades

- `bitacora`: evidencia histórica exhaustiva e inmutable.
- `notificaciones`: evento general seleccionado para la bandeja.
- `notificaciones_usuario`: destinatario y lectura/archivo individual.
- Supabase Realtime: señal de cambio sobre `notificaciones_usuario`, no fuente de verdad.
- RPC: única vía del cliente para listar, contar y modificar su estado individual.

No existe caché SQLite, modo offline ni cola local. Si Supabase no está disponible, la UI vacía el estado derivado, informa la desconexión y bloquea operaciones. Al reconectar, se suscribe antes de consultar nuevamente listado y contador.

## Seguridad y RBAC

Las acciones son `NOTIFICACIONES_CONSULTAR` y `NOTIFICACIONES_GESTIONAR`. El Administrador las recibe mediante la garantía general del rol de sistema. La bandeja usa la primera; la segunda queda reservada para resolución global futura.

Las RPC resuelven al usuario con `auth.uid()`, verifican usuario/rol/asignación activos y no aceptan un `id_usuario` para escoger otra bandeja. RLS permite a Realtime evaluar solamente la fila de `notificaciones_usuario` del usuario autenticado. No hay escritura directa para `authenticated` ni acceso para `anon`.

## Componentes C#

- `CapaAplicacion4/Notificaciones/`: DTO y contrato del repositorio.
- `CapaDatos/Repositories/Notificaciones/NotificacionRepository.cs`: llamadas RPC.
- `CapaUI/Formularios/Principal/Pantallas/Notificaciones/`: ViewModel y bandeja WPF.
- `MainWindow`: campana, contador y vista previa.
- `RealtimeService`: canal opcionalmente filtrado y espera de suscripción.

La inicialización sigue: validar permiso → suscribir canal filtrado → listar → contar. Los eventos `INSERT`/`UPDATE` provocan una recarga autorizada y se deduplican por `id_notificacion`.

## Persistencia

Las migraciones `20260902115932_notificaciones_internas_rbac.sql` y `20260902123358_endurecer_notificaciones_indices.sql` crean catálogo, eventos, destinatarios, restricciones, índices, políticas, privilegios y RPC. `private.crear_notificacion` no es ejecutable por el cliente y distribuye a usuarios activos que tenían `NOTIFICACIONES_CONSULTAR` al ocurrir el evento.

La retención acordada es 180 días, pero la depuración automática no forma parte de esta entrega. Archivar es individual; resolver será global y requerirá `NOTIFICACIONES_GESTIONAR` cuando se implemente.

## Pendientes

- Modernizar las RPC/mutaciones de proveedores, fabricantes, usuarios, productos y categorías con `p_id_solicitud`, no-op e idempotencia antes de conectar sus emisores.
- Implementar navegación al registro de origen con una segunda validación del permiso del módulo destino.
- Implementar resolución global y retención automática solo con autorización posterior.
- Ejecutar QA visual autenticada y multisesión.

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Usuarios]]
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]]
- [[Sesión 2026-09-02 - Infraestructura de notificaciones internas]]
