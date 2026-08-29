---
title: Sesión 2026-05-23 — INavigationService eliminado + IPickerService creado
type: sesion
status: vigente
tags:
  - bitacora
  - arquitectura
  - navegacion
  - picker
  - refactor
date: 2026-05-23
updated: 2026-05-23
summary: "El caso de uso real identificado fue el picker/lookup modal: abrir un buscador de otra entidad desde un formulario de edición y retornar el registro…"
scope:
  - CapaUI/Services
  - CapaUI/Services/Navigation
  - CapaUI/Services/Picker
  - CapaUI/Services/Picker/Views
symbols:
  - INavigationService
  - IPickerService
  - MainViewModel
  - ResultSelected
---

# Sesión 2026-05-23 — INavigationService eliminado + IPickerService creado

## Decisión tomada

`INavigationService` fue eliminado. Era código muerto (ningún ViewModel lo consumía realmente)
y resolvía un problema diferente al que el proyecto necesita.

El caso de uso real identificado fue el **picker/lookup modal**: abrir un buscador de otra entidad
desde un formulario de edición y retornar el registro seleccionado. Eso es `IPickerService`.

---

## Archivos eliminados

| Archivo | Razón |
|---|---|
| `CapaUI/Services/Navigation/INavigationService.cs` | Reemplazado por IPickerService |
| `CapaUI/Services/Navigation/NavigationService.cs` | Abría EntityDetailWindow (stub vacío) |
| `CapaUI/Services/Navigation/EntityDetailWindow.cs` | Stub sin implementación real |

---

## UniversalSearchViewModel — refactor del SelectResult

**Antes:** dependía de `INavigationService` para navegar al hacer clic en un resultado.

```csharp
// ❌ ViewModel dependía de servicio de navegación
public UniversalSearchViewModel(IMediator mediator, INavigationService navigation)
[RelayCommand]
private void SelectResult(SearchResultDto result)
    => _navigation.NavigateTo(result.EntityType, result.NavigationParam);
```

**Después:** expone evento `ResultSelected`. El ViewModel no sabe nada de navegación.

```csharp
// ✅ ViewModel solo notifica; el consumidor decide cómo navegar
public UniversalSearchViewModel(IMediator mediator)
public event Action<SearchResultDto>? ResultSelected;

[RelayCommand]
private void SelectResult(SearchResultDto result)
    => ResultSelected?.Invoke(result);
```

---

## MainViewModel — manejo del evento ResultSelected

`MainViewModel` suscribe al evento en su constructor y mapea `EntityType → routeId`:

```csharp
_searchVm.ResultSelected += OnResultadoBusquedaSeleccionado;

private void OnResultadoBusquedaSeleccionado(SearchResultDto result)
{
    var routeId = result.EntityType switch
    {
        "Producto" => Routes.Productos,
        "Empleado" => Routes.Empleados,
        _          => null
    };
    if (routeId is not null)
        Navigate(routeId);
}
```

> [!note] Extensión futura
> Cuando los módulos tengan vista de detalle, este switch puede retornar
> una ruta de detalle (`Routes.DetalleProducto`) en vez del módulo general.

---

## IPickerService — nuevo servicio creado

### Patrón: Picker / Lookup Modal

Abre un modal de búsqueda desde un formulario de edición y retorna el registro seleccionado.
El usuario cancela → retorna `null`.

```
FormularioEdición
  → [RelayCommand] SeleccionarProducto()
    → await _pickerService.PickProductoAsync()
      → modal de búsqueda abierto (overlay)
        → usuario selecciona → retorna ProductoDto
        → usuario cancela → retorna null
```

### Archivos creados

| Archivo | Capa | Descripción |
|---|---|---|
| `CapaUI/Services/Picker/IPickerService.cs` | CapaUI | Contrato del servicio |
| `CapaUI/Services/Picker/PickerService.cs` | CapaUI | Implementación (stub por ahora) |

### Por qué el contrato está en CapaUI (no en CapaAplicacion)

El picker abre modales WPF — es inherentemente una responsabilidad de la capa de Presentación.
Solo los ViewModels de CapaUI lo necesitan. No hay motivo para colocarlo en Application.

### Registro en DI

```csharp
// App.xaml.cs
services.AddSingleton<IPickerService, PickerService>();
```

Singleton porque el servicio no tiene estado propio — es stateless.

### Cómo implementar un picker cuando se necesite

```csharp
// 1. Crear UserControl con buscador + DataGrid
//    CapaUI/Services/Picker/Views/ProductoPicker.xaml

// 2. En PickerService.PickProductoAsync():
var tcs = new TaskCompletionSource<ProductoDto?>();
var picker = new ProductoPicker(tcs);
ModalHelper.Mostrar(picker);        // overlay sobre la ventana activa
ct.Register(() => tcs.TrySetResult(null));
return await tcs.Task;

// 3. Uso en cualquier ViewModel:
[RelayCommand]
private async Task SeleccionarProducto()
{
    var producto = await _pickerService.PickProductoAsync();
    if (producto is null) return; // usuario canceló
    ProductoId   = producto.Id;
    NombreProducto = producto.Nombre;
}
```

### Pickers futuros a agregar en IPickerService

```csharp
// Descomentar y agregar a PickerService cuando el módulo esté listo:
Task<EmpleadoDto?>  PickEmpleadoAsync(ct);
Task<ProveedorDto?> PickProveedorAsync(ct);
Task<ClienteDto?>   PickClienteAsync(ct);
```

---

## Estado final de servicios en CapaUI/Services/

```
Services/
  Picker/
    IPickerService.cs   ✅ nuevo
    PickerService.cs    ✅ nuevo (stub)
  (Navigation/ eliminado)
```

---

## Build: 0 errores ✅

---

*Relacionado: [[Plan de Refactor - Navegación MainViewModel]] · [[Arquitectura Actual]] · [[Clean Architecture]]*
