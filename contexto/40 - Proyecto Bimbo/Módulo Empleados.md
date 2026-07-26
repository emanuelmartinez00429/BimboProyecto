---
title: Módulo Empleados
tags:
  - modulo
  - empleados
  - wip
date: 2026-07-26
---

# Módulo Empleados

> [!warning] Solo lectura — en revisión
> Creado el 2026-07-26 a pedido explícito de Fernando: **la lista, el buscador y los modales de Ver/Crear están completamente funcionales, pero el botón Guardar (y "Cambiar Estado") NO persisten nada.** Es intencional — Fernando necesita revisar algo del modelo de Empleados antes de habilitar la escritura. Ver [[Sesión 2026-07-26 - Módulo Empleados (solo lectura)]].

---

## Qué funciona

- Listado paginado (50/página), stats TOTAL/ACTIVOS/INACTIVOS reales.
- Buscador `SuggestionSearchBox` (nombre, apellido, número de identidad) — mismo patrón que Usuarios/Categorías, con popup, teclado y mouse.
- Filtro por estado (Activos/Inactivos/Todos).
- Navegación cross-page al seleccionar una sugerencia que está en otra página.
- Modal `EmpleadoModal` abre y **muestra datos reales** al hacer doble-click o "Editar"; abre vacío al hacer "Nuevo".

## Qué NO funciona (a propósito)

- `EmpleadoModal.BtnGuardar_Click` no llama a ningún repositorio — solo muestra un `MessageBox` informativo ("Guardado deshabilitado temporalmente — módulo en revisión").
- `EmpleadosViewModel.ToggleEstadoCommand` tampoco persiste — solo escribe en `ErrorCarga` el mismo aviso.
- `IEmpleadoRepository` **no expone** `CreateAsync`/`UpdateAsync`/`CambiarEstadoAsync` todavía — a propósito, para que sea imposible escribir por accidente mientras el módulo está en revisión.

## Archivos clave

### CapaAplicacion4/Empleados/
```
Dtos/EmpleadoDto.cs           — IdEmpleado, NombreEmpleado, ApellidoEmpleado, NumeroIdentidad, TelefonoEmpleado, CorreoEmpleado, IdEstado
Queries/EmpleadoFiltros.cs    — { int? IdEstado }
Interfaces/IEmpleadoRepository.cs — GetPagedAsync, BuscarSugerenciasAsync, GetPaginaDeRegistroAsync (SOLO lectura)
```

### CapaDatos/Repositories/Empleados/
```
EmpleadoCrudRepository.cs — implementa IEmpleadoRepository sobre CapaDatos.Modelados.Usuarios.Empleados
```

> [!bug] Namespace no puede ser `CapaDatos.Repositories.Empleados`
> El modelo Supabase `CapaDatos.Modelados.Usuarios.Empleados` se usa **sin calificar** en `EmpleadoRepository.cs` (buscador universal) y `UsuarioRepository.cs`, ambos con `using CapaDatos.Modelados.Usuarios;`. Un namespace **hermano** con el mismo nombre ("Empleados") gana la resolución de C# sobre el `using` y rompe esos dos archivos con `CS0118: 'Empleados' es espacio de nombres pero se usa como tipo`. Por eso el namespace real es `CapaDatos.Repositories.GestionEmpleados` (la carpeta física sigue llamándose `Empleados/`, eso no afecta al compilador).

### CapaUI/.../Pantallas/Empleados/
```
EmpleadosViewModel.cs   — mismo patrón que UsuariosViewModel (sin Realtime, sin filtro de Rol)
EmpleadosView.xaml(.cs) — lista + SuggestionSearchBox + paginación + stats
EmpleadoModal.xaml(.cs) — Ver/Crear; Guardar es un no-op deliberado
```

### Navegación
- `Routes.Empleados = "empleados"` ya existía (definido para el buscador universal). Antes apuntaba a un placeholder `ConstructionVM("Gestión de Empleados", ...)` — ahora apunta a `EmpleadosVM` real (`MainViewModel.cs` + `MainWindow.xaml` DataTemplate).
- Sidebar: botón "Gestión de Empleados" bajo el submenú Usuarios ya existía en `MainWindow.xaml`, sin cambios.

## Para habilitar la escritura más adelante

1. Agregar `CreateAsync`/`UpdateAsync`/`CambiarEstadoAsync` a `IEmpleadoRepository` (mismo patrón que `ICategoriaRepository`).
2. Implementarlos en `EmpleadoCrudRepository`.
3. En `EmpleadoModal.xaml.cs`, reemplazar el `MessageBox` de `BtnGuardar_Click` por la llamada real al repositorio (ver `CategoriaModal.xaml.cs` como plantilla) + evento `Guardado` + suscripción en `EmpleadosView.xaml.cs`.
4. En `EmpleadosViewModel.ToggleEstado()`, llamar al repositorio real en vez de solo escribir `ErrorCarga`.

## Relaciones

- [[Módulo Productos]] — patrón de referencia general
- [[Sesión 2026-07-26 - Módulo Empleados (solo lectura)]]
- [[Arquitectura Actual]]
