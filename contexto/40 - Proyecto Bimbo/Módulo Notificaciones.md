---
title: "Módulo Notificaciones"
tags: [bimbo, notificaciones, supabase, realtime, rbac]
date: 2026-09-21
estado: operativo
---

# Módulo Notificaciones

> [!success] Estado vigente
> La infraestructura, seguridad, campana, bandeja y detalle están operativos. Desde el 2026-09-21 la bandeja consume los filtros compartidos del sistema y el detalle es un modal sobre el overlay de `MainWindow`, con la anatomía de Bitácora y los colores dinámicos de la empresa. «Registro relacionado» muestra el nombre del usuario o rol; el ID técnico se conserva solo para navegación. Los emisores de pesaje, roles, proveedores, fabricantes, usuarios, productos y categorías operan mediante RPC idempotentes. `PRODUCTO_EXISTENCIA_BAJA` permanece deshabilitado hasta existir un modelo real de inventario.

## Responsabilidades

- `bitacora`: evidencia histórica exhaustiva e inmutable.
- `notificaciones`: evento general seleccionado para la bandeja.
- `notificaciones_usuario`: destinatario y lectura/archivo individual.
- Supabase Realtime: señal de cambio sobre `notificaciones_usuario`, no fuente de verdad.
- RPC: única vía del cliente para listar, contar y modificar el estado individual o masivo de sus notificaciones.

No existe caché SQLite, modo offline ni cola local. Si Supabase no está disponible, la UI vacía el estado derivado, informa la desconexión y bloquea operaciones. Al reconectar, se suscribe antes de consultar nuevamente listado y contador.

## Seguridad y RBAC

Las acciones son `NOTIFICACIONES_CONSULTAR` y `NOTIFICACIONES_GESTIONAR`. El Administrador las recibe mediante la garantía general del rol de sistema. La bandeja usa la primera; la segunda queda reservada para resolución global futura.

Las RPC resuelven al usuario con `auth.uid()`, verifican usuario/rol/asignación activos y no aceptan un `id_usuario` para escoger otra bandeja. RLS permite a Realtime evaluar solamente la fila de `notificaciones_usuario` del usuario autenticado. No hay escritura directa para `authenticated` ni acceso para `anon`.

## Componentes C#

- `CapaAplicacion4/Notificaciones/`: DTO y contrato del repositorio.
- `CapaDatos/Repositories/Notificaciones/NotificacionRepository.cs`: llamadas RPC.
- `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionesResources.xaml`: botones de texto e icono, estados interactivos, tarjetas y colores de severidad compartidos.
- `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionesView.xaml`: bandeja completa con filtros `Bandeja`, `No leídas`, `Leídas` y `Archivadas`.
- `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionDetalleModal.xaml`: modal declarativo sobre overlay, con resumen de severidad, colores dinámicos y renderizado seguro de metadata.
- `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionMetadataRenderer.cs`: campos legibles; oculta identificadores técnicos y evita duplicar fecha/severidad en el cuerpo.
- `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionesViewModel.cs`: listado, cinco recientes, contador, navegación y comandos de estado.
- `MainWindow`: campana, contador, desplegable limitado a las cinco notificaciones más recientes de Bandeja y host de `NotificacionDetalleModal`.
- `RealtimeService`: canal opcionalmente filtrado y espera de suscripción.
- `BimboProyecto.Tests/Notificaciones/ContratoNotificacionesTests.cs`: contrato C#/XAML de la corrección.

La inicialización sigue: validar permiso → suscribir canal filtrado → listar → cargar cinco recientes → contar. Los eventos `INSERT`/`UPDATE` provocan una recarga autorizada. Después de cualquier mutación, incluida la acción masiva, la UI vuelve a consultar listado, recientes y contador mediante RPC; no modifica las colecciones de forma optimista.

## Persistencia

Las migraciones `20260902115932_notificaciones_internas_rbac.sql` y `20260902123358_endurecer_notificaciones_indices.sql` crean catálogo, eventos, destinatarios, restricciones, índices, políticas, privilegios y RPC. La migración forward-only `20260903075117_archivar_notificaciones_al_marcar_todas.sql` conserva el nombre, firma, propietario, `SECURITY DEFINER`, `search_path` y privilegios de `marcar_todas_mis_notificaciones_leidas()` mientras reemplaza su comportamiento masivo. `private.crear_notificacion` no es ejecutable por el cliente y distribuye a usuarios activos que tenían `NOTIFICACIONES_CONSULTAR` al ocurrir el evento.

La migración `20260921141136_mostrar_nombre_registro_en_notificaciones.sql` conserva la firma tabular de `listar_mis_notificaciones` y enriquece su `metadata` de salida con `nombre_registro_origen`: nombre completo del empleado —con `alias_usuario` como respaldo— para `usuarios`, y `nombre_rol` para `roles`. No actualiza `notificaciones.metadata`; `tabla_origen` e `id_registro_origen` siguen siendo las referencias canónicas para navegación. Si el origen dejó de existir, el detalle omite el campo antes que mostrar el ID.

`Marcar todas como leídas y archivar` ejecuta un único `UPDATE` autorizado por `auth.uid()` sobre todas las filas no archivadas del usuario: conserva la primera fecha de lectura, asigna la fecha de archivo y deja intactas las filas ya archivadas. Incluye tanto notificaciones no leídas como previamente leídas y mantiene al usuario en `Bandeja`, que queda vacía después de la recarga. El archivado y la restauración individuales continúan disponibles.

La retención acordada es 180 días, pero la depuración automática no forma parte de esta entrega. Resolver será global y requerirá `NOTIFICACIONES_GESTIONAR` cuando se implemente.

## Pendientes

- La homologación visual de filtros, modal y nombre relacionado fue enviada a contexto por solicitud de Emanuel el 2026-09-21; queda pendiente una prueba manual multisesión de navegación y reconexión con dos sesiones WPF autenticadas.
- `PRODUCTO_EXISTENCIA_BAJA`, resolución global y retención automática continúan fuera de alcance y requieren autorización posterior.

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Usuarios]]
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]]
- [[Sesión 2026-09-02 - Infraestructura de notificaciones internas]]
- [[Sesión 2026-09-03 - Corrección visual y acción masiva de Notificaciones]]
- [[Sesión 2026-09-21 - Homologación visual y nombres relacionados en Notificaciones]]
