---
title: Sesión 2026-05-23 — Refactor Navegación Fase 1
type: sesion
status: vigente
tags:
  - bitacora
  - navegacion
  - mvvm
  - refactor
  - clean-architecture
date: 2026-05-23
updated: 2026-05-23
summary: Auditoría detectó 5 hallazgos en el sistema de navegación del menú principal. Esta sesión implementa la Fase 1 del plan (y Fase 2 + 3 incluidas). Ver plan…
scope:
  - CapaUI/Formularios/Principal
symbols:
  - ICommand
  - INotifyPropertyChanged
  - UniversalSearchViewModel
---

# Sesión 2026-05-23 — Refactor Navegación Fase 1

## Contexto

Auditoría detectó 5 hallazgos en el sistema de navegación del menú principal.
Esta sesión implementa la Fase 1 del plan (y Fase 2 + 3 incluidas).
Ver plan completo: [[Plan de Refactor - Navegación MainViewModel]]

---

## Cambios implementados

### `CapaUI/Formularios/Principal/MainViewModel.cs` — Reescrito

**Antes:**
- `class MainViewModel : ViewModelBase` (manual `INotifyPropertyChanged`)
- 18 `ICommand` declarados individualmente
- 18 `new RelayCommand(...)` init en el constructor
- Service locator: `App.Services.GetRequiredService<UniversalSearchViewModel>()` dentro de un comando
- `UniversalSearchViewModel` creado cada vez que se ejecutaba el comando de búsqueda

**Después:**
- `partial class MainViewModel : ObservableObject` — CommunityToolkit.Mvvm ✅
- `[ObservableProperty] private object? _vistaActual;` — source generator
- `[RelayCommand] private void Navigate(string? routeId)` — comando único paramétrico
- `[RelayCommand] private void Buscar(string? term)` — reemplaza NavBusquedaCommand
- `[RelayCommand] private void CerrarSesion()` — reemplaza CerrarSesionCommand
- `Dictionary<string, Func<object>> _routes` — tabla de rutas centralizada
- `UniversalSearchViewModel` inyectado en constructor → instancia única por sesión

**Rutas registradas:**

| routeId | VM resultante |
|---|---|
| `usuarios-sub` | `ConstructionVM("Gestión de Usuarios", "Usuarios")` |
| `empleados` | `ConstructionVM("Gestión de Empleados", "Usuarios")` |
| `roles` | `ConstructionVM("Gestión de Roles", "Usuarios")` |
| `bitacora` | `ConstructionVM("Bitácora", "Usuarios")` |
| `productos-sub` | `ProductosVM()` |
| `proveedores` | `ConstructionVM("Gestión de Proveedores", "Productos")` |
| `fabricantes` | `ConstructionVM("Gestión de Fabricantes", "Productos")` |
| `categorias` | `ConstructionVM("Gestión de Categorías", "Productos")` |
| `contactos-proveedores` | `ConstructionVM("Contactos Proveedores", "Productos")` |
| `contactos-fabricantes` | `ConstructionVM("Contactos Fabricantes", "Productos")` |
| `pesajes-sub` | `ConstructionVM("Movimientos y Entradas", "Pesajes")` |
| `dashboard` | `DashboardVM()` |
| `crear-reportes` | `ConstructionVM("Crear Reportes", "Reportería")` |
| `bienvenida` | `WelcomeVM()` |
| `mi-usuario` | `ConstructionVM("Mi Usuario", "")` |

---

### `CapaUI/Formularios/Principal/MainWindow.xaml.cs` — 5 cambios puntuales

Eliminado el método `Navigate(string id)` con su switch de 13 cases.
Cada llamador actualizado directamente:

| Antes | Después |
|---|---|
| `Vm.NavBienvenidaCommand.Execute(null)` | `Vm.NavigateCommand.Execute("bienvenida")` |
| `Vm.NavMiUsuarioCommand.Execute(null)` | `Vm.NavigateCommand.Execute("mi-usuario")` |
| `Navigate(subId)` (en BtnSub_Click) | `Vm.NavigateCommand.Execute(subId)` |
| `Navigate(id)` (en BtnModuloDirect_Click) | `Vm.NavigateCommand.Execute(id)` |
| `Vm.NavBusquedaCommand?.Execute(text)` | `Vm.BuscarCommand.Execute(text)` |

> [!note] Lo que NO cambió en el code-behind
> `BtnSub_Click` y `BtnModuloDirect_Click` siguen existiendo — gestionan estado visual
> (dots activos, indicadores de módulo, animaciones). Eso es responsabilidad legítima de la View.

---

## Resultado

- **Build:** 0 errores ✅
- **Hallazgos resueltos:** #1 (triple indirección), #2 (ViewModelBase manual), #3 (service locator)
- **Líneas eliminadas:** ~80 (18 declaraciones + 18 inits + switch de 13 cases)
- **Agregar módulo nuevo ahora requiere:** editar `_routes` en MainViewModel + agregar botón en XAML

---

## Patrón para módulos futuros con DI

Cuando un módulo placeholder se convierta en VM real con dependencias:

```csharp
// 1. Registrar en App.xaml.cs
services.AddTransient<UsuariosViewModel>();

// 2. Inyectar en MainViewModel constructor
public MainViewModel(..., UsuariosViewModel usuariosVm)

// 3. Actualizar la ruta
["usuarios-sub"] = () => _usuariosVm,
```

---

## Estado del plan

| Fase | Descripción | Estado |
|---|---|---|
| 1 | MainViewModel → ObservableObject + NavigateCommand único | ✅ Completada |
| 2 | Limpiar switch en code-behind | ✅ Completada (incluida en Fase 1) |
| 3 | Inyectar UniversalSearchViewModel | ✅ Completada (incluida en Fase 1) |
| 4 | Constantes de ruta `Routes.cs` | ⬜ Pendiente |
| — | Decisión INavigationService | 🤔 Pendiente consulta |

---

*Relacionado: [[Plan de Refactor - Navegación MainViewModel]] · [[CommunityToolkit.Mvvm]] · [[Arquitectura Actual]]*
