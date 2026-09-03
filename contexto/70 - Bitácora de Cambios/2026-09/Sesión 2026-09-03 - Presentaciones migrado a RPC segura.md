---
title: "Sesión 2026-09-03 — Presentaciones migrado a RPC segura"
tags:
  - sesion
  - supabase
  - rpc
  - rbac
  - auditoria
  - catalogos
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando
---

# Sesión 2026-09-03 — Presentaciones migrado a RPC segura

> [!success] Resultado
> Presentaciones dejó de ser el único catálogo sin migrar. Se aplicó la migración `presentaciones_rpc_segura` en Supabase (`bzmmrifjgzlvsphctais`): tres funciones `SECURITY DEFINER` idempotentes con RBAC por código, set de acciones `PRESENTACIONES_*`, retiro del trigger de auditoría legacy y cierre del DML directo. `CapaDatos` + `CapaAplicacion4` compilan 0/0 y **243/243 pruebas** pasan. La parte de UI (`PresentacionModal`) quedó escrita pero **sin compilar ni probar** por bloqueo de la app en ejecución.

---

## Problema / motivo

Auditoría de las RPC de catálogos (ver [[Plan de Migración de Presentaciones a RPC segura]]): Categoría, Proveedor y Fabricante ya tenían la familia `crear_/actualizar_/cambiar_estado_<x>_seguro`; **Presentación seguía en la ruta legacy**:

- `CreateAsync` → `ingresar_presentacion_tabla_bitacora`, `SECURITY INVOKER`, validando `id_accion = 1` (`PRODUCTOS_CREAR`) porque no existía ninguna acción `PRESENTACIONES_*`; auditaba como módulo Productos.
- `UpdateAsync` / `DeleteAsync` → `.Update()` DML directo desde C#.
- `presentacion_producto` y `bitacora` eran las únicas tablas de catálogo con `INSERT/UPDATE/DELETE` directo abierto a `authenticated` **y `anon`**.
- El trigger `trg_upd_presentacion` auditaba por su cuenta exigiendo `id_accion = 2`, lo que con una RPC `_seguro` habría dado doble bitácora y `42501` reversivo.

## Cambios aplicados

### Base de datos — migración `presentaciones_rpc_segura`

