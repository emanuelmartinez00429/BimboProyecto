---
title: "ADR-010 - Permisos desde BD en vez de switch hardcodeado"
tags: [adr, decision, bimbo, gestion-usuarios, permisos]
date: 2026-07-23
estado: aceptado
---

# ADR-010 - Permisos desde Base de Datos en vez de switch hardcodeado

**Módulo:** [[Módulo Usuarios]]
**Fecha:** Fase 7 (Julio 2026)

## Contexto

Los permisos estaban hardcodeados en un `switch (idRol)` con 3 cases fijos: Admin=1, Operador=2, Observador=3. Agregar un nuevo rol o permiso requería tocar código C# y recompilar.

## Opciones consideradas

| # | Opción | Veredicto |
|---|--------|-----------|
| 1 | Mantener el switch hardcodeado | Rechazada — no escala |
| 2 | Cargar desde BD al login: 3 queries paralelas → agrupar por módulo → HashSet O(1) | **Elegida** |
| 3 | Cargar bajo demanda por cada chequeo | Rechazada — muy lento |

## Decisión tomada

Cargar una vez al login:
1. `acciones_roles` filtrado por `id_rol` → IDs de acciones
2. `acciones` → todas las acciones (nombre, módulo)
3. `modulos` → todos los módulos

3 queries en paralelo vía `Task.WhenAll`, agrupadas en `ModuloPermisos`, indexadas en `HashSet<string>` para lookup O(1).

## Por qué

- Escalable: agregar permisos = INSERT en BD, sin tocar código.
- Coherente con las tablas existentes (`acciones_roles`, `acciones`, `modulos`).
- Performante: queries paralelas + lookup O(1).
- Auditable vía SQL directo.

## Consecuencias / Trade-offs

| Gana | Sacrifica |
|------|-----------|
| Escalable — nuevos roles sin recompilar | 3 queries adicionales en login (~100-200ms) |
| Auditable vía SQL | Cache implica re-login para ver cambios de permisos |
| Lookup O(1) con HashSet | HashSet no distingue "sin permiso" de "sin sesión" |
| Coherente con tablas BD | — |

## Relaciones

- [[Módulo Usuarios]] — módulo que implementa esta decisión
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]] — el servicio carga los permisos
- [[ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML]] — fachada que consulta estos permisos
