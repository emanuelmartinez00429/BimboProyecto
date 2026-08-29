---
title: ADR-008 - Cliente Temporal para SignUp de Usuarios
type: adr
status: vigente
tags:
  - adr
  - decision
  - bimbo
  - gestion-usuarios
  - auth
  - supabase
date: 2026-07-23
updated: 2026-07-23
summary: "El SDK de Supabase mantiene una sola sesión por instancia de Supabase.Client. Cuando el admin crea un usuario, el SignUp() sobreescribe la sesión del admin en el…"
scope: []
symbols: []
estado: aceptado
---

# ADR-008 - Cliente Temporal para SignUp de Usuarios

**Módulo:** [[Módulo Usuarios]]
**Fecha:** Fase 7 (Julio 2026)

## Contexto

El SDK de Supabase mantiene una sola sesión por instancia de `Supabase.Client`. Cuando el admin crea un usuario, el `SignUp()` sobreescribe la sesión del admin en el cliente singleton, corruptándola.

## Opciones consideradas

| # | Opción | Veredicto |
|---|--------|-----------|
| 1 | Usar el singleton y restaurar la sesión después del SignUp | Rechazada — corrupta si falla a mitad |
| 2 | Crear un cliente temporal (`AutoRefreshToken = false`, `AutoConnectRealtime = false`) | **Elegida** |
| 3 | Llamada HTTP directa a la API de Supabase | Rechazada — pierde validación del SDK |

## Decisión tomada

Cliente `Supabase.Client` temporal por cada creación de usuario: misma URL/key, auto-features deshabilitados, `InitializeAsync()` → `SignUp()` → `SignOut()` + `Dispose()` en `finally`, luego restaurar sesión del admin con `SetSession()`.

## Por qué

- La sesión del admin nunca se toca.
- Cleanup garantizado (bloque `finally`).
- Funciona aunque el SignUp falle.
- Sin configuración adicional.

## Consecuencias / Trade-offs

| Gana | Sacrifica |
|------|-----------|
| Sesión del admin intacta | Crear/destruir Client por usuario (costoso) |
| Cleanup garantizado (finally) | Doble llamada a `SetSession()` |
| Sin config adicional | Más complejo que flujo lineal |
| — | `Debug.WriteLine` debe limpiarse antes de producción |

## Relaciones

- [[Módulo Usuarios]] — flujo de creación de usuarios
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]] — sesión que se preserva
- [[ADR-009 - RPC crear_usuario_empleado_seguro para vinculacion auth-empleado]] — paso 2 de creación
