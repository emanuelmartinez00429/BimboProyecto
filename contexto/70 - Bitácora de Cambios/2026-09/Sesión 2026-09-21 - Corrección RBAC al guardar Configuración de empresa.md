---
title: "Sesión 2026-09-21 — Corrección RBAC al guardar Configuración de empresa"
tags:
  - sesion
  - configuracion
  - rbac
  - fix
date: 2026-09-21
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-09-21 — Corrección RBAC al guardar Configuración de empresa

> [!success] Resultado
> El Administrador vuelve a superar la validación local al guardar colores, datos e imágenes de la empresa. La corrección conserva las políticas RLS y no cambia asignaciones ni datos de Supabase.

---

## Problema / motivo

`UsuarioSesion` carga `acciones.codigo_accion` en memoria, pero `EmpresaRepository` consultaba la etiqueta visible `Modificar Configuración`. La lectura era posible, mientras que todo guardado fallaba localmente antes de llegar a Supabase porque esa etiqueta no podía coincidir con `CONFIGURACION_MODIFICAR`.

La base viva confirmó que el rol `Administrador` está activo, es de sistema y tiene activa la asignación `CONFIGURACION_MODIFICAR`. También conserva todas sus acciones; el defecto estaba en el contrato C#.

## Cambios aplicados

- `CapaDatos/Repositories/Empresa/EmpresaRepository.cs`: la guarda local ahora exige `CONFIGURACION_MODIFICAR`.
- `BimboProyecto.Tests/Configuracion/ConfiguracionEmpresaWhiteBoxTests.cs`: nueva regresión que fija el código estable usado por el repositorio.
- `contexto/40 - Proyecto Bimbo/Módulo Configuración de Empresa.md`: se aclaró la separación entre etiqueta visible, código en sesión y validación RLS.

## Verificación

- Pruebas de Configuración y RBAC: 72/72 aprobadas.
- `dotnet build BimboProyecto.sln --no-restore --nologo`: 0 errores y 0 advertencias.
- Consulta de solo lectura en Supabase: `Administrador`, `es_sistema = true`, rol activo y asignación `CONFIGURACION_MODIFICAR` activa.
- Suite completa: 687/691 aprobadas. Los cuatro fallos restantes pertenecen a `PreferenciasInicioSesionServiceTests` y reproducen aisladamente en la persistencia DPAPI; no tocan Configuración ni RBAC.
- `git diff --check`: sin errores; Git únicamente informó conversiones futuras LF a CRLF en archivos del árbol de trabajo.

## Lo que NO cambió

- No se modificaron tablas, funciones, políticas RLS, Storage ni datos de Supabase.
- No se amplió el permiso de ningún rol.
- No se modificaron la vista, los colores, la carga de imágenes ni el flujo de guardado.
- No se tocaron los cambios locales en curso del detalle de Bitácora.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Configuración de Empresa]]
- [[ADR-024 - Rol Administrador inmutable con acceso total]]
