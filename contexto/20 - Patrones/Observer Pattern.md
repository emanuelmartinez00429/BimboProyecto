---
title: Observer Pattern
type: patron
status: vigente
tags:
  - patron
  - comportamiento
  - wpf
  - dotnet
date: 2026-05-21
updated: 2026-05-21
summary: "Define una dependencia uno-a-muchos entre objetos. Cuando un objeto cambia de estado, todos sus dependientes son notificados y actualizados automáticamente."
scope: []
symbols:
  - CanExecute
  - ICommand
aliases:
  - Patrón Observador
  - INotifyPropertyChanged
  - ObservableObject
---

# Observer Pattern

> [!abstract] Definición
> Define una dependencia uno-a-muchos entre objetos. Cuando un objeto cambia de estado, todos sus dependientes son notificados y actualizados automáticamente.

---

## En WPF: el patrón central de MVVM

WPF construye toda la capa de binding sobre Observer. El ViewModel **notifica** a la View cuando sus propiedades cambian; la View **nunca pregunta** — solo escucha.

```
ViewModel (Subject/Observable)
    ↓ PropertyChanged event
WPF Binding Engine (Observer)
    ↓ actualiza
View (TextBlock, DataGrid, Button...)
```

---

## Evolución en el proyecto Bimbo

### Antes (manual — tedioso y propenso a errores)

```csharp
public class ProductosViewModel : INotifyPropertyChanged
{
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

### Después (CommunityToolkit.Mvvm — source generators)

```csharp
public partial class ProductosViewModel : ObservableObject
{
    // El source generator genera la propiedad IsLoading completa
    [ObservableProperty] private bool _isLoading;

    // Con notificaciones encadenadas automáticas
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    private ProductoDto? _seleccionado;
}
```

> [!success] Resultado
> De ~80 líneas de boilerplate a ~5 líneas con el mismo comportamiento.

---

## ObservableCollection como Observer

```csharp
// WPF escucha CollectionChanged automáticamente
ObservableCollection<ProductoDto> _pageRows = new();

// Al hacer esto, el DataGrid se actualiza solo
PageRows = new ObservableCollection<ProductoDto>(pagina.Items);
```

---

## Commands como Observer inverso

Los `ICommand` notifican a WPF cuándo su `CanExecute` cambia:

```csharp
[RelayCommand(CanExecute = nameof(HaySeleccionado))]
private void Editar() { ... }

// Cuando Seleccionado cambia, notifica al comando
[NotifyCanExecuteChangedFor(nameof(EditarCommand))]
private ProductoDto? _seleccionado;
```

---

## Code-behind como Observer manual

Para casos que WPF binding no puede manejar (manipulación de controles, animaciones):

```csharp
_vm.PropertyChanged += (s, ev) =>
{
    if (ev.PropertyName == nameof(ProductosViewModel.HighlightIndex))
        ActualizarHighlight(); // manipula VisualTree manualmente
    if (ev.PropertyName == nameof(ProductosViewModel.ShowSuggestions))
        ActualizarSuggestions();
};
```

---

## Relaciones

- [[Clean Architecture]] — Observer mantiene la separación View/ViewModel
- [[SOLID]] — SRP: la View solo observa, el ViewModel solo notifica
- [[CommunityToolkit.Mvvm]] — Herramienta que implementa Observer automáticamente
- [[Módulo Productos]] — Uso real en ProductosViewModel
