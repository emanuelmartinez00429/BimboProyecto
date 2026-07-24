---
title: CommunityToolkit.Mvvm
tags:
  - referencia
  - wpf
  - mvvm
  - dotnet
aliases:
  - ObservableObject
  - RelayCommand
  - ObservableProperty
---

# CommunityToolkit.Mvvm

> [!info] Versión usada: 8.4.0

---

## Reglas de uso en Bimbo

1. El ViewModel **debe** ser `partial class`
2. Los campos para `[ObservableProperty]` deben ser `private` y en `camelCase` con `_`
3. `[RelayCommand]` genera `{Método}Command`

---

## [ObservableProperty]

```csharp
// Campo → genera propiedad pública con OnPropertyChanged automático
[ObservableProperty] private bool _isLoading;
// → genera: public bool IsLoading { get; set; } con notificación

// Con notificaciones encadenadas
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HaySeleccionado), nameof(TextoSeleccionado))]
[NotifyCanExecuteChangedFor(nameof(EditarCommand))]
private ProductoDto? _seleccionado;
```

---

## [RelayCommand]

```csharp
// Comando simple
[RelayCommand]
private void Guardar() { ... }
// → genera: public IRelayCommand GuardarCommand

// Con CanExecute
[RelayCommand(CanExecute = nameof(HaySeleccionado))]
private void Editar() { ... }

// Async
[RelayCommand]
private async Task CargarAsync() { ... }
// → genera: public IAsyncRelayCommand CargarCommand
```

---

## Notificar CanExecute manualmente

```csharp
// Cuando la condición de CanExecute depende de algo externo
private void NotifyPaginationCanExecuteChanged()
{
    PrimeraPaginaCommand.NotifyCanExecuteChanged();
    PaginaAnteriorCommand.NotifyCanExecuteChanged();
    PaginaSiguienteCommand.NotifyCanExecuteChanged();
    UltimaPaginaCommand.NotifyCanExecuteChanged();
}
```

---

## Partial methods para side effects

```csharp
// Cuando [ObservableProperty] necesita lógica adicional al cambiar
partial void OnIsLoadingChanged(bool value)
{
    // Se llama automáticamente cuando IsLoading cambia
}
```

---

## Relaciones

- [[Observer Pattern]] — ObservableObject implementa el patrón Observer
- [[Módulo Productos]] — ProductosViewModel usa todo esto
