---
title: Sesión 2026-08-15 - Protección contra autoadministración de usuarios
type: sesion
status: vigente
tags:
  - sesion
  - usuarios
  - seguridad
  - rls
date: 2026-08-15
updated: 2026-08-15
summary: Impedir que la sesión actual cambie su propio rol o estado desde el módulo Usuarios.
scope:
  - CapaDatos/Repositories/Usuarios
  - CapaUI/Formularios/Principal/Pantallas/Usuarios
symbols:
  - ActualizarUltimoAccesoAsync
  - IdUsuario
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-08-15 - Protección contra autoadministración de usuarios

## Objetivo

Impedir que la sesión actual cambie su propio rol o estado desde el módulo Usuarios.

## Trabajo realizado

- Se agregó detección por `IdUsuario` de sesión en ViewModel y repositorio.
- Se deshabilitaron las acciones y se añadió explicación visible para la propia fila.
- El doble clic ahora respeta `EditarCommand.CanExecute`.
- Se aplicó la migración remota `protect_usuario_self_role_and_status`.
- Se sustituyó la política UPDATE abierta por una autenticada y se añadió protección por campo/permiso.
- `ActualizarUltimoAccesoAsync` se conservó intacto.

## Archivos modificados

- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosViewModel.cs`.
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml` y `.xaml.cs`.
- `CapaDatos/Repositories/Usuarios/UsuarioRepository.cs`.

## Decisiones

- Usar `IdUsuario` de la sesión en C# y `auth.uid()` contra `uuid_usuario` en PostgreSQL.
- No permitir excepciones por rol.
- Proteger solamente `id_rol` e `id_estado` para conservar la actualización legítima de `ultimo_acceso`.

## Pruebas y validaciones

- Build: 0 errores; warnings preexistentes.
- XAML válido y `git diff --check` limpio.
- Prueba SQL transaccional: rol y estado propios rechazados; `ultimo_acceso` propio permitido; rollback ejecutado.
- Trigger, función con `SECURITY INVOKER`/`search_path` fijo y política autenticada verificados.
- Asesores de Supabase ejecutados; sin hallazgos nuevos atribuibles a esta migración. Persisten advertencias preexistentes en otras funciones/tablas.

## Problemas encontrados

- La política anterior `update_Usuarios` estaba abierta a `public` con condiciones verdaderas; se reemplazó por una política para `authenticated` con autorización real.

## Pendientes

- Prueba visual manual de la advertencia y los botones deshabilitados con la propia fila seleccionada.

## Próximo paso recomendado

Ejecutar la prueba visual anterior con una sesión administrativa; no se requieren más cambios de base para esta corrección.

## Relaciones

- [[Módulo Usuarios]]
- [[ADR-020 - Defensa en profundidad contra autoadministracion de usuarios]]
