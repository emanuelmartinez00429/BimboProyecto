---
title: "Sesión 2026-09-02 - Auditoría Notificaciones y fix permiso fantasma USUARIOS_ACTIVAR"
tags: [sesion, bimbo, notificaciones, rbac, supabase, bugfix]
date: 2026-09-02
branch: feat/fase8-MaquetadodeRoles
autor_cambios: opencode (sesión gestionada por Emanuel)
---

# Sesión 2026-09-02 — Auditoría Notificaciones y fix permiso fantasma USUARIOS_ACTIVAR

> [!success] Resultado
> Se auditó el submódulo de Notificaciones sobre el SQL y C# reales y se corrigió un bug crítico de backend: la reactivación de usuarios estaba bloqueada porque `cambiar_estado_usuario_seguro` validaba contra un permiso (`USUARIOS_ACTIVAR`) que no existe en `public.acciones`.

---

## Problema / motivo

Dos tareas encadenadas: (1) revisión completa del contexto del sistema, (2) auditoría del funcionamiento real del submódulo de Notificaciones (no solo documentación). Durante la auditoría se detectó que el hotfix RBAC introdujo una referencia a un permiso inexistente.

## Cambios aplicados

- **`supabase/migrations/20260902140000_hotfix_notificaciones_seguridad_rbac.sql`** (líneas ~1384-1390): en `cambiar_estado_usuario_seguro`, `v_codigo_accion := 'USUARIOS_ACTIVAR'` → `'USUARIOS_MODIFICAR'` al activar (`p_id_estado = 1`). Se aplicó el patrón de Proveedor: activar reutiliza `*_MODIFICAR`, desactivar usa `*_ELIMINAR`. Se añadió comentario explicativo.
- **`supabase/migrations/20260902140000_hotfix_notificaciones_seguridad_rbac_supermin.sql`**: misma corrección. **Imprescindible**: al compartir el prefijo `20260902140000_` con el legible y ejecutarse alfabéticamente después, su `CREATE OR REPLACE` es el que queda vigente en la BD al desplegar; corrigiendo solo el legible el despliegue seguiría roto.

## Verificación

- Confirmado por grep que `USUARIOS_ACTIVAR` solo aparece como referencia (en hotfix y en los borradores `out.sql`/`temp_5.sql`), nunca como `INSERT` en `public.acciones`.
- Confirmado que `USUARIOS_MODIFICAR` existe (`id_accion 24`, migración `20260902115932`), por lo que `preparar_solicitud_rpc` resuelve el `id_accion` correcto para auditoría.
- Patrón del proyecto verificado en `20260902071800_rpc_proveedor_fabricante.sql`: proveedor activa con `PROVEEDORES_MODIFICAR` (id 15) y fabricante usa `FABRICANTES_MODIFICAR` para cambios de estado (lo dice en comentario).

## Lo que NO cambió

- No se tocó el C# (`CapaUI/Core/Permisos/Permiso.cs`): la sesión carga permisos desde la BD (no del enum), y no existe `Permiso.ActivarUsuario`; el bloqueo era 100% backend.
- No se limpiaron los borradores `supabase/migrations/temp_*.sql` ni `out.sql`; siguen conteniendo el código antiguo pero son sobrescritos por el hotfix en el despliegue. Quedan como deuda de limpieza.
- No se tocaron los demás hallazgos de la auditoría (ver resumen en memoria/deuda potencial).

---

## Relaciones

- [[Módulo Notificaciones]]
- [[Módulo Usuarios]]
- [[Deuda Técnica - Pendientes]] — si se registran ítems por los borradores/patrón espejo
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]]
- [[Arquitectura Actual]]