1. **Acciones `PRESENTACIONES_*`** (`id_accion` 43–47, `id_modulo = 1`): `CREAR`, `MODIFICAR`, `DESACTIVAR`, `ACTIVAR`, `CONSULTAR`. Espejo de `CATEGORIAS_*`. El trigger `private.asignar_accion_nueva_administrador` las propagó automáticamente al rol Administrador (verificado: `admin_tiene = true` en las 5).
2. **`crear_presentacion_seguro(varchar, varchar, uuid) → jsonb`** — `SECURITY DEFINER`, `search_path` fijo, permiso `PRESENTACIONES_CREAR`, idempotencia por `private.preparar_solicitud_rpc`, auditoría `private.registrar_auditoria_rbac`, notificación, `private.completar_solicitud_rpc`.
3. **`actualizar_presentacion_seguro(int, varchar, varchar, uuid) → jsonb`** — permiso `PRESENTACIONES_MODIFICAR`, `SELECT … FOR UPDATE`, no-op check, no toca `id_estado`.
4. **`cambiar_estado_presentacion_seguro(int, int, uuid) → jsonb`** — permiso `PRESENTACIONES_DESACTIVAR` / `PRESENTACIONES_ACTIVAR` según destino.
5. **`DROP TRIGGER trg_upd_presentacion`** sobre `presentacion_producto`. Se conserva `trg_presentacion_updated_at`. La función `log_upd_presentacion()` queda en el esquema para poder revertir.
6. **`REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON public.presentacion_producto FROM authenticated, anon`**.
7. **`GRANT EXECUTE`** de las 3 funciones a `authenticated, service_role`; `REVOKE` de `anon, public`.
8. `ingresar_presentacion_tabla_bitacora` marcada como **DEPRECADA** vía `COMMENT` (pendiente `DROP` cuando el C# deje de invocarla — ya no lo hace tras esta sesión).

> **Paso 8 del plan (revocar DML de `bitacora` a `authenticated`/`anon`) NO se aplicó** — es un cambio transversal, decisión pendiente de Fernando.

### C# — capa de datos (compila 0/0, 243/243 tests)

- `CapaAplicacion4/Presentaciones/Interfaces/IPresentacionRepository.cs`: `CreateAsync`/`UpdateAsync`/`DeleteAsync` ahora reciben `Guid idSolicitud`; nuevo `CambiarEstadoAsync(int id, int nuevoIdEstado, Guid idSolicitud, ct)`.
- `CapaDatos/Repositories/Presentaciones/PresentacionCrudRepository.cs`:
  - `CreateAsync` → `Rpc("crear_presentacion_seguro", {p_nombre_presentacion, p_descripcion_presentacion, p_id_solicitud})`, parseo del `jsonb` con el helper `ObtenerIdCreado(json, entidad, jsonKey)` (mismo que `CategoriaCrudRepository`).
  - `UpdateAsync` → `Rpc("actualizar_presentacion_seguro", …)`.
  - `CambiarEstadoAsync` → `Rpc("cambiar_estado_presentacion_seguro", …)`.
  - `DeleteAsync` → delega en `CambiarEstadoAsync(id, EstadoRegistro.Inactivo, …)`.
  - **0** `.Update()` / `.Insert()` directos en el repositorio.

### C# — UI (escrito, SIN compilar ni probar)

- `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionModal.xaml.cs`: se agregó `SolicitudIdempotente _solicitud`; `BtnGuardar_Click` pasa `_solicitud.Obtener(...)` a las tres llamadas, hace `CambiarEstadoAsync` aparte cuando el estado cambió (igual que `CategoriaModal`), y llama `_solicitud.Confirmar()` al éxito.

## Verificación

- `dotnet build CapaDatos/CapaDatos.csproj` → **0 errores, 0 advertencias**.
- `dotnet test BimboProyecto.Tests` → **243/243 correctas**.
- Verificación remota en Supabase: 3 funciones `prosecdef = true`, `EXECUTE` solo `authenticated`/`service_role`; 5 acciones creadas y asignadas al Administrador; `trg_upd_presentacion` retirado; `presentacion_producto` sin `INSERT/UPDATE/DELETE` para `authenticated`/`anon`.
- ❌ **`CapaUI` no se pudo compilar**: la app estaba en ejecución (PID 17168) y Visual Studio 2022 tenía bloqueados los DLLs de salida (`MSB3021`). No hay errores de C# en el código nuevo; el bloqueo es de copia de archivos.

## Pendiente (requiere la app cerrada + prueba manual)

1. `dotnet build BimboProyecto.sln` completo con la app y VS cerrados → confirmar `PresentacionModal` 0/0.
2. Prueba manual con un rol que tenga solo `PRESENTACIONES_*` (sin `PRODUCTOS_*`): crear, editar, desactivar y reactivar una presentación → debe funcionar y auditar como Presentación, una sola fila de bitácora por operación.
3. El Administrador debe **re-login** para que su sesión tome las acciones `PRESENTACIONES_*` (ADR-024, sección Riesgos).
4. Cuando 1–3 estén verdes: `DROP FUNCTION ingresar_presentacion_tabla_bitacora`.
5. P-052 (doble-log latente en categoría/proveedor/fabricante) sigue pendiente — fuera del alcance de esta sesión.

## Relaciones

- [[Plan de Migración de Presentaciones a RPC segura]] — plan y SQL de esta migración
- [[Plan de Migración de Mutaciones Directas a RPC]] — plan maestro
- [[Deuda Técnica - Pendientes]] — P-052
- [[Sesión 2026-08-16 - Creación auditada de catálogos y contactos mediante RPC]] — migración parcial que dejó fuera a Presentaciones
- [[Módulos de Catálogos Administrativos]]
- [[ADR-024 - Rol Administrador inmutable con acceso total]]
- [[Arquitectura Actual]]
