---
title: ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico
type: adr
status: vigente
tags:
  - adr
  - decision
  - bimbo
  - gestion-usuarios
  - sesion
date: 2026-07-23
updated: 2026-07-23
summary: "Crear IUsuarioSesionService como Singleton con: SesionActual (read-only), IniciarSesionAsync(idUsuario), CerrarSesion(), TienePermiso(nombreAccion)."
scope: []
symbols:
  - IUsuarioSesionService
  - SesionActual
estado: aceptado
---

# ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico

**Módulo:** [[Módulo Usuarios]]
**Fecha:** Fase 7 (Julio 2026)

## Contexto

`SesionActual.cs` era un holder estático `public static int? IdUsuario` en CapaDominio, y `servicioSesionActual.cs` una clase estática con `Iniciar()`. El módulo de usuarios necesitaba un servicio que persistiera la sesión global de la aplicación, cargara permisos desde la BD, y fuera inyectable en ViewModels.

## Opciones consideradas

| # | Opción | Veredicto |
|---|--------|-----------|
| 1 | Mantener los estáticos y agregar funcionalidad encima | Rechazada — escala mal, no testeable |
| 2 | Crear `IUsuarioSesionService` como **Singleton** en DI | **Elegida** |
| 3 | Scoped por operación | Rechazada — la sesión debe persistir entre ViewModels |

## Decisión tomada

Crear `IUsuarioSesionService` como Singleton con: `SesionActual` (read-only), `IniciarSesionAsync(idUsuario)`, `CerrarSesion()`, `TienePermiso(nombreAccion)`.

## Por qué

- La sesión es estado global de la app, pertenece a DI, no al dominio.
- Singleton = instanciado una vez, inyectable, mockeable.
- La interfaz vive en CapaAplicacion (principio de DI).

## Consecuencias / Trade-offs

| Gana | Sacrifica |
|------|-----------|
| Estado centralizado y coherente | Refactor de todos los llamadores de `SesionActual.IdUsuario` |
| Testeable vía interfaz | Dependencia de DI en ViewModels |
| Permisos desde BD | 3 queries paralelas en login (~100-200ms) |
| Sin mutable globals | — |

## Relaciones

- [[Módulo Usuarios]] — módulo que impulsó esta decisión
- [[ADR-008 - Cliente Temporal para SignUp de Usuarios]] — sesión preservada al crear usuarios
- [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]] — permisos cargados en el servicio
- [[ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML]] — fachada sobre este servicio
- [[ADR-013 - Eliminacion de SesionActual y servicioSesionActual legacy]] — limpieza del legacy
