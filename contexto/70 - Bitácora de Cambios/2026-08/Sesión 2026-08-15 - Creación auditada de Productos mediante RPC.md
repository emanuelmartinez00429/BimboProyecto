---
title: Sesión 2026-08-15 — Creación auditada de Productos mediante RPC
type: sesion
status: vigente
tags:
  - sesion
  - productos
  - supabase
  - rpc
  - bitacora
date: 2026-08-15
updated: 2026-08-15
summary: "La creación de productos dejó de hacer un INSERT directo desde C#. Ahora usa ingresarproductotablabitacora, que crea el producto y su registro de bitácora en la…"
scope:
  - CapaDatos/Repositories/Productos
symbols:
  - CreateAsync
  - DeleteAsync
  - IProductoRepository
  - IUsuarioSesionService
  - ProductoDto
  - ProductoModal
  - Result<int>
  - UpdateAsync
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-08-15 — Creación auditada de Productos mediante RPC

> [!success] Resultado
> La creación de productos dejó de hacer un `INSERT` directo desde C#. Ahora usa
> `ingresar_producto_tabla_bitacora`, que crea el producto y su registro de
> bitácora en la misma transacción y devuelve el nuevo `id_producto`.

---

## Problema / motivo

`ProductoCrudRepository.CreateAsync` insertaba directamente en `productos`, por
lo que ese camino no utilizaba la función de base de datos encargada de validar
al usuario, comprobar `Crear Producto` y escribir la auditoría asociada.

## Cambios aplicados

- `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs` ahora recibe
  `IUsuarioSesionService` por constructor y rechaza la creación si no hay sesión.
- `CreateAsync` mapea los doce campos del `ProductoDto` y
  `SesionActual.IdUsuario` a los trece parámetros exactos de la RPC.
- Los IDs y valores opcionales se envían como `NULL`, nunca como `0`.
- La respuesta escalar se valida y convierte a `int`; una respuesta vacía,
  inválida o no positiva se reporta mediante el `Result<int>` existente.
- Se conservó la firma de `IProductoRepository`, el DTO y el flujo del modal.

La función vigente fue inspeccionada en Supabase antes del cambio: retorna
`integer`, corre como `SECURITY INVOKER`, compara el usuario indicado con
`auth.uid()`, exige `acciones.id_accion = 1` (`Crear Producto`) y ejecuta ambos
`INSERT` dentro de la misma llamada.

## Verificación

- `dotnet build BimboProyecto.sln --no-incremental` → 0 errores y 55
  advertencias preexistentes.
- `git diff --check` → sin errores de espacios antes de documentar.
- La definición y el retorno de la RPC se verificaron contra el proyecto
  Supabase Bimbo en modo de solo lectura.
- No se ejecutó una creación real para evitar insertar datos de prueba; queda
  pendiente confirmar en runtime un alta válida y consultar producto + bitácora
  por el ID retornado.

## Lo que NO cambió

- No se modificó la función SQL, el esquema, las políticas RLS ni las migraciones.
- `UpdateAsync` y `DeleteAsync` siguen usando sus actualizaciones actuales.
- No se modificaron `ProductoDto`, `IProductoRepository` ni `ProductoModal`.

---

## Relaciones

- [[Módulo Productos]]
- [[Arquitectura Actual]]
- [[Módulo Usuarios]] — fuente única de sesión mediante `IUsuarioSesionService`
