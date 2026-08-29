---
title: "Sesión 2026-07-23 — Revisión QA: Módulo Usuarios + Refactor de Sesión (Emanuel)"
type: sesion
status: vigente
tags:
  - sesion
  - qa
  - revision
  - usuarios
  - sesion
  - permisos
  - seguridad
  - deuda-tecnica
date: 2026-07-23
updated: 2026-07-23
summary: "Comparación 2f5489d (versión previa, Fernando) → f105047 (Emanuel, 2026-07-23). 36 archivos, +2823 / −150 líneas. Introduce el módulo Usuarios completo…"
scope:
  - CapaAplicacion4/Usuarios/Interfaces
  - CapaDominio/Entities
symbols:
  - Accion
  - AccionRol
  - BaseModel
  - CancellationTokenSource
  - Cerrado
  - CrearAsync
  - Guardado
  - HashSet<string>
  - IPerfilUsuarioService
  - IUsuarioSesionService
branch: feat/fase7-GestióndeUsuarios
autor_cambios: Codex (sesión gestionada por Emanuel)
revisor: QA
---

# Revisión QA — commit `f105047` (Emanuel)

> [!info] Alcance
> Comparación `2f5489d` (versión previa, Fernando) → `f105047` (Emanuel, 2026-07-23).
> **36 archivos, +2823 / −150 líneas.** Introduce el módulo Usuarios completo (View/VM/Modal/Repos/DTOs) y **refactoriza la arquitectura de sesión y permisos**.
> Build de la solución completa: **0 errores** (47 warnings preexistentes de nullable, no relacionados).

---

## Qué cambió

### 1. Refactor de sesión (cambio arquitectónico mayor)

Se **eliminaron** dos holders estáticos y se reemplazaron por un servicio DI:

| Antes (borrado) | Ahora |
|---|---|
| `CapaDominio/SesionActual.cs` (static) | `IUsuarioSesionService` (Singleton en DI) |
| `CapaDominio/servicioSesionActual.cs` (static, "legado") | `UsuarioSesion` (entidad) + `UsuarioSesionService` (impl) |

- **`IUsuarioSesionService`** (`CapaAplicacion4/Usuarios/Interfaces/`) — fuente única de verdad de autenticación + permisos. `IniciarSesionAsync` carga perfil, rol, permisos reales de BD y actualiza `ultimo_acceso` en **una sola llamada**.
- **`UsuarioSesion`** (`CapaDominio/Entities/`) — sellada, `init`-only, con `HashSet<string>` interno para `TieneAccion()` en **O(1)**.
- **`ModuloPermisos`** — agrupación de acciones por módulo.

### 2. Permisos reales desde BD (antes hardcodeados)

`SesionPermisos` pasó de un `switch (idRol)` con IDs mágicos (1=Admin, 2=Operador, 3=Supervisor) y sufijos `_Ver`/`_Modificar` **hardcodeados**, a una **fachada estática que delega** en `IUsuarioSesionService`. Los permisos ahora vienen de las tablas `acciones_roles` / `acciones` / `modulos`. `SesionPermisos.Configurar(servicio)` se llama una vez en `App.xaml.cs`.

### 3. Login simplificado 4 → 3 pasos

`LoginWindow` ya no llama por separado a `servicioSesionActual.Iniciar` + `SesionActual` + `SesionPermisos.CargarAsync` + `_perfilService.CargarAsync`. Todo se unifica en `_sesionService.IniciarSesionAsync(idUsuario)`, con manejo de error vía `Result`.

### 4. Módulo Usuarios (nuevo, completo)

- `UsuarioRepository` / `RolRepository` — heredan `RepositorioBase`, usan `TryAsync` + `Result`. CRUD, paginación, conteos, `ObtenerEmpleadosSinUsuarioAsync`.
- `UsuariosViewModel` — **espejo fiel de `ProductosViewModel`**: paginación, debounce 300ms con `CancellationTokenSource`, contador de generación (`_loadGeneration`), filtros estado/rol, `[ObservableProperty]`/`[RelayCommand]`.
- `UsuarioModal` — patrón `Cerrado`/`Guardado`, validación, auto-generación de email al elegir empleado.
- Creación de usuario vía **RPC `crear_usuario_empleado_seguro`** usando un **cliente Supabase temporal** para no destruir la sesión del admin logueado (patrón correcto para SignUp desde admin).
- Modelos nuevos (`Accion`, `AccionRol`, `Modulo`) heredan `BaseModel` correctamente → `client.From<T>()` válido.

### 5. Fix real incluido

`Usuarios.cs`: columna mal escrita `alias_usuarios` (plural) → **corregida** a `alias_usuario`.

---

## Veredicto QA

> [!success] Lo que está bien
> - **Uniformidad alta.** El VM, los repos y los DTOs siguen exactamente los patrones ya establecidos ([[Módulo Productos]], [[Base Repository con TryAsync]], [[Result Pattern]], [[CommunityToolkit.Mvvm]]). Un dev del proyecto no notaría que lo escribió otra persona.
> - **Mejora arquitectónica neta.** Elimina estado estático global (alineado con la dirección de [[ADR-003 - Disolución de CapaServicios]]), centraliza la sesión en DI y reemplaza permisos hardcodeados por permisos reales de BD — más correcto, testeable y escalable.
> - **Sin referencias colgantes** a las clases borradas. **Sin captive dependency**: `IPerfilUsuarioService` y `UsuarioSesionService` son ambos `Singleton`.
> - **Build limpio** en toda la solución.

> [!warning] Hallazgos (deuda técnica registrada)
> Ver P-013 … P-021 en [[Deuda Técnica - Pendientes]]. Los más relevantes:
> - 🔴 **P-013 (seguridad):** regresión de auditoría — `UsuarioActual ?? 0` en Pesaje pierde el "fail-loud" que tenía `SesionActual.IdUsuario`.
> - 🟡 **P-014 (seguridad):** `Debug.WriteLine` logueando prefijos de access token en `CrearAsync`.
> - 🟡 **P-016 (bug latente):** `Normalizar()` del modal itera bytes UTF-8 como `char` → stripping de acentos poco confiable.
> - 🟡 **P-018 (acoplamiento frágil):** permisos dependen de que `Permiso.ToString()` coincida exacto con `acciones.nombre_accion` en BD; si no, fallan en silencio.
> - 🟢 **P-020 (higiene):** artefactos de tooling de IA (`.atl/`, `.codegraph/`) commiteados al repo.

---

## Notas de contexto

- La búsqueda del módulo Usuarios solo filtra por `alias_usuario` (correo), no por nombre de empleado (está en tabla joineada). **El propio commit lo admite:** *"falta el buscador dentro de este"*. → P-021.
- `ObtenerConteosAsync` descarga IDs y cuenta en memoria — mismo patrón que la deuda aceptada **P-007** en Productos, no es deuda nueva.
- La propiedad C# `correoUsuario` mapea a la columna `alias_usuario` (nombre semánticamente confuso: "alias" vs "correo"). → P-019.

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-013 a P-021, hallazgos de esta revisión
- [[ADR-003 - Disolución de CapaServicios]] — dirección de eliminar estado estático que este cambio continúa
- [[Arquitectura Actual]] — actualizar con la nueva arquitectura de sesión
- [[Módulo Productos]] — plantilla que Usuarios replica
- [[Base Repository con TryAsync]] — patrón de los repos nuevos
- [[Plan de Seguridad - Roadmap 10-10]] — P-013/P-014 son relevantes al roadmap de seguridad
