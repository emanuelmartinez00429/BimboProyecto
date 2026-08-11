---
title: "ADR-014 — Precarga única y caché del catálogo RBAC"
tags:
  - adr
  - decision
  - rbac
  - rendimiento
  - supabase
date: 2026-08-11
estado: aceptado
---

# ADR-014 — Precarga única y caché del catálogo RBAC

## Contexto

La pantalla de Roles tardaba **~3 segundos en blanco** en cada visita. La causa no era una consulta lenta sino el patrón de acceso:

1. `MainViewModel` crea una instancia nueva en cada navegación (`() => new RolesVM()`), así que no había nada que sobreviviera entre visitas.
2. `CargarAsync` pedía roles + catálogo; después, **cada cambio de rol** disparaba otra consulta (`ObtenerAccionesAsignadasAsync`) y **reconstruía toda la colección** de módulos y acciones.

Sobre datos que casi no cambian: `modulos` y `acciones` solo se mueven con una migración de esquema, y `roles` no tiene CRUD propio todavía.

## Decisión

Dos cambios complementarios:

**1. Una sola llamada que trae todo** — `IRolPermisoRepository.ObtenerResumenAsync` devuelve `RolesResumenDto`: roles, catálogo de módulos/acciones, asignaciones activas de **todos** los roles, y conteo de usuarios por rol. Cambiar de rol pasa a ser una operación **en memoria**: no toca la red.

**2. Caché estática de proceso solo para lo inmutable** — el catálogo (`RolPermisoRepository._catalogoCache`) y la lista de roles (`RolRepository._rolesCache`), ambos con doble verificación bajo `SemaphoreSlim`.

Las **asignaciones no se cachean**: son el dato que el usuario edita y debe venir fresco en cada entrada a la pantalla.

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| Cachear la vista/VM entre navegaciones | Cero recarga | Rompe el patrón de navegación del shell; estado sucio al volver | ❌ |
| Caché con expiración por tiempo (TTL) | Tolera cambios de esquema en caliente | Complejidad sin beneficio: el catálogo no cambia en runtime | ❌ |
| **Precarga única + caché solo de lo inmutable** | Cambio de rol instantáneo; el dato editable sigue fresco | El catálogo queda fijo hasta reiniciar la app | ✅ |
| Cachear también las asignaciones | Aún más rápido | Dos usuarios editando roles se pisarían sin darse cuenta | ❌ |

## Consecuencias

**Se gana:** cambiar de rol es instantáneo y sin red. La segunda visita a la pantalla ya no espera el catálogo.

**Se sacrifica:** si alguien agrega un módulo o una acción en Supabase, **hay que reiniciar la app** para verlo. Es aceptable porque implica una migración de esquema, que ya requiere despliegue.

**Seguimiento:** si en el futuro se agrega CRUD de roles o del catálogo de acciones, **hay que invalidar esas cachés estáticas al guardar**. Hoy no existe ese CRUD, por eso no se implementó la invalidación.

**Consulta nueva:** el conteo de usuarios por rol lee `usuarios` filtrando por `id_estado`. Se hace con `.Select("id_rol")` para no traer columnas de más, y va envuelta en `try/catch` que degrada a diccionario vacío: es información decorativa del selector y un bloqueo de RLS no debe tumbar la pantalla.

---

## Relaciones

- [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]] — de dónde salen estos datos
- [[Sesión 2026-08-11 - Rediseño de Gestión de Roles y esqueleto con shimmer]] — ejecución
- [[Módulo Usuarios]]
- [[Arquitectura Actual]]
