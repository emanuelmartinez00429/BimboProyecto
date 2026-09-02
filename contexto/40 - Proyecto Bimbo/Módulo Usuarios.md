---
title: "Módulo Usuarios"
tags: [bimbo, modulo, gestion-usuarios, auth, permisos]
date: 2026-09-01
---

# Módulo Usuarios

> CRUD de usuarios con autenticación Supabase, roles/permisos desde BD, y sesión refactorizada (`IUsuarioSesionService`).

## Archivos del Módulo

### CapaAplicacion — Interfaces
- `CapaAplicacion/Usuarios/Interfaces/IRolRepository.cs`
- `CapaAplicacion/Usuarios/Interfaces/IRolPermisoRepository.cs`
- `CapaAplicacion/Usuarios/Interfaces/IUsuarioRepository.cs`
- `CapaAplicacion/Usuarios/Interfaces/IUsuarioSesionService.cs`

### CapaAplicacion — DTOs
- `CapaAplicacion/Usuarios/DTOs/ActualizarUsuarioDto.cs`
- `CapaAplicacion/Usuarios/DTOs/CrearUsuarioDto.cs`
- `CapaAplicacion/Usuarios/DTOs/EmpleadoDto.cs` (4 campos: id, nombre, apellido, correo)
- `CapaAplicacion/Usuarios/DTOs/RolDto.cs`
- `CapaAplicacion/Usuarios/DTOs/ModuloAccionesDto.cs`
- `CapaAplicacion/Usuarios/DTOs/UsuarioVistaDto.cs`

### CapaDatos — Modelos Supabase
- `CapaDatos/Modelados/Usuarios/Accion.cs`
- `CapaDatos/Modelados/Usuarios/AccionRol.cs`
- `CapaDatos/Modelados/Usuarios/Modulo.cs`
- `CapaDatos/Modelados/Usuarios/Roles.cs`

Los cuatro modelos RBAC reflejan las tablas `acciones`, `acciones_roles`, `modulos` y `roles`. Incluyen sus columnas de auditoría `created_at`/`updated_at` cuando corresponden. `acciones_roles.id_estado` controla si la asignación está activa; `roles.id_estado` habilita la baja lógica y `roles.es_sistema` identifica de forma estable el rol Administrador protegido.

### CapaDatos — Repositorios
- `CapaDatos/Repositories/Usuarios/RolRepository.cs`
- `CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs` — catálogo y asignaciones rol-acción
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
- `CapaUI/Formularios/Principal/Pantallas/Roles/RolesView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Roles/RolesViewModel.cs`
- `CapaUI/Formularios/Principal/Pantallas/Roles/RolModal.xaml`

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

### Protección contra autoadministración

Desde 2026-08-15 ningún usuario, incluido un administrador, puede cambiar su propio rol ni deshabilitar su cuenta desde este módulo:

- `UsuariosViewModel` deshabilita Editar/Cambiar estado y muestra una advertencia cuando `Seleccionado.IdUsuario` coincide con `IUsuarioSesionService.SesionActual.IdUsuario`.
- El doble clic pasa por `EditarCommand`; ya no evita el `CanExecute`.
- `UsuarioRepository` repite la comparación antes de cualquier viaje de red.
- Supabase protege `id_rol` e `id_estado` con `trg_proteger_campos_sensibles_usuario`, comparando `usuarios.uuid_usuario` con `auth.uid()`.
- `update_Usuarios` dejó de estar abierto a `public`: solo aplica a `authenticated` y permite la fila propia para campos no sensibles o la administración de terceros según `Modificar Usuario`/`Eliminar Usuario`.

`ActualizarUltimoAccesoAsync` permanece vigente: el trigger no bloquea `ultimo_acceso`. Ver [[ADR-020 - Defensa en profundidad contra autoadministracion de usuarios]].

### Consulta del grid
```
UsuariosViewModel → UsuarioRepository.ObtenerPaginaAsync(page, filters)
    → Server-side con Task.WhenAny(query, Task.Delay(10_000))
    → _loadGeneration counter descarta stale responses
    → Counts paralelos: total / activos / inactivos
    → Búsqueda: vista SQL vista_usuarios_busqueda (OR cross-tabla)
```

### Administración de permisos por rol
```
MainWindow → RolesView → RolesViewModel
    → RolPermisoRepository.ObtenerResumenAsync()
        → carga en paralelo roles, usuarios, catálogo y asignaciones
    → cuadrícula de tarjetas → abrir detalle por IdRol (sin nueva consulta)
        → nombre, estado, tipo de sistema, usuarios y permisos activos
        → acciones agrupadas por módulo con búsqueda y filtros
    → marcar/desmarcar acciones → GuardarAsignacionesAsync(...)
        → reemplazar_permisos_rol_seguro realiza el reemplazo atómico
```

