---
title: Plan de Refactor — Navegación MainViewModel
type: plan
status: pendiente
tags:
  - bimbo
  - plan
  - navegacion
  - mvvm
  - refactor
date: 2026-05-23
updated: 2026-05-23
summary: "Auditoría detectó 5 hallazgos en el sistema de navegación del menú principal. Este plan los resuelve en 4 fases incrementales, cada una compila y funciona de…"
scope:
  - CapaUI/Core/MVVM
  - CapaUI/Formularios/Principal
  - CapaUI/Navigation
symbols:
  - AddTransient
  - ConstructionVM
  - EntityDetailWindow
  - ICommand
  - INavigationService
  - MainViewModel
  - NavigationService
  - ObservableObject
  - PlaceholderVM
  - ProductosVM
---

# Plan de Refactor — Navegación MainViewModel

> [!info] Contexto
> Auditoría detectó 5 hallazgos en el sistema de navegación del menú principal.
> Este plan los resuelve en 4 fases incrementales, cada una compila y funciona de forma independiente.

---

## Diagnóstico resumido

| # | Hallazgo | Severidad |
|---|---|---|
| 1 | Triple indirección: XAML Tag → switch en code-behind → 18 ICommand | 🔴 Alto |
| 2 | MainViewModel usa `ViewModelBase` manual, no `ObservableObject` | 🔴 Alto |
| 3 | Service locator en ViewModel (`App.Services.GetRequiredService` en un comando) | 🟡 Medio |
| 4 | `INavigationService` registrado en DI pero nunca inyectado ni usado | 🟡 Medio |
| 5 | VMs marcadores (`WelcomeVM`, `ProductosVM`) instanciados sin DI | 🟢 Bajo |

---

## Arquitectura objetivo

### Flujo actual (problemático)

```
XAML Tag="usuarios-sub"
  → BtnSub_Click() lee el Tag como string
    → Navigate(string id) — switch con 13 cases
      → Vm.NavUsuariosCommand.Execute(null)
        → RelayCommand(manual) → VistaActual = new ConstructionVM(...)
```

**Problemas:** 4 lugares para mantener por cada ruta · code-behind orquesta al ViewModel · errores silenciosos

### Flujo objetivo (limpio)

```
XAML Tag="usuarios-sub"
  → BtnSub_Click() maneja estado visual (dots, indicadores, animaciones)
    → Vm.NavigateCommand.Execute(subId)  ← único punto de entrada
      → [RelayCommand] Navigate(string routeId)
        → _routes[routeId]()  ← factory dictionary
          → VistaActual = vm resultante
```

**Beneficio:** 1 lugar para agregar ruta · code-behind solo gestiona UI · error de clave detectable

---

## Fase 1 — Migrar MainViewModel a ObservableObject + NavigateCommand único

> [!tip] Esta fase resuelve los hallazgos #1 y #2 simultáneamente.

### Cambios en `CapaUI/Formularios/Principal/MainViewModel.cs`

**Antes:** 18 `ICommand` declarados + 18 `RelayCommand` init en constructor + `ViewModelBase` manual

**Después:**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CapaUI.Formularios.Principal;

public partial class MainViewModel : ObservableObject
{
    private readonly IPerfilUsuarioService _perfilService;
    private readonly UniversalSearchViewModel _searchVm;   // ← Fase 3

    // ── Vista actual ──────────────────────────────────────────────────
    [ObservableProperty] private object? _vistaActual;

    // ── Info de usuario ───────────────────────────────────────────────
    public string NombreUsuario => _perfilService.PerfilActual?.NombreCompleto
                                   ?? servicioSesionActual.NombreUsuario;
    public string Iniciales     => _perfilService.PerfilActual?.Iniciales ?? "??";
    public string NombreRol     => _perfilService.PerfilActual?.NombreRol  ?? "";

    // ── Visibilidad por permisos (sin cambios) ────────────────────────
    public bool VerPesajes     => SesionPermisos.TieneAlguno(...);
    // ... resto igual

    // ── Tabla de rutas ────────────────────────────────────────────────
    private Dictionary<string, Func<object>> _routes = null!;

    public event EventHandler? CierreRequerido;

