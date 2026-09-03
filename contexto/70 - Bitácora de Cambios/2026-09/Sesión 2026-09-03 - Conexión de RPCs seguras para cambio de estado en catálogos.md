---
title: "Sesión 2026-09-03 — Conexión de RPCs seguras para cambio de estado en catálogos"
tags:
  - sesion
  - catalogos
  - rpc
  - supabase
  - bugfix
  - rbac
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente)
---

# Sesión 2026-09-03 — Conexión de RPCs seguras para cambio de estado en catálogos

> [!success] Resultado
> Diagnóstico completo y resolución de la falla de activación/desactivación en los catálogos administrativos (**Categorías**, **Productos**, **Proveedores** y **Fabricantes**). Se descartó cualquier relación con el sistema de caché (FusionCache) y se conectaron las RPCs seguras `cambiar_estado_*_seguro` en los repositorios de C# y modales de edición. Compilación limpia (0 errores, 0 advertencias) y 243/243 pruebas unitarias superadas.

---

## Diagnóstico y Causa Raíz

### 1. Descarte de la Hipótesis de Caché
- Se verificó que las grillas de los catálogos cargan directo desde Supabase mediante `GetPagedAsync()` sin intermediación de caché.
- `FusionCache` únicamente aplica en selectores de modales (`CachedCatalogoRepository`) y en sugerencias de autocompletado (`BuscarSugerenciasAsync`).
- El módulo **Presentaciones** cuenta con la misma infraestructura de caché y funcionaba correctamente, comprobando que la caché no era la causa.

### 2. Causa Raíz Real: Desacople en Commit `bb506f0`
- En el commit `bb506f0` (*noti*, 2026-09-02) se migraron las mutaciones a RPCs de seguridad para RBAC y auditoría.
- Las funciones PostgreSQL `actualizar_*_seguro` intencionalmente omitieron las columnas de estado (`estado_categoria`, `id_estado`), delegando esa responsabilidad a funciones especializadas `cambiar_estado_*_seguro`.
- En la capa C#, los repositorios llamaban a `actualizar_*_seguro` omitiendo el estado, y los modales solo llamaban a `UpdateAsync`.
- En consecuencia, al cambiar de "Activo" a "Inactivo" en los modales, la operación retornaba éxito pero el estado nunca se modificaba en la base de datos.
- **Presentaciones** no se había visto afectada porque nunca fue migrada a RPCs en aquel commit y continuaba usando `UPDATE` directo vía Postgrest.

---

## Cambios Aplicados

### 1. Interfaces (`CapaAplicacion4`)
Se agregó la declaración del método de cambio de estado en:
- `ICategoriaRepository`: `Task<Result> CambiarEstadoAsync(int id, bool nuevoEstado, Guid idSolicitud, CancellationToken ct = default);`
- `IProductoRepository`: `Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default);`
- `IProveedorRepository`: `Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default);`
- `IFabricanteRepository`: `Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default);`

### 2. Repositorios (`CapaDatos/Repositories/`)
Se implementó `CambiarEstadoAsync` invocando exclusivamente las funciones RPC de PostgreSQL con sus respectivos parámetros y `idSolicitud`:
- `CategoriaCrudRepository` -> `client.Rpc("cambiar_estado_categoria_seguro", ...)`
- `ProductoCrudRepository` -> `client.Rpc("cambiar_estado_producto_seguro", ...)`
- `ProveedorCrudRepository` -> `client.Rpc("cambiar_estado_proveedor_seguro", ...)`
- `FabricanteCrudRepository` -> `client.Rpc("cambiar_estado_fabricante_seguro", ...)`

Asimismo, `DeleteAsync` en cada repositorio fue refactorizado para reutilizar `CambiarEstadoAsync` con estado inactivo.

### 3. Modales de Edición (`CapaUI/.../Pantallas/`)
En los modales `CategoriaModal`, `ProductoModal`, `ProveedorModal` y `FabricanteModal`:
- Al presionar **Guardar** en modo edición (`!_esNuevo`), se verifica si el estado seleccionado difiere del estado original (`dto.Estado != _original.Estado`).
- De haber cambiado, se invoca `CambiarEstadoAsync`, disparando la auditoría y las notificaciones RBAC correspondientes.
- Si solo se modificaron campos informativos (nombre, descripción, etc.), no se llama a la RPC de estado, protegiendo contra peticiones innecesarias y requerimientos indebidos de permisos de cambio de estado.

---

## Verificación

1. **Compilación de la Solución:**
   ```bash
   dotnet build BimboProyecto.sln
   ```
   *Resultado:* Compilación correcta. 0 Advertencias, 0 Errores.
2. **Pruebas Unitarias:**
   ```bash
   dotnet test BimboProyecto.sln
   ```
   *Resultado:* 243 de 243 pruebas superadas (100%).

---

## Relaciones

- [[Módulos de Catálogos Administrativos]]
- [[Módulo Productos]]
- [[Plan de Migración de Mutaciones Directas a RPC]]
- [[Plan de Migración de Presentaciones a RPC segura]]
- [[Deuda Técnica - Pendientes]]
