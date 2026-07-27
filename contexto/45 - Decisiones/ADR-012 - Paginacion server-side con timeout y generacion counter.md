---
title: "ADR-012 - Paginacion server-side con timeout y generacion counter"
tags: [adr, decision, bimbo, gestion-usuarios, paginacion, timeout]
date: 2026-07-23
estado: aceptado
---

# ADR-012 - Paginación Server-Side con Timeout y Generación Counter

**Módulo:** [[Módulo Usuarios]]
**Fecha:** Fase 7 (Julio 2026)

## Contexto

Las queries a Supabase pueden ser lentas. Sin protección, el usuario cambia de página rápido y la UI se congela esperando una respuesta que ya no le importa (race condition de paginación).

## Opciones consideradas

| # | Opción | Veredicto |
|---|--------|-----------|
| 1 | Paginación client-side | Rechazada — no escala |
| 2 | Server-side sin protección | Rechazada — UI congelada indefinidamente |
| 3 | Server-side con timeout + generation counter | **Elegida** |

## Decisión tomada

`Task.WhenAny(task, Task.Delay(10_000))` con contador atómico `_loadGeneration` para descartar respuestas stale. Counts paralelos para total/activos/inactivos.

- Máximo 10 segundos de espera antes de mostrar estado stale.
- `_loadGeneration` incrementado con `Interlocked.Increment` en cada carga.
- Respuestas de generaciones anteriores se descartan silenciosamente.

## Por qué

- UX: máximo 10s de espera antes de mostrar estado.
- Seguridad de datos: generation counter previene overwrites stale.
- Patrón reutilizable: ya existe en `CategoriasViewModel`.
- Simple: sin `CancellationTokenSource`.

## Consecuencias / Trade-offs

| Gana | Sacrifica |
|------|-----------|
| UI nunca congelada >10s | Timeout no cancela la llamada HTTP (solo deja de esperar) |
| Race conditions eliminadas | Respuestas late descartadas desperdician red |
| Counts paralelos | `TimeoutMs` duplicado por cada VM |
| Patrón simple y reutilizable | Necesita `Interlocked.Increment` para multi-threaded |

## Relaciones

- [[Módulo Usuarios]] — grid de usuarios con paginación
- [[Paginación y Búsqueda - Arquitectura Detallada]] — arquitectura completa de paginación
- [[Módulo Productos]] —另一个 ViewModel que usa este patrón
