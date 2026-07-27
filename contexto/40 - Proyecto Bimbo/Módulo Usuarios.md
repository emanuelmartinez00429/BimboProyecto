---
title: "Módulo Usuarios"
tags: [bimbo, modulo, gestion-usuarios, auth, permisos]
date: 2026-07-26
---

# Módulo Usuarios

> CRUD de usuarios con autenticación Supabase, roles/permisos desde BD, y sesión refactorizada (`IUsuarioSesionService`).

## Archivos del Módulo

### CapaAplicacion — Interfaces
- `CapaAplicacion/Usuarios/Interfaces/IRolRepository.cs`
- `CapaAplicacion/Usuarios/Interfaces/IUsuarioRepository.cs`
- `CapaAplicacion/Usuarios/Interfaces/IUsuarioSesionService.cs`

### CapaAplicacion — DTOs
- `CapaAplicacion/Usuarios/DTOs/ActualizarUsuarioDto.cs`
- `CapaAplicacion/Usuarios/DTOs/CrearUsuarioDto.cs`
- `CapaAplicacion/Usuarios/DTOs/EmpleadoDto.cs` (4 campos: id, nombre, apellido, correo)
- `CapaAplicacion/Usuarios/DTOs/RolDto.cs`
- `CapaAplicacion/Usuarios/DTOs/UsuarioVistaDto.cs`

### CapaDatos — Modelos Supabase
- `CapaDatos/Modelados/Usuarios/Accion.cs`
- `CapaDatos/Modelados/Usuarios/AccionRol.cs`
- `CapaDatos/Modelados/Usuarios/Modulo.cs`

### CapaDatos — Repositorios
- `CapaDatos/Repositories/Usuarios/RolRepository.cs`
- `CapaDatos/Repositories/Usuarios/UsuarioRepository.cs` (~312 líneas)
- `CapaDatos/Services/UsuarioSesionService.cs` (~165 líneas)

### CapaDominio — Entidades
- `CapaDominio/Entities/ModuloPermisos.cs`
- `CapaDominio/Entities/UsuarioSesion.cs` (sealed, `HashSet<string>` O(1))

### CapaUI — Vistas y ViewModels
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml` (~714 líneas)
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosViewModel.cs` (~331 líneas)
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuarioModal.xaml.cs`

### Archivos eliminados (legacy)
- ~~`SesionActual.cs`~~ → reemplazado por `IUsuarioSesionService`
- ~~`servicioSesionActual.cs`~~ → reemplazado por `IUsuarioSesionService`

## Flujo de Datos

### Login
```
LoginWindow → AuthService → UsuarioSesionService.IniciarSesionAsync(idUsuario)
    → 3 queries paralelas (acciones_roles, acciones, modulos)
    → Agrupar en ModuloPermisos → HashSet O(1) TieneAccion()
    → SesionPermisos.Configurar(sesionService)
```

### Creación de usuario (desde Empleados)
```
EmpleadosView → click fila → "Crear Usuario"
    → UsuarioModal(idEmpleado, nombre, correo)
    → TxtNombre + TxtEmail readonly, pre-cargados
    → Usuario completa: contraseña + rol
    → BtnGuardar_Click:
        1. Supabase.Client TEMPORAL → SignUp(auth) → Dispose
        2. RPC crear_usuario_empleado_seguro(p_id_empleado, p_email, p_rol)
           → USUARIO_CREADO | USUARIO_NO_EXISTE | USUARIO_YA_EXISTE
```

### Edición de usuario
```
UsuariosView → doble clic fila → UsuarioModal(usuario)
    → Muestra: email (readonly), rol (ComboBox), estado (radio)
    → BtnGuardar_Click → _usuarioRepo.ActualizarAsync(dto)
```

### Consulta del grid
```
UsuariosViewModel → UsuarioRepository.ObtenerPaginaAsync(page, filters)
    → Server-side con Task.WhenAny(query, Task.Delay(10_000))
    → _loadGeneration counter descarta stale responses
    → Counts paralelos: total / activos / inactivos
    → Búsqueda: vista SQL vista_usuarios_busqueda (OR cross-tabla)
```

## Patrones en Uso
- [[Repository Pattern]] — RolRepository, UsuarioRepository
- [[Result Pattern]] — TryAsync + Result<T> en todos los repos
- [[Base Repository con TryAsync]] — herencia de RepositorioBase
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]] — sesión como DI Singleton
- [[ADR-008 - Cliente Temporal para SignUp de Usuarios]] — preservar sesión admin
- [[ADR-009 - RPC crear_usuario_empleado_seguro para vinculacion auth-empleado]] — vinculación atómica
- [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]] — carga al login, HashSet O(1)
- [[ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML]] — puente DI ↔ XAML
- [[ADR-012 - Paginacion server-side con timeout y generacion counter]] — protección race conditions
- [[ADR-013 - Eliminacion de SesionActual y servicioSesionActual legacy]] — limpieza de estáticos
- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] — vista_usuarios_busqueda

## Cadenas Críticas

> [!warning] Creación de usuario = Supabase Client temporal
> El SDK mantiene una sesión por instancia. Se crea un Client temporal
> con AutoRefreshToken=false, se hace SignUp, se Dispose en finally,
> y se restaura la sesión del admin con SetSession().

> [!warning] Permisos desde BD
> `acciones_roles` → `acciones` → `modulos`. 3 queries paralelas al login.
> HashSet O(1). Cambios de permisos requieren re-login.

## Preguntas Abiertas
1. Búsqueda autocomplete sin UI en XAML (VM tiene debounce, XAML falta popup/listBox)
2. Implicaciones de RLS
3. Count duplicado (in-memory vs COUNT(*))
4. Unicidad de email no validada explícitamente
5. Naming inconsistente: alias_usuario vs CorreoUsuario (resuelto en P-019)
6. Sin chequeo de permisos en la vista UsuariosView

## Relaciones
- [[Módulo Productos]] — patrón canónico que replica
- [[Módulo Empleados]] — datos de empleados vinculados, lanza creación de usuario
- [[Arquitectura Actual]] — estado del proyecto
- [[ADR-007]] a [[ADR-013]] — decisiones del módulo
- [[Deuda Técnica - Pendientes]] — P-NNN relacionados
