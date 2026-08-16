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

## Alcance

- `UpdateAsync` continúa usando actualizaciones PostgREST.
- La eliminación continúa siendo una baja lógica.
- Los contratos, DTO, ViewModels y modales no cambiaron.
- Fabricantes conserva `IdProveedor` e `IdPais` como valores opcionales en C#.
- Categorías conserva `EstadoCategoria` como booleano.

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
- [[Sesión 2026-08-16 - Creación auditada de catálogos y contactos mediante RPC]]