    public MainViewModel(IPerfilUsuarioService perfilService,
                         UniversalSearchViewModel searchVm)   // ← Fase 3
    {
        _perfilService = perfilService;
        _searchVm      = searchVm;

        _routes = new()
        {
            // Usuarios
            ["usuarios-sub"] = () => new ConstructionVM("Gestión de Usuarios",  "Usuarios"),
            ["empleados"]    = () => new ConstructionVM("Gestión de Empleados", "Usuarios"),
            ["roles"]        = () => new ConstructionVM("Gestión de Roles",     "Usuarios"),
            ["bitacora"]     = () => new ConstructionVM("Bitácora",             "Usuarios"),
            // Productos
            ["productos-sub"]         = () => new ProductosVM(),
            ["proveedores"]           = () => new ConstructionVM("Proveedores",           "Productos"),
            ["fabricantes"]           = () => new ConstructionVM("Fabricantes",           "Productos"),
            ["categorias"]            = () => new ConstructionVM("Categorías",            "Productos"),
            ["contactos-proveedores"] = () => new ConstructionVM("Contactos Proveedores", "Productos"),
            ["contactos-fabricantes"] = () => new ConstructionVM("Contactos Fabricantes", "Productos"),
            // Pesajes
            ["pesajes-sub"]    = () => new ConstructionVM("Movimientos y Entradas", "Pesajes"),
            // Reportería
            ["dashboard"]      = () => new Dashboard.DashboardVM(),
            ["crear-reportes"] = () => new ConstructionVM("Crear Reportes", "Reportería"),
            // Especiales
            ["bienvenida"]     = () => new WelcomeVM(),
            ["mi-usuario"]     = () => new ConstructionVM("Mi Usuario", ""),
        };

        VistaActual = new WelcomeVM();
    }

    // ── Comando único de navegación ───────────────────────────────────
    [RelayCommand]
    private void Navigate(string? routeId)
    {
        if (string.IsNullOrEmpty(routeId)) return;
        if (!_routes.TryGetValue(routeId, out var factory)) return;
        VistaActual = factory();
    }

    // ── Búsqueda ──────────────────────────────────────────────────────
    [RelayCommand]
    private void Buscar(string? term)
    {
        VistaActual = _searchVm;
        if (!string.IsNullOrWhiteSpace(term))
            _searchVm.TriggerSearch(term);
    }

    // ── Cierre de sesión ──────────────────────────────────────────────
    [RelayCommand]
    private void CerrarSesion() => CierreRequerido?.Invoke(this, EventArgs.Empty);
}
```

> [!warning] Clases marcadoras
> `WelcomeVM`, `ConstructionVM`, `ProductosVM`, `PlaceholderVM` siguen siendo `ViewModelBase`.
> Solo `MainViewModel` migra a `ObservableObject` en esta fase. Las marcadoras no necesitan
> notificaciones de propiedad ni comandos — cambiarlas sería ruido sin beneficio.

### Registrar `UniversalSearchViewModel` como parámetro del constructor

En `CapaUI/App.xaml.cs` no cambia nada — `UniversalSearchViewModel` ya está registrado como Transient.
El contenedor DI lo inyectará automáticamente al resolver `MainViewModel`.

---

## Fase 2 — Limpiar el code-behind (MainWindow.xaml.cs)

> [!tip] Esta fase elimina el switch de 13 cases del code-behind.

### Cambio en `Navigate(string id)`

**Antes:**
```csharp
private void Navigate(string id)
{
    switch (id)
    {
        case "usuarios-sub": Vm.NavUsuariosCommand.Execute(null); break;
        case "empleados":    Vm.NavEmpleadosCommand.Execute(null); break;
        // ... 11 casos más
    }
}
```

**Después — método eliminado.** Cada llamador pasa directamente al ViewModel:

```csharp
// BtnSub_Click — al final, en lugar de Navigate(subId):
Vm.NavigateCommand.Execute(subId);

// BtnModuloDirect_Click — en lugar de Navigate(id):
Vm.NavigateCommand.Execute(id);

// BtnHome_Click — en lugar de Vm.NavBienvenidaCommand.Execute(null):
Vm.NavigateCommand.Execute("bienvenida");

// TxtSearch_KeyDown — en lugar de Vm.NavBusquedaCommand?.Execute(text):
Vm.BuscarCommand.Execute(TxtSearch.Text);
```

> [!note] Lo que NO cambia en el code-behind
> `BtnSub_Click`, `BtnModulo_Click`, `BtnModuloDirect_Click` siguen existiendo —
> son responsables de la **UI visual** (dots activos, indicadores de módulo, animaciones del sidebar).
> Eso es legítimamente responsabilidad de la View. Solo se elimina la función `Navigate(string)`.

---

## Fase 3 — Inyectar UniversalSearchViewModel (resuelve hallazgo #3)

> [!tip] Ya incluido en la implementación de Fase 1. Esta fase solo documenta el razonamiento.

### Problema

```csharp
// ❌ ANTES — service locator dentro de un comando
NavBusquedaCommand = new RelayCommand(param =>
{
    var vm = App.Services.GetRequiredService<UniversalSearchViewModel>();
    VistaActual = vm;
});
```

Cada ejecución del comando resuelve una **instancia nueva** del SearchViewModel.
Si el usuario busca "harina", navega a Productos, y vuelve al buscador, pierde el estado anterior.

### Solución

```csharp
// ✅ DESPUÉS — instancia única inyectada en el constructor
public MainViewModel(IPerfilUsuarioService perfilService,
                     UniversalSearchViewModel searchVm)
{
    _searchVm = searchVm;  // misma instancia toda la sesión
}