Desde 2026-09-01 la lectura exige `Consultar Rol`; crear, modificar, desactivar y asignar permisos exigen respectivamente `Crear Rol`, `Modificar Rol`, `Eliminar Rol` y `Asignar Permisos a Rol`. Asignar un rol a otra cuenta exige `Asignar Rol a Usuario`. Los cambios afectan a todos los usuarios del rol y se reflejan al renovar la sesión.

Desde 2026-09-02 `Gestión de Roles` usa una sola ruta con dos estados internos: cuadrícula y detalle. Cada tarjeta abre el detalle por `IdRol`; el nombre es solo informativo y renombrar el rol no afecta la navegación. Crear un rol incorpora el resultado al resumen ya cargado y abre su detalle inmediatamente. Los roles inactivos y de sistema pueden consultarse, pero la edición de permisos solo se habilita si la sesión tiene `Asignar Permisos a Rol`, el rol está activo y no es de sistema. Al volver con cambios pendientes se puede guardar, descartar o seguir editando; si el guardado remoto falla, el detalle permanece abierto.

Desde 2026-09-02 el Administrador no puede renombrarse, desactivarse, perder `es_sistema` ni modificar ninguna asignación. UI, RPC y triggers conservan activas todas las acciones del catálogo; un trigger sobre `acciones` le asigna automáticamente cada acción futura. La pantalla mantiene los permisos visibles con candado, pero deshabilita cambios individuales, por módulo y masivos. Los demás roles siguen siendo editables y tampoco puede desactivarse un rol con usuarios asignados. Ver [[ADR-024 - Rol Administrador inmutable con acceso total]].

### Aplicación de permisos
```
acciones_roles → UsuarioSesionService → SesionPermisos
    ├─ PermisoBehavior.Requiere en menú y botones
    ├─ MainViewModel bloquea navegación directa sin permiso
    ├─ ViewModel valida antes de ejecutar comandos CRUD
    └─ code-behind valida antes de abrir modales o eliminar
```

`PermisoBehavior` trabaja en modo cerrado: una cadena vacía o que no corresponda al catálogo `PermisoCatalogo` oculta el elemento y genera un error en Serilog. El XAML utiliza literalmente los 34 valores de `acciones.nombre_accion` (por ejemplo, `Consultar Rol`); el enum C# conserva identificadores sin espacios y `NombreBaseDatos()` realiza la traducción explícita. La base de datos/RLS continúa siendo la frontera final para solicitudes directas fuera del cliente WPF.

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
- [[ADR-020 - Defensa en profundidad contra autoadministracion de usuarios]] — bloqueo UI, repositorio y base de datos
- [[ADR-023 - Rol Administrador de sistema con nucleo de permisos protegido]] — rol compartido y núcleo recuperable
- [[ADR-024 - Rol Administrador inmutable con acceso total]] — reemplaza el núcleo parcial por acceso total obligatorio

## Cadenas Críticas

> [!warning] Creación de usuario = Supabase Client temporal
> El SDK mantiene una sesión por instancia. Se crea un Client temporal
> con AutoRefreshToken=false, se hace SignUp, se Dispose en finally,
> y se restaura la sesión del admin con SetSession().

> [!warning] Permisos desde BD
> `acciones_roles` → `acciones` → `modulos`. 3 queries paralelas al login.
> HashSet O(1). Los nombres se comparan con la nomenclatura literal de BD (`Crear Producto`, `Consultar Usuario`, etc.). Cambios de permisos requieren re-login.

## Preguntas Abiertas
1. Búsqueda autocomplete sin UI en XAML (VM tiene debounce, XAML falta popup/listBox)
2. Implicaciones de RLS
3. Count duplicado (in-memory vs COUNT(*))
4. Unicidad de email no validada explícitamente
5. Naming inconsistente: alias_usuario vs CorreoUsuario (resuelto en P-019)
6. Auditoría de políticas RLS para confirmar la frontera final del backend

## Relaciones
- [[Módulo Productos]] — patrón canónico que replica
- [[Módulo Empleados]] — datos de empleados vinculados, lanza creación de usuario
- [[Arquitectura Actual]] — estado del proyecto
- [[Sesión 2026-08-09 - Alineación de modelados RBAC]] — correspondencia de los modelos con el esquema vigente
- [[Sesión 2026-08-09 - Implementación RBAC visual y gestión de roles]] — aplicación visual, navegación y administración de asignaciones
- [[ADR-007]] a [[ADR-013]] — decisiones del módulo
- [[Deuda Técnica - Pendientes]] — P-NNN relacionados
