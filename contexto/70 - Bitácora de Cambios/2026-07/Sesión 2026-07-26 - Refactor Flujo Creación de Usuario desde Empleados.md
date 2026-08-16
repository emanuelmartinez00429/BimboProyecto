---
title: "Sesión 2026-07-26 - Refactor Flujo Creación de Usuario desde Empleados"
tags: [sesion, bimbo, gestion-usuarios, empleados, refactor, ux]
date: 2026-07-26
branch: main
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-07-26 - Refactor Flujo Creación de Usuario desde Empleados

## Resultado

Se refactorizó el flujo de creación de usuarios para que se lance desde el módulo de Empleados en vez de desde Usuarios. El botón "+Nuevo usuario" se eliminó de UsuariosView y se agregó "Crear Usuario" en EmpleadosView.

## Problema

El flujo anterior requería que el usuario fuera al módulo de Usuarios, diera click en "+Nuevo usuario", y seleccionara un empleado de un ComboBox. Esto era confuso porque:
- El usuario primero tenía que ir a Empleados para ver quiénes existían
- Luego ir a Usuarios para crear el usuario
- El ComboBox cargaba todos los empleados sin usuario (query adicional innecesaria)

El flujo correcto: el usuario está en Empleados, selecciona un empleado, y crea el usuario directamente desde ahí con los datos pre-cargados.

## Cambios realizados

### Archivos modificados (8)

| Archivo | Cambio |
|---------|--------|
| `UsuarioModal.xaml` | Agregado `TxtEmpleadoNombre` TextBlock para mostrar nombre del empleado readonly |
| `UsuarioModal.xaml.cs` | Nueva sobrecarga de constructor con `idEmpleado`, `nombreEmpleado`, `correoEmpleado`. OnLoaded usa datos pre-cargados en vez de ComboBox |
| `EmpleadosView.xaml` | Nuevo botón púrpura "Crear Usuario" en la barra de acciones |
| `EmpleadosView.xaml.cs` | Handler `AbrirModalCrearUsuario` que abre UsuarioModal con empleado pre-seleccionado |
| `EmpleadosViewModel.cs` | Nuevo `[RelayCommand] CrearUsuario()` con `SolicitarCrearUsuario` event |
| `UsuariosView.xaml` | Eliminado botón "+Nuevo usuario" |
| `UsuariosViewModel.cs` | Eliminado comando `Nuevo()` y evento `SolicitarNuevo` |
| `UsuariosView.xaml.cs` | Eliminado método `AbrirModalNuevo()` y suscripciones a `SolicitarNuevo` |

## Flujo nuevo

```
EmpleadosView (DataGrid: correo, nombre, apellido)
    │
    ├── Click en fila → se habilitan botones
    │   ├── Editar
    │   ├── Deshabilitar
    │   └── ★ "Crear Usuario"
    │
    └── "Crear Usuario" → UsuarioModal
            ├── Nombre empleado (readonly, pre-cargado)
            ├── Correo empleado (readonly, pre-cargado)
            ├── Contraseña (editable)
            └── Rol (ComboBox, editable)
```

## Verificación

- `dotnet build BimboProyecto.sln` → **0 errores, 0 warnings**

## Lo que no cambió

- RPC `crear_usuario_empleado_seguro` — se mantiene igual
- Modo edición de usuarios — se mantiene igual
- Tabla `empleados` en BD — sin cambios
- Módulo de Empleados sigue siendo solo lectura para Guardar (el "Crear Usuario" es un flujo aparte)

## Relaciones

- [[Módulo Usuarios]] — módulo afectado
- [[Módulo Empleados]] — módulo que ahora lanza la creación
- [[ADR-008 - Cliente Temporal para SignUp de Usuarios]] — flujo de creación preservado
- [[ADR-009 - RPC crear_usuario_empleado_seguro]] — RPC sin cambios
