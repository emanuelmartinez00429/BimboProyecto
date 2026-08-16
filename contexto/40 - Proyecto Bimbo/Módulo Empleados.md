---
title: "Módulo Empleados"
tags: [bimbo, modulo, empleados, crud]
date: 2026-07-26
---

# Módulo Empleados

> CRUD completo de empleados: crear, editar, cambiar estado. Los empleados están vinculados al módulo de Usuarios — desde Empleados se puede crear un usuario para un empleado seleccionado.

## Tabla `empleados` (Supabase)

| Columna | Tipo | Notas |
|---------|------|-------|
| `id_empleado` | int (PK) | Auto-increment |
| `nombre_empleado` | varchar | |
| `apellido_empleado` | varchar | |
| `numero_identidad` | varchar | |
| `telefono_empleado` | varchar | |
| `correo_empleado` | varchar | |
| `id_estado` | int | 1 = activo, 2 = inactivo |
| `created_at` | timestamptz | |
| `updated_at` | timestamptz | |

## Archivos del Módulo

### CapaAplicacion
```
Empleados/Interfaces/IEmpleadoRepository.cs  — GetPagedAsync, BuscarSugerenciasAsync,
                                                GetPaginaDeRegistroAsync,
                                                CreateAsync, UpdateAsync, CambiarEstadoAsync
Empleados/Dtos/EmpleadoDto.cs                — IdEmpleado, NombreEmpleado, ApellidoEmpleado,
                                                NumeroIdentidad, TelefonoEmpleado, CorreoEmpleado, IdEstado
Empleados/Queries/EmpleadoFiltros.cs         — { int? IdEstado }
```

### CapaDatos
```
Repositories/Empleados/EmpleadoCrudRepository.cs  — implementa IEmpleadoRepository
                                                     namespace: GestionEmpleados (ver nota)
Modelados/Usuarios/Empleados.cs                    — modelo Supabase, hereda BaseModel
```

> [!bug] Namespace: `GestionEmpleados`, no `Empleados`
> El modelo `CapaDatos.Modelados.Usuarios.Empleados` se usa sin calificar en otros repos.
> Un namespace hermano "Empleados" gana la resolución y rompe `CS0118`.

### CapaUI
```
Empleados/EmpleadosViewModel.cs    — patrón UsuariosViewModel (sin Realtime, sin filtro Rol)
Empleados/EmpleadosView.xaml(.cs)  — lista + SuggestionSearchBox + paginación + stats
Empleados/EmpleadoModal.xaml(.cs)  — Crear + Editar + Cambiar estado
```

### Navegación
- `Routes.Empleados = "empleados"` en `Routes.cs`
- Botón "Gestión de Empleados" en sidebar bajo submenú Usuarios

## Data Flow

### Crear empleado
```
"+Nuevo" → EmpleadoModal(repo, null)
    → TxtNombre, TxtApellido, TxtIdentidad, TxtTelefono, TxtCorreo (editables)
    → RowEstado oculto
    → BtnGuardar → Validate → EmpleadoDto → _repo.CreateAsync(dto)
    → EmpleadoCrudRepository toma SesionActual.IdUsuario
    → RPC ingresar_empleado_tabla_bitacora
        ├── valida p_usuario_ingresando contra auth.uid()
        ├── exige la acción Crear Empleado
        ├── INSERT en empleados
        ├── INSERT en bitacora
        └── retorna id_empleado
    → Guardado event → View cierra + RefrescarDatos
```

Desde 2026-08-15 el alta ya no hace un `Insert` PostgREST directo. Empleado y
bitácora se crean en la misma función PostgreSQL, por lo que ambos cambios son
atómicos. El usuario de auditoría sale de `IUsuarioSesionService`, no del DTO ni
del formulario; si no hay sesión activa, el repositorio rechaza la operación.

Este cambio aplica únicamente al alta de empleados. `UpdateAsync` y
`CambiarEstadoAsync` conservan sus `UPDATE`, y el flujo separado **Crear Usuario**
continúa usando `crear_usuario_empleado_seguro` sin modificaciones.

### Editar empleado
```
Doble clic en fila → EmpleadoModal(repo, empleadoDto)
    → Campos pre-cargados con datos actuales
    → RowEstado visible (RadioButtons Activo/Inactivo)
    → BtnGuardar → Validate → EmpleadoDto → _repo.UpdateAsync(dto)
    → Update Supabase → Guardado event → View cierra + RefrescarDatos
```

### Cambiar estado
```
Seleccionar fila → "Deshabilitar" button → ToggleEstadoAsync
    → _repo.CambiarEstadoAsync(id, nuevoEstado)
    → Update Supabase (solo id_estado) → CargarPaginaAsync
```

### Crear usuario desde empleado
```
Seleccionar fila → "Crear Usuario" button
    → UsuarioModal(usuarioRepo, rolRepo, idEmpleado, nombre, correo)
    → TxtNombre + TxtEmail readonly, pre-cargados
    → Usuario completa: contraseña + rol
    → RPC crear_usuario_empleado_seguro
```

## Patrones en Uso
- [[Repository Pattern]] — EmpleadoCrudRepository hereda RepositorioBase
- [[Result Pattern]] — TryAsync + Result<T> en todas las operaciones
- [[Base Repository con TryAsync]] — wrapper de error handling
- [[Módulo Productos]] — patrón canónico replicado

## Preguntas Abiertas
1. Empleados no tiene Realtime (no es crítico para catálogos)
2. No hay filtro por rol (empleados no tienen rol — eso es de usuarios)
3. Validación de número de identidad duplicado no implementada

## Relaciones
- [[Módulo Usuarios]] — usuarios se crean desde empleados
- [[Módulo Productos]] — patrón canónico replicado
- [[Arquitectura Actual]] — estado del proyecto
- [[Sesión 2026-07-26 - Módulo Empleados (solo lectura)]] — origen del módulo
- [[Sesión 2026-07-26 - CRUD Completo Módulo Empleados]] — completación del CRUD
