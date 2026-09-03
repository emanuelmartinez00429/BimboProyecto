---
title: "Módulos de Catálogos Administrativos"
tags: [modulos, catalogos, supabase, auditoria]
date: 2026-08-16
---

# Módulos de Catálogos Administrativos

Describe el estado vigente de creación de proveedores, fabricantes, categorías y presentaciones.

## Creación auditada

Desde 2026-08-16, sus `CreateAsync` no ejecutan `Insert` PostgREST directo. Los repositorios obtienen `SesionActual.IdUsuario` mediante `IUsuarioSesionService`, rechazan el alta si no existe sesión y llaman estas funciones:

| Módulo | RPC |
|---|---|
| Proveedores | `ingresar_proveedor_tabla_bitacora` |
| Fabricantes | `ingresar_fabricante_tabla_bitacora` |
| Categorías | `ingresar_categoria_tabla_bitacora` |
| Presentaciones | `ingresar_presentacion_tabla_bitacora` |

Cada llamada conserva los valores del DTO, agrega `p_usuario_ingresando` y valida que la respuesta sea una PK entera positiva. El alta del registro y su entrada de bitácora quedan delegadas a la función PostgreSQL correspondiente.

## Alcance y Mutaciones

- **Modificación de atributos:** `UpdateAsync` en Proveedores, Fabricantes y Categorías invoca `actualizar_*_seguro` con `id_solicitud` (solo actualiza campos informativos). Presentaciones conserva PostgREST directo.
- **Cambio de estado y baja lógica:** `CambiarEstadoAsync` y `DeleteAsync` en Proveedores, Fabricantes y Categorías invocan `cambiar_estado_*_seguro` con auditoría y notificaciones RBAC dedicadas.
- Los modales de edición detectan transiciones de estado de forma reactiva e invocan `CambiarEstadoAsync` únicamente si el estado difiere del registro original.
- Fabricantes conserva `IdProveedor` e `IdPais` como valores opcionales en C#.
- Categorías conserva `EstadoCategoria` como booleano.
- Las tablas presentan las columnas de auditoría `CREADO` y `ACTUALIZADO` antes de `ESTADO` alineadas a la derecha con auto-dimensionamiento (`Width="Auto" MinWidth="130"`).

## Archivos clave

- `CapaDatos/Repositories/Proveedores/ProveedorCrudRepository.cs`
- `CapaDatos/Repositories/Fabricantes/FabricanteCrudRepository.cs`
- `CapaDatos/Repositories/Categorias/CategoriaCrudRepository.cs`
- `CapaDatos/Repositories/Presentaciones/PresentacionCrudRepository.cs`

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Productos]]
- [[Módulo Contactos (Drill-down)]]
- [[Módulo Bitácora]]
- [[Columnas de Auditoria Temporal en DataGrid - Estandarizacion Creado y Actualizado]]
- [[Plan de Migración de Mutaciones Directas a RPC]]
- [[Plan de Migración de Presentaciones a RPC segura]]
- [[Sesión 2026-09-02 - Columnas Creado y Actualizado en tablas de catálogo]]
- [[Sesión 2026-08-16 - Creación auditada de catálogos y contactos mediante RPC]]
- [[Sesión 2026-09-02 - Alineación de columnas y fallback universal de campos vacíos]]
- [[Sesión 2026-09-03 - Conexión de RPCs seguras para cambio de estado en catálogos]]
