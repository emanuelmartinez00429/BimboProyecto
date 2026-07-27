---
title: "ADR-013 - Eliminacion de SesionActual y servicioSesionActual legacy"
tags: [adr, decision, bimbo, gestion-usuarios, legacy, limpieza]
date: 2026-07-23
estado: aceptado
---

# ADR-013 - Eliminación de SesionActual y servicioSesionActual Legacy

**Módulo:** [[Módulo Usuarios]]
**Fecha:** Fase 7 (Julio 2026)

## Contexto

Los archivos legacy `SesionActual.cs` y `servicioSesionActual.cs` en CapaDominio eran holders estáticos que coexistían con el nuevo `IUsuarioSesionService`. Mientras existieran, siempre había la tentación de usar el camino viejo.

## Opciones consideradas

| # | Opción | Veredicto |
|---|--------|-----------|
| 1 | Mantener legacy y documentar como deuda técnica | Rechazada — más código se acopla a diario |
| 2 | Eliminar completamente y reemplazar con `IUsuarioSesionService` | **Elegida** |
| 3 | Marcar `[Obsolete]` y crear servicio nuevo | Rechazada — no se necesita transición gradual |

## Decisión tomada

Eliminar ambos archivos. Todos los `SesionActual.IdUsuario` → `_sesionService.SesionActual?.IdUsuario`. Login simplificado de 4 a 3 pasos (eliminadas llamadas separadas a `servicioSesionActual.Iniciar()` y `SesionPermisos.CargarAsync()`).

## Por qué

- Sin razón para mantener código legacy cuando el reemplazo funciona.
- Más código se acopla a diario a los estáticos.
- Los estáticos no son testeables (fuerzan DI).
- Limpieza mental: no hay tentación de usar el camino viejo.

## Consecuencias / Trade-offs

| Gana | Sacrifica |
|------|-----------|
| Legacy eliminado, sin confusión | Todos los llamadores cambiados en una fase |
| Mutable global state removido | Sin fallback si el servicio tiene bugs |
| Login simplificado (4→3 pasos) | Sin rollback gradual |
| DI forzado en todo | — |

## Relaciones

- [[Módulo Usuarios]] — módulo que reemplazó el legacy
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]] — el servicio que reemplaza el legacy
- [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]] — permisos integrados en el nuevo servicio
- [[ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML]] — fachada sobre el nuevo servicio
