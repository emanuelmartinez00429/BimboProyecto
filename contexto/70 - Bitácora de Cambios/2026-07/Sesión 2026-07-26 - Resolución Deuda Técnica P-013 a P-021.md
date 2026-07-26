---
title: "Sesión 2026-07-26 — Resolución Deuda Técnica P-013 a P-021"
tags: [sesion, deuda-tecnica, usuarios, pesaje, seguridad, supabase]
date: 2026-07-26
branch: claude/en-que-estaba-78b882
autor_cambios: Claude (Fable 5), dirigido por Fernando
---

# Sesión 2026-07-26 — Resolución Deuda Técnica P-013 a P-021

Se resolvieron los **8 ítems pendientes** de la revisión QA del commit `f105047` ([[Sesión 2026-07-23 - Revisión QA Módulo Usuarios y Refactor de Sesión (Emanuel)]]), un commit por ítem/fase para poder revertir individualmente. Todos con `dotnet build` 0 errores.

## Cambios

| Ítem | Commit | Qué se hizo |
|---|---|---|
| P-013 🔴 | `fix(P-013)` | `PesajeViewModel.UsuarioActual` lanza excepción si no hay sesión (fail-loud) + guarda `HaySesionActiva()` con Toast genérico y `Serilog.Log.Warning` en registrar camión / guardar pesaje |
| P-014 | `fix(P-014,P-015)` | Eliminados 3 `Debug.WriteLine` con prefijos de access token en `CrearAsync`; queda un `Log.Debug` booleano. Nueva regla §2.1b en [[Plan de Seguridad - Roadmap 10-10]] |
| P-015 | ídem | Eliminado el diagnóstico FK por fila en `MapToDto` |
| P-016 | `fix(P-016,P-017)` | `Normalizar()` reescrito: chars FormD → filtrar `NonSpacingMark` → FormC. Verificado `Muñoz→munoz`. Ver [[NET - Normalizacion Unicode para Quitar Acentos]] |
| P-017 | ídem | `IUsuarioSesionService` eliminado del constructor de `UsuarioModal` + 2 llamadores |
| P-018 | `fix(P-018)` | Contrato enum↔BD documentado en `Permiso.cs` + `SesionPermisos.ValidarContraBD()` (Serilog, diagnóstico, no bloquea) invocado tras login |
| P-021 | `feat(P-021)` | Vista SQL `vista_usuarios_busqueda` (`security_invoker=true`) aplana `nombre_completo`; OR server-side alias+nombre en página y conteos. Ver [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] |
| P-019 | `refactor(P-019)` | Propiedad `correoUsuario` → `aliasUsuario` (5 archivos); `[Column]` y BD intactos |

## Cambios en la base de datos

- Migración `crear_vista_usuarios_busqueda` aplicada al proyecto Supabase `Bimbo_Pesaje` (Postgres 17). Vista con `security_invoker = true` — verificado que respeta RLS y que los grants estándar están presentes.

## Correcciones a la bóveda

- **`contexto/CLAUDE.md`** documentaba el patrón OR roto (`Filter("or", Op.Equals, ...)`) como convención — corregido al patrón `.Or()` + wikilinks a [[Bug - Filter OR con Op.Equals en postgrest-csharp]] y [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]]. Era una trampa activa para cualquier agente.
- Notas nuevas: [[Supabase - Vistas SQL, RLS y security_invoker]] y [[NET - Normalizacion Unicode para Quitar Acentos]] en `50 - Referencia`.
- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] en `45 - Decisiones`.

## Verificación pendiente del usuario (runtime)

1. Login → revisar `%AppData%\BimboPesaje\Logs\` por advertencias `Permisos:` (P-018 detectando desalineaciones reales enum↔BD).
2. Vista Usuarios → buscar por nombre de empleado ("Emanuel", "Fernando") y confirmar resultados + conteos.
3. Modal crear usuario → seleccionar empleado con ñ/acentos y confirmar email generado.
4. Confirmar que los **embeds** `roles(*), empleados(*)` funcionan sobre la vista (si fallara, plan B documentado en el ADR: aplanar `nombre_rol` en la vista).

## Deuda detectada NO resuelta

- `contexto/CLAUDE.md` aún describe `BimboPesaje/` y `CapaServicios/` (eliminados según [[ADR-003 - Disolución de CapaServicios]]) — desactualización parcial, queda para otra sesión.

## Relaciones

- [[Deuda Técnica - Pendientes]] — ítems tachados + historial actualizado
- [[Sesión 2026-07-23 - Revisión QA Módulo Usuarios y Refactor de Sesión (Emanuel)]] — origen de los ítems
- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]]
- [[Plan de Seguridad - Roadmap 10-10]]
- [[Paginación y Búsqueda - Arquitectura Detallada]]
