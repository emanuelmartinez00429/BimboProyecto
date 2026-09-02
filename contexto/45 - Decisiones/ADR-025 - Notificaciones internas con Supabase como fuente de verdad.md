---
title: "ADR-025 - Notificaciones internas con Supabase como fuente de verdad"
tags: [bimbo, arquitectura, notificaciones, supabase, realtime, rbac]
date: 2026-09-02
estado: aceptado
---

# ADR-025 - Notificaciones internas con Supabase como fuente de verdad

## Contexto

La aplicación necesita una bandeja de eventos seleccionados, separada de la bitácora exhaustiva, con lectura independiente por destinatario. La infraestructura local SQLCipher/offline aún no existe y no debe anticiparse.

## Decisión

Supabase es la única fuente de verdad. `notificaciones` representa el evento general y `notificaciones_usuario` conserva lectura y archivo por usuario. El acceso depende de códigos RBAC estables, no del nombre ni ID del rol. Realtime observa únicamente `notificaciones_usuario` y actúa como señal para volver a consultar RPC autorizadas.

Las operaciones empresariales notificables comparten `id_solicitud` con auditoría e idempotencia. Un reintento devuelve el resultado canónico y una operación sin cambios no produce bitácora de modificación ni notificación.

## Alternativas consideradas

- Usar `bitacora` como bandeja: mezcla auditoría con estado mutable individual.
- Guardar `leida` en la notificación general: la lectura de una persona afectaría a todos.
- Usar Realtime como fuente única: pierde eventos durante desconexiones y deja una ventana de carrera inicial.
- Introducir SQLite/SQLCipher ahora: crea infraestructura especulativa fuera del alcance vigente.

## Consecuencias

- La lectura y el archivo de dos administradores son independientes.
- Sin conexión no se muestran datos como si estuvieran sincronizados ni se aceptan mutaciones aparentes.
- Reconexión e inicio siempre vuelven a consultar listado y contador.
- Cada emisor empresarial debe ser transaccional, idempotente y seguro antes de conectarse.

## Riesgos

- Hasta modernizar las mutaciones restantes, el catálogo contiene tipos que todavía no tienen emisor.
- Realtime requiere conservar el `SELECT` mínimo sobre `notificaciones_usuario` y una política RLS evaluable por el usuario suscrito.
- La resolución global y la depuración por retención quedan fuera de esta primera versión.

## Relaciones

- [[Módulo Notificaciones]]
- [[Módulo Usuarios]]
- [[Arquitectura Actual]]
- [[Sesión 2026-09-02 - Infraestructura de notificaciones internas]]
