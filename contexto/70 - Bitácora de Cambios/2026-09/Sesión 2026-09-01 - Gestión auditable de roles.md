---
title: "Sesión 2026-09-01 - Gestión auditable de roles"
tags: [sesion, bimbo, roles, rbac, supabase]
date: 2026-09-01
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex
---

# Sesión 2026-09-01 - Gestión auditable de roles

> [!success] Resultado
> Se implementó la administración del catálogo de roles y permisos con autorización específica, auditoría transaccional y un núcleo administrativo protegido para el rol Administrador.

---

## Problema / motivo

La pantalla anterior solo editaba asignaciones y reutilizaba `Modificar Configuración`. El esquema tampoco distinguía roles activos ni un rol de sistema, y las tablas RBAC admitían escrituras directas demasiado amplias.

## Cambios aplicados

- `CapaUI/.../Roles/`: dos apartados, catálogo y permisos; modal, filtros, estados, conteos y candados.
- `Permiso.cs`: incorporación de las seis acciones nuevas de roles.
- Contratos y repositorios para roles, reemplazo atómico de permisos y asignación separada de rol a usuario.
- `UsuarioModal`: cada campo sensible usa su acción específica; el rol exige `Asignar Rol a Usuario`.
- Supabase: `roles.id_estado`, `roles.es_sistema`, restricciones del Administrador, baja lógica bloqueada con usuarios, cinco RPC, bitácora y grants/RLS de solo lectura directa.

## Verificación

- `dotnet build BimboProyecto.sln --no-restore` → 0 errores, 0 advertencias.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-build --no-restore` → 113/113 pruebas superadas.
- Prueba transaccional Supabase con rollback → crear, renombrar, reemplazar permisos, desactivar, cuatro auditorías y rechazo de desactivación del Administrador.
- Siete acciones protegidas activas; políticas SELECT para `authenticated`; RPC `SECURITY DEFINER` autorizadas internamente.

## Lo que NO cambió

- No se creó commit ni se hizo push.
- No se tocaron los planes offline-first ni cambios locales preexistentes de Pesaje.
- La validación visual autenticada de WPF queda pendiente de ejecución manual.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Usuarios]]
- [[ADR-023 - Rol Administrador de sistema con nucleo de permisos protegido]]
