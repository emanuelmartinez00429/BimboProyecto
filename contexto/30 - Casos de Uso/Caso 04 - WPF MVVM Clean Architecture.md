---
title: "Caso 04 — WPF Desktop App con MVVM y Clean Architecture"
tags:
  - caso-de-uso
  - wpf
  - mvvm
  - desktop
  - dotnet
---

# Caso 04 — WPF Desktop App con MVVM y Clean Architecture

> [!example] Caso real
> **Referencia:** Aplicaciones WPF empresariales con CommunityToolkit.Mvvm, DI, y capas separadas  
> **Fuente:** [MVVM in .NET — DEV Community](https://dev.to/adrianbailador/mvvm-in-net-37k6) + [Avalonia Clean Architecture — Medium](https://medium.com/c-sharp-programming/avalonia-and-reactiveui-mvvm-di-clean-architecture-67fe4777d463)

---

## El patrón completo MVVM + Clean Architecture en Desktop

```
View (.xaml + .xaml.cs)
    ↓ DataBinding / Commands
ViewModel (ObservableObject)
    ↓ IProductoRepository (abstracción)
Application Layer (DTOs, Interfaces)
    ↑ implementa
Infrastructure (Repository → Supabase / EF Core / SQLite)
```

---

## Lo que hace que este caso sea idéntico a Bimbo

### 1. DI en App.xaml.cs

```csharp
// Punto de composición — único lugar donde Infrastructure y Application se tocan
protected override void OnStartup(StartupEventArgs e)
{
    var services = new ServiceCollection();
    services.AddScoped<IProductoRepository, ProductoCrudRepository>();
    services.AddTransient<ProductosViewModel>();
    Services = services.BuildServiceProvider();
}
```

### 2. ViewModel sin `new` — resuelto desde DI

```csharp
// View resuelve su ViewModel del contenedor
private async void UserControl_Loaded(object sender, RoutedEventArgs e)
{
    _vm = App.Services.GetRequiredService<ProductosViewModel>();
    DataContext = _vm;
    await _vm.CargarDatosAsync();
}
```

### 3. ViewModel no instancia nada — solo inyecta

```csharp
public partial class ProductosViewModel : ObservableObject
{
    private readonly IProductoRepository _repo;

    // Constructor limpio — sin new ProductoCrudRepository()
    public ProductosViewModel(IProductoRepository repo) => _repo = repo;
}
```

---

## Por qué esto importa

> [!success] Beneficios concretos
> - **Testeable:** puedes crear `new ProductosViewModel(new MockProductoRepository())` en tests
> - **Intercambiable:** cambiar Supabase por SQLite solo afecta el registro en DI
> - **Legible:** el ViewModel no sabe quién le provee los datos

---

## Errores comunes en WPF sin Clean Architecture

```csharp
// ❌ ViewModel con lógica de acceso directo
public class ProductosViewModel
{
    public async Task CargarAsync()
    {
        var client = await ConexionSupabase.GetClientAsync(); // acoplado
        var data = await client.From<Productos>().Get();      // acoplado
    }
}

// ✅ ViewModel desacoplado
public class ProductosViewModel
{
    public async Task CargarAsync()
    {
        var pagina = await _repo.GetPagedAsync(1, 50, new ProductoFiltros());
    }
}
```

---

## Relaciones

- [[Observer Pattern]] — El corazón del data binding en WPF
- [[Repository Pattern]] — Lo que el ViewModel inyecta
- [[Clean Architecture]] — La estructura que lo hace posible
- [[Módulo Productos]] — Implementación real en Bimbo
- [[CommunityToolkit.Mvvm]] — La herramienta que reduce el boilerplate
