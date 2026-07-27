---
title: "ADR-009 - RPC crear_usuario_empleado_seguro para vinculacion auth-empleado"
tags: [adr, decision, bimbo, gestion-usuarios, rpc, supabase]
date: 2026-07-23
estado: aceptado
---

# ADR-009 - RPC crear_usuario_empleado_seguro para vinculación auth-empleado

**Módulo:** [[Módulo Usuarios]]
**Fecha:** Fase 7 (Julio 2026)

## Contexto

Crear un usuario implica 2 pasos: (1) auth SignUp en Supabase Auth, (2) vincular al empleado en la tabla `usuarios`. Hacerlo en C# con queries separadas crea condiciones de carrera.

## Opciones consideradas

| # | Opción | Veredicto |
|---|--------|-----------|
| 1 | Todo en C# con queries separadas | Rechazada — race conditions |
| 2 | RPC `crear_usuario_empleado_seguro`: función PostgreSQL atómica con códigos de resultado | **Elegida** |
| 3 | Trigger de base de datos | Rechazada — menos control desde la app |

## Decisión tomada

RPC recibe `p_id_empleado`, `p_email`, `p_rol`. Retorna códigos: `USUARIO_CREADO`, `USUARIO_NO_EXISTE`, `USUARIO_YA_EXISTE`.

## Por qué

- Atómico (transacción SQL).
- Seguro (RLS aplica).
- Reutilizable (cualquier cliente puede llamar).
- Códigos de resultado claros.

## Bugs corregidos en el RPC original

1. UUID del usuario Auth no se pasaba correctamente al INSERT.
2. Existencia del empleado no se verificaba antes de vincular.
3. Faltaba chequeo de duplicados (no retornaba `USUARIO_YA_EXISTE`).

## Consecuencias / Trade-offs

| Gana | Sacrifica |
|------|-----------|
| Lógica atómica en servidor | Dependencia de mantenimiento de función SQL |
| Códigos de resultado claros | Difícil de debuggear desde C# |
| Reutilizable | Requiere migración SQL para cambios |
| Una sola llamada de red | Mensajes de error como strings |

## Relaciones

- [[Módulo Usuarios]] — flujo de creación de usuarios
- [[ADR-008 - Cliente Temporal para SignUp de Usuarios]] — paso 1 de creación (auth SignUp)
- [[Supabase .NET]] — SDK utilizado
