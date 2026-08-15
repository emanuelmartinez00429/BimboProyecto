---
title: "ADR-020 - Defensa en profundidad contra autoadministración de usuarios"
tags: [adr, decision, seguridad, usuarios, rls]
date: 2026-08-15
estado: aceptado
---

# ADR-020: Defensa en profundidad contra autoadministración de usuarios

## Contexto

El módulo permitía que un usuario con permisos administrativos seleccionara su propia fila, cambiara su rol o se deshabilitara. El repositorio confiaba únicamente en el ID solicitado y la política `update_Usuarios` aceptaba actualizaciones de `public` con condiciones verdaderas.

## Decisión

Bloquear la autoadministración en UI y repositorio comparando `IdUsuario` con la sesión inyectada. En PostgreSQL, proteger cambios de `id_rol` e `id_estado` mediante un trigger que compara `OLD.uuid_usuario` con `auth.uid()` y exige el permiso específico para administrar terceros.

La política UPDATE queda limitada a `authenticated`. La propia fila admite actualizaciones no sensibles para conservar `ActualizarUltimoAccesoAsync`; el trigger impide elevar privilegios o deshabilitarse.

## Consecuencias

- Ningún rol tiene excepción para autoadministrarse.
- Una llamada directa que evite WPF sigue protegida.
- `ultimo_acceso` continúa actualizándose normalmente.
- Rol y estado usan permisos independientes: `Modificar Usuario` y `Eliminar Usuario`.

## Relaciones

- [[Módulo Usuarios]]
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]]
- [[Sesión 2026-08-15 - Protección contra autoadministración de usuarios]]
