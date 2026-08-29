---
title: Sesión 2026-05-22 — Refactor Arquitectural Fase 2
type: sesion
status: vigente
tags:
  - bitacora
  - arquitectura
  - clean-architecture
  - refactor
  - di
date: 2026-05-22
updated: 2026-05-22
summary: // MostrarPrincipal ahora resuelve desde DI (igual que LoginWindow) var main = Services.GetRequiredService<MainWindow>();
scope:
  - CapaAplicacion4/Perfil
  - CapaDatos/Perfil
  - CapaDominio/Perfil
symbols:
  - IPerfilUsuarioService
  - LoginWindow
  - MainViewModel
  - MainWindow
  - PerfilActual
  - PerfilUsuarioService
  - RepositorioUsuario
  - ServicioPerfilUsuario
  - Transient
  - WelcomeScreen
---

# Sesión 2026-05-22 — Refactor Arquitectural Fase 2

## Problema resuelto

`ServicioPerfilUsuario` estaba en `CapaServicios` como clase estática global y referenciaba `RepositorioUsuario` de `CapaDatos` directamente. Esto era la causa restante de la violación `CapaServicios → CapaDatos`.

---

## Cambios implementados

### Archivos nuevos

| Archivo | Capa | Descripción |
|---|---|---|
| `CapaDominio/Perfil/PerfilUsuario.cs` | Domain | Entidad POCO extraída del archivo eliminado |
| `CapaAplicacion4/Perfil/IPerfilUsuarioService.cs` | Application | Contrato: `PerfilActual`, `CargarAsync()`, `Limpiar()` |
| `CapaDatos/Perfil/PerfilUsuarioService.cs` | Infrastructure | Implementación: llama `RepositorioUsuario.ObtenerPorIdAsync` |

### Interfaz (Application layer)

```csharp
// CapaAplicacion4/Perfil/IPerfilUsuarioService.cs
public interface IPerfilUsuarioService
{
    PerfilUsuario? PerfilActual { get; }
    Task CargarAsync(int idUsuario);  // El caller pasa el ID, el servicio no lee estado global
    void Limpiar();
}
```

> [!note] Ajuste durante implementación
> La firma final quedó `CargarAsync(int idUsuario)` en vez de `CargarAsync()` sin parámetros.
> Razón: `PerfilUsuarioService` está en `CapaDatos` y no referencia `CapaServicios`,
> por lo que no puede leer `servicioSesionActual` directamente. El caller (`LoginWindow`)
> pasa `result.Value!.IdUsuario` obtenido del resultado del login.

### Registro en DI — Singleton

```csharp
// CapaDatos/DependencyInjection.cs
services.AddSingleton<IPerfilUsuarioService, PerfilUsuarioService>();
```

**Por qué Singleton:** el servicio mantiene estado (`PerfilActual`) que debe persistir entre `LoginWindow` → `MainWindow` → `WelcomeScreen` durante toda la sesión del usuario. Al hacer logout se llama `Limpiar()` y al hacer login se llama `CargarAsync()`.

---

### App.xaml.cs — MainViewModel y MainWindow en DI

```csharp
services.AddTransient<MainViewModel>();
services.AddTransient<MainWindow>();

// MostrarPrincipal ahora resuelve desde DI (igual que LoginWindow)
var main = Services.GetRequiredService<MainWindow>();
```

---

### MainWindow.xaml — DataContext removido del XAML

```xml
<!-- ANTES -->
<Window.DataContext>
    <local:MainViewModel/>
</Window.DataContext>

<!-- DESPUÉS — eliminado, se setea en code-behind -->
```

El DataContext se setea en el constructor antes de `InitializeComponent()`:

```csharp
public MainWindow(MainViewModel vm, IPerfilUsuarioService perfilService)
{
    _perfilService = perfilService;
    DataContext    = vm;
    InitializeComponent();
    // ... resto igual
}
```

---

### Archivos de CapaUI actualizados

| Archivo | Cambio |
|---|---|
| `MainWindow.xaml` | Removido `<local:MainViewModel/>` del XAML |
| `MainWindow.xaml.cs` | Constructor acepta `MainViewModel` + `IPerfilUsuarioService`; `ServicioPerfilUsuario.Limpiar()` → `_perfilService.Limpiar()` |
| `MainViewModel.cs` | Constructor acepta `IPerfilUsuarioService`; propiedades usan `_perfilService.PerfilActual` |
| `LoginWindow.xaml.cs` | Constructor acepta `IPerfilUsuarioService`; `ServicioPerfilUsuario.CargarAsync()` → `_perfilService.CargarAsync()` |
| `WelcomeScreen.xaml.cs` | Usa `App.Services.GetRequiredService<IPerfilUsuarioService>()` — patrón service locator aceptado en WPF para UserControls |

### Archivo eliminado

- `CapaServicios/ServicioPerfilUsuario.cs` — reemplazado por la implementación en CapaDatos

---

## Resultado final del grafo de dependencias

```
CapaServicios  →  (ninguno) ← VIOLACIÓN ELIMINADA
CapaDatos      →  CapaDominio, CapaAplicacion
CapaAplicacion →  CapaDominio
CapaDominio    →  (ninguno)
CapaUI         →  CapaDatos, CapaAplicacion, CapaDominio, CapaServicios
```

`CapaServicios.csproj` ya no tiene `<ProjectReference>` alguna. Solo NuGet de Supabase (para `servicioSesionActual.Sesion` de tipo `Supabase.Gotrue.Session`, mantenido por compat con BimboPesaje).

---

## Patrón aplicado: WPF + DI para ventanas con ViewModel

El mismo patrón usado en `LoginWindow` se extendió a `MainWindow`:
1. Ventana registrada como `Transient` en DI
2. Resuelta con `Services.GetRequiredService<T>()`
3. Sus dependencias (ViewModel, servicios) inyectadas via constructor
4. DataContext seteado en constructor antes de `InitializeComponent()`

Para `WelcomeScreen` (UserControl instanciado por WPF desde DataTemplates, sin constructor DI), se usa el **service locator** `App.Services.GetRequiredService<T>()` — patrón pragmático y documentado para este caso específico de WPF.