[RelayCommand]
private void Buscar(string? term)
{
    VistaActual = _searchVm;
    if (!string.IsNullOrWhiteSpace(term))
        _searchVm.TriggerSearch(term);
}
```

`UniversalSearchViewModel` ya es `AddTransient` en DI — al inyectarse en el constructor de
`MainViewModel` (también Transient), se crea una vez y vive toda la sesión del usuario.

---

## Fase 4 — Constantes de ruta (elimina magic strings)

> [!tip] Fase de bajo riesgo. Se puede diferir si la prioridad es otra.

Crear `CapaUI/Navigation/Routes.cs`:

```csharp
namespace CapaUI.Navigation;

/// <summary>Constantes de las rutas de navegación del menú principal.</summary>
public static class Routes
{
    // Usuarios
    public const string Usuarios   = "usuarios-sub";
    public const string Empleados  = "empleados";
    public const string Roles      = "roles";
    public const string Bitacora   = "bitacora";

    // Productos
    public const string Productos            = "productos-sub";
    public const string Proveedores          = "proveedores";
    public const string Fabricantes          = "fabricantes";
    public const string Categorias           = "categorias";
    public const string ContactosProveedores = "contactos-proveedores";
    public const string ContactosFabricantes = "contactos-fabricantes";

    // Pesajes
    public const string Pesajes = "pesajes-sub";

    // Reportería
    public const string Dashboard     = "dashboard";
    public const string CrearReportes = "crear-reportes";

    // Especiales
    public const string Bienvenida = "bienvenida";
    public const string MiUsuario  = "mi-usuario";
}
```

Uso en MainViewModel:
```csharp
_routes = new()
{
    [Routes.Usuarios] = () => new ConstructionVM("Gestión de Usuarios", "Usuarios"),
    [Routes.Productos] = () => new ProductosVM(),
    // ...
};
```

Uso en MainWindow.xaml.cs:
```csharp
Vm.NavigateCommand.Execute(Routes.Bienvenida);  // sin strings literales
```

> [!note] Los Tags en XAML siguen siendo strings
> XAML no puede referenciar constantes C# en `Tag=""`.
> Las rutas en XAML permanecen como strings literales — eso es aceptable.
> La constante existe para proteger el código C# (ViewModel, code-behind), no el XAML.

---

## Decisión sobre INavigationService (hallazgo #4)

> [!question] ¿Conectar o eliminar?

`NavigationService.NavigateTo(entityType, param)` abre `EntityDetailWindow` — ventanas secundarias
de detalle sobre entidades. Es funcionalidad diferente a la navegación de paneles del menú principal.

**Opción A — Conectar:** cuando se implemente la pantalla de detalle de empleado/producto/pesaje,
inyectar `INavigationService` en el ViewModel del módulo correspondiente y llamar `NavigateTo("empleado", id)`.

**Opción B — Eliminar:** si la app no usará ventanas secundarias (todo en panel central),
eliminar `NavigationService`, `INavigationService`, `EntityDetailWindow` y el registro en DI.

> **Consultar al usuario antes de implementar.** No hay prioridad hasta que se necesite un detalle.

---

## Patrón para agregar módulos nuevos

Con este refactor, agregar una pantalla nueva requiere exactamente **2 cambios**:

### 1. Agregar la ruta en `_routes` (MainViewModel)
```csharp
[Routes.NuevoModulo] = () => App.Services.GetRequiredService<NuevoModuloViewModel>(),
```

### 2. Agregar el botón en XAML (MainWindow.xaml)
```xml
<Button Style="{StaticResource SidebarSubButton}" Tag="nuevo-modulo"
        Click="BtnSub_Click"/>
```

Si el VM necesita DI: registrarlo en `App.xaml.cs` + `CapaDatos/DependencyInjection.cs`.
Si es placeholder por ahora: `() => new ConstructionVM("Nombre", "Módulo Padre")`.

---

## Estado y fases

| Fase | Descripción | Estado |
|---|---|---|
| 1 | MainViewModel → ObservableObject + NavigateCommand único | ✅ Completada |
| 2 | Limpiar switch en code-behind | ✅ Completada |
| 3 | Inyectar UniversalSearchViewModel | ✅ Completada |
| 4 | Constantes de ruta `Routes.cs` | ✅ Completada |
| — | Decisión INavigationService | 🤔 Consultar |

---

## Archivos afectados

| Archivo | Cambio |
|---|---|
| `CapaUI/Formularios/Principal/MainViewModel.cs` | Reescrito completo (Fases 1 + 3) |
| `CapaUI/Formularios/Principal/MainWindow.xaml.cs` | Eliminar `Navigate(string)`, actualizar 4 llamadores (Fase 2) |
| `CapaUI/Navigation/Routes.cs` | Nuevo — constantes de ruta (Fase 4) |
| `CapaUI/Core/MVVM/ViewModelBase.cs` | Sin cambios — sigue siendo base de clases marcadoras |

---

*Relacionado: [[Arquitectura Actual]] · [[CommunityToolkit.Mvvm]] · [[Clean Architecture]] · [[Convenciones C#]]*
