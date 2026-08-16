---
title: "Sesión 2026-08-15 — Creación auditada de Empleados mediante RPC"
tags:
  - sesion
  - empleados
  - supabase
  - rpc
  - bitacora
date: 2026-08-15
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex
---

# Sesión 2026-08-15 — Creación auditada de Empleados mediante RPC

> [!success] Resultado
> La creación de empleados dejó de ejecutar un `INSERT` directo desde C# y ahora
> usa `ingresar_empleado_tabla_bitacora`, que crea el empleado y su auditoría en
> una sola transacción.

---

## Problema / motivo

`EmpleadoCrudRepository.CreateAsync` insertaba directamente en `empleados` y no
pasaba por la función que valida la identidad de la sesión, exige el permiso de
creación y registra la operación en `bitacora`.

## Cambios aplicados

- `CapaDatos/Repositories/Empleados/EmpleadoCrudRepository.cs` ahora recibe
  `IUsuarioSesionService` por constructor.
- `CreateAsync` obtiene `SesionActual.IdUsuario` y rechaza el alta si no hay una
  sesión activa.
- Los campos del `EmpleadoDto` se mapean a los siete parámetros exactos de
  `ingresar_empleado_tabla_bitacora`.
- La respuesta escalar se valida y convierte al nuevo `id_empleado`; una
  respuesta vacía, inválida o no positiva se devuelve como error mediante el
  `Result<int>` vigente.

La función real se verificó en Supabase antes del cambio: retorna `integer`, usa
`SECURITY INVOKER`, permite ejecución a `authenticated`, compara el usuario con
`auth.uid()` y exige `acciones.id_accion = 5` (`Crear Empleado`).

## Verificación

- `dotnet build BimboProyecto.sln --no-incremental` → 0 errores y 55
  advertencias preexistentes.
- `git diff --check` → sin errores de espacios antes de documentar.
- La firma, el retorno y la seguridad de la RPC se consultaron en Supabase en
  modo de solo lectura.
- No se insertó un empleado de prueba. Queda pendiente realizar un alta válida
  desde la aplicación y comprobar empleado + bitácora mediante el ID retornado.

## Lo que NO cambió

- No se modificó la función SQL, el esquema, RLS ni las migraciones.
- No se modificaron `EmpleadoDto`, `IEmpleadoRepository` ni `EmpleadoModal`.
- `UpdateAsync` y `CambiarEstadoAsync` mantienen sus actualizaciones actuales.
- El flujo **Crear Usuario** y la RPC `crear_usuario_empleado_seguro` quedaron
  completamente intactos.

---

## Relaciones

- [[Módulo Empleados]]
- [[Módulo Usuarios]]
- [[Arquitectura Actual]]
