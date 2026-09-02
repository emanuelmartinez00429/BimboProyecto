---
title: "ADR-022 — Persistencia local-first con SQLCipher y sincronización por outbox"
tags:
  - adr
  - offline-first
  - sqlcipher
  - supabase
  - sincronizacion
date: 2026-08-30
estado: propuesto
---

# ADR-022 — Persistencia local-first con SQLCipher y sincronización por outbox

> [!warning] Propuesta no aprobada
> Este ADR registra la dirección arquitectónica planteada, pero no autoriza implementación. Las decisiones de sucursal, cuentas Windows, concesión offline, infraestructura criptográfica y backup externo siguen abiertas.

## Contexto

La aplicación WPF consulta actualmente Supabase a través de repositorios. El monitor de conexión evita algunas llamadas cuando no existe red, pero no ofrece trabajo offline. Cambiar entre Supabase y SQLite según conectividad introduciría dos flujos de lectura, resultados distintos durante una sesión y mayor riesgo de divergencia.

Pesaje ya dispone de diez RPC idempotentes/auditadas, aunque su integración en C# es parcial. La infraestructura actual no incluye SQLCipher, `outbox`, versión optimista, sucursales ni sesiones offline.

## Decisión propuesta

Adoptar una arquitectura local-first para el módulo de Pesaje:

- SQLCipher será la fuente única de la interfaz para las pantallas habilitadas.
- Supabase/PostgreSQL seguirá siendo la fuente autoritativa empresarial.
- Toda acción local guardará en una transacción la propuesta visible y un comando empresarial en `outbox`.
- Las lecturas no entrarán a la cola y no se almacenarán sentencias SQL para reproducir.
- Las RPC v2 serán específicas, idempotentes, versionadas y auditadas.
- La respuesta RPC será la confirmación primaria; Realtime será un canal descendente no durable.
- Snapshot identificado, staging y buffer Realtime cerrarán la carrera de reconexión.
- La primera base compartida contendrá solamente Pesaje y catálogos de la sucursal.
- Las colas y concesiones estarán separadas por usuario de aplicación.
- SQLCipher se compilará internamente y la clave se protegerá con mecanismos Windows más recuperación corporativa cuando la infraestructura sea aprobada.

El detalle operativo, fases, pruebas y decisiones pendientes vive en [[Plan Offline-First de Pesaje]].

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| Supabase online y SQLite solo al perder conexión | Menos cambios iniciales | Dos fuentes durante una sesión, consultas duplicadas y divergencia | ❌ |
| SQLCipher local-first con outbox | Flujo único, continuidad y sincronización explícita | Mayor inversión en seguridad, idempotencia, conflictos y recuperación | Propuesta |
| Servidor local por sucursal | Puede centralizar varios equipos offline | Nueva infraestructura, despliegue y punto de falla local | ❌ para v1 |
| Cache de lectura sin escrituras offline | Riesgo menor | No resuelve continuidad operativa completa | Fase intermedia |

## Consecuencias

### A favor

- La UI no cambia de origen en medio de una sesión.
- Las acciones sobreviven reinicios y pérdidas de respuesta.
- La idempotencia central evita duplicar cambios y bitácora.
- El alcance local puede limitarse y revocarse por usuario/sucursal.
- Se conserva atribución individual en un dispositivo compartido.

### Costos y riesgos

- Hace obligatorios versionado, staging, reconciliación, resolución de conflictos y recuperación de claves/outbox.
- Requiere una decisión central de sucursales que hoy no existe en el esquema.
- Una base compartida no aísla criptográficamente a los usuarios de aplicación.
- No puede habilitarse escritura offline hasta completar las fases de seguridad central y multiusuario.
- Un backup en el mismo disco no protege pendientes ante pérdida física.

## Estado de aprobación

`propuesto`. La implementación queda bloqueada hasta aprobación expresa del plan y resolución de las puertas registradas en [[Plan Offline-First de Pesaje]].

## Relaciones

- [[Plan Offline-First de Pesaje]]
- [[Arquitectura Actual]]
- [[Módulo Pesaje]]
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]]
- [[ADR-015 - Cache de catalogos mostrar y revalidar]]
- [[Sesión 2026-08-30 - Documentación del plan offline-first de Pesaje]]

