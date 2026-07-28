# Proyecto Bimbo Honduras — Contexto para Claude

## ¿Qué es este proyecto?
Portal interno de gestión para **Bimbo Honduras**. Maneja catálogo de productos, operaciones de pesaje, usuarios y empleados. App de escritorio **.NET 8** con arquitectura híbrida **WPF + WinForms**.

## Ubicación
`C:\Users\fbara\OneDrive\Desktop\Proyecto de BIMBO\BimboProyecto\`  
Solución: `BimboProyecto.sln`

---

## Arquitectura de capas

```
CapaUI → CapaAplicacion ← CapaDatos
              ↓
         CapaDominio
```

| Proyecto | Rol | Dependencias |
|---|---|---|
| `BimboPesaje/` | App WinForms (host) + WPF embebido | — |
| `CapaUI/` | Vistas WPF + ViewModels | CapaAplicacion, CapaDatos, CapaDominio |
| `CapaAplicacion4/` | Interfaces, DTOs, estrategias de búsqueda | CapaDominio |
| `CapaDatos/` | Implementaciones Supabase, repositorios | CapaDominio, **CapaAplicacion** |
| `CapaDominio/` | Entidades de dominio, interfaces genéricas | — |
| `CapaServicios/` | Sesión, notificaciones, realtime | — |
| `ServicioConexión/` | Singleton del cliente Supabase | — |

> **Regla de oro:** `CapaAplicacion` nunca referencia `CapaDatos`. La dependencia va en sentido contrario — `CapaDatos` implementa contratos definidos en `CapaAplicacion`.

---

## Shell de navegación
- **WinForms host:** `BimboPesaje/Formularios/MenuPrincipal/FrmMenuPrincipal.cs`
- **WPF shell:** `CapaUI/Formularios/Principal/MainWindow.xaml`
- Vistas WPF se cargan con `_shell.ShowWpfView(view)` desde `FrmMenuPrincipal.OnNavigationRequested`
- Para agregar pantalla nueva: `UserControl` + `ViewModel` → registrar en `FrmMenuPrincipal` con `case "key-pantalla"` → registrar en DI en `App.xaml.cs`

---

## Inyección de dependencias (`CapaUI/App.xaml.cs`)

```csharp
public static IServiceProvider Services { get; private set; }

private static IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();
    services.AddDataLayer();        // CapaDatos/DependencyInjection.cs
    services.AddApplicationLayer(); // CapaAplicacion4/DependencyInjection.cs
    services.AddTransient<ProductosViewModel>();
    services.AddTransient<UniversalSearchViewModel>();
    return services.BuildServiceProvider();
}
```

Los ViewModels se resuelven en la vista con:
```csharp
_vm = App.Services.GetRequiredService<ProductosViewModel>();
```

---

## Dos rutas para Productos

El módulo Productos tiene dos caminos separados e independientes:

| Ruta | Propósito | Entidad | Interfaz | Implementación |
|---|---|---|---|---|
| **Buscador universal** | Búsqueda global en la app | `Producto` (dominio) | `IRepository<Producto>` | `ProductoSearchRepository` |
| **Formulario Productos** | CRUD, paginación, filtros | `ProductoDto` (aplicación) | `IProductoRepository` | `ProductoCrudRepository` |

---

## Módulo Productos — Archivos clave

### CapaAplicacion4/Productos/
```
Dtos/
  ProductoDto.cs       — DTO completo con FKs (Id, Nombre, IdFabricante, IdPais, etc.)
  FiltroItem.cs        — { int? Id, string Nombre } para ComboBox de filtros
Queries/
  PagedResult.cs       — { IReadOnlyList<T> Items, int Total, int Activos, int Inactivos }
  ProductoFiltros.cs   — { int? IdEstado, int? IdFabricante, int? IdPais }
Interfaces/
  IProductoRepository.cs — contrato para el formulario
```

### `IProductoRepository` (contrato del formulario)
```csharp
Task<PagedResult<ProductoDto>>   GetPagedAsync(int page, int size, ProductoFiltros filtros, ct);
Task<IReadOnlyList<ProductoDto>> BuscarSugerenciasAsync(string termino, ProductoFiltros filtros, ct);
Task<IReadOnlyList<FiltroItem>>  GetFabricantesAsync(ct);
Task<IReadOnlyList<FiltroItem>>  GetPaisesAsync(ct);
```

### CapaDatos/Repositories/
```
Productos/
  ProductoCrudRepository.cs    — implementa IProductoRepository (paginación, sugerencias, filtros)
Search/
  ProductoSearchRepository.cs  — implementa IRepository<Producto> (buscador universal, ILike server-side)
  EmpleadoRepository.cs
  ClienteRepository.cs
  SupabaseRepository.cs        — base abstracta con GetClientAsync() y MapToDomain()
```

### CapaUI/.../Pantallas/Productos/
```
ProductosViewModel.cs   — hereda ObservableObject, inyecta IProductoRepository
ProductosView.xaml.cs   — resuelve VM desde DI, usa ProductoDto
ProductoModal.xaml.cs   — acepta ProductoDto? en constructor
```

---

## Patrones MVVM (CommunityToolkit.Mvvm 8.x)

**Siempre usar en ViewModels de WPF:**

```csharp
public partial class MiViewModel : ObservableObject
{
    // Propiedad simple → fuente generator
    [ObservableProperty] private string _nombre = "";

    // Con notificaciones encadenadas
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(EditarCommand))]
    private MiDto? _seleccionado;

    // Comando simple
    [RelayCommand]
    private void Guardar() { ... }

    // Comando con CanExecute
    [RelayCommand(CanExecute = nameof(HaySeleccionado))]
    private void Editar() { ... }

    public bool HaySeleccionado => _seleccionado is not null;
}
```

- La clase debe ser `partial` para los source generators
- `[RelayCommand]` en método `Foo()` genera `FooCommand`
- `[ObservableProperty]` en campo `_foo` genera propiedad `Foo`
- Para comandos de paginación: llamar `XxxCommand.NotifyCanExecuteChanged()` manualmente

**NO usar:**
- `INotifyPropertyChanged` manual
- `RelayCommand` local por módulo (ej. `ProductosRelayCommand`)
- Constructor sin parámetros en ViewModels que necesiten repositorios

---

## Repositorios — Convenciones

### Supabase (CapaDatos)

```csharp
var client = await ConexionSupabase.GetClientAsync(); // singleton lazy, siempre await

// Filtros server-side
query = query.Filter("id_estado", Op.Equals, idEstado.Value.ToString());

// Paginación server-side
int from = (page - 1) * size;
query = query.Range(from, from + size - 1);

// ILike
query = query.Filter("nombre_producto", Op.ILike, $"%{termino}%");

// OR multi-columna — SIEMPRE con .Or() + QueryFilter.
// ⚠️ NUNCA usar Filter("or", Op.Equals, "(...)"): compila, no lanza error,
// y retorna resultados silenciosamente incorrectos.
// Ver [[Bug - Filter OR con Op.Equals en postgrest-csharp]].
query.Or(new List<IPostgrestQueryFilter>
{
    new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
    new QueryFilter("codigo_producto", Op.ILike, $"%{termino}%"),
})

// OR que cruza tablas joineadas (ej. usuarios + empleados): PostgREST NO lo
// permite — crear una vista SQL con security_invoker que aplane la columna.
// Ver [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]].
```

### Convención de columnas
- BD: `snake_case` → C#: `camelCase` con `[Column("nombre_columna")]`
- PK: `[PrimaryKey("id_xxx")]`
- Estado: `id_estado = 1` (habilitado), `id_estado = 2` (deshabilitado)
- Select con joins: `"*, presentacion_producto(*), fabricante(*), categoria(*), paises(*)"`

### Nota crítica
- `Fabricante` **NO hereda `BaseModel`** → nunca `client.From<Fabricante>()`. Cargar siempre vía join de productos.

---

## Buscador Universal

- Estrategias en `CapaAplicacion4/Search/Strategies/`
- Cada estrategia implementa `ISearchStrategy` e inyecta `IRepository<TEntidad>`
- `IRepository<T>` solo tiene `SearchAsync(string term, ct)` — sin expresiones LINQ
- Los filtros server-side van en el repositorio concreto (`ProductoSearchRepository`, `EmpleadoRepository`, etc.)
- Registradas en DI como `ISearchStrategy` (múltiples implementaciones)

---

## Búsqueda con sugerencias (ProductosView)

- El buscador es **siempre** `controls:SuggestionSearchBox`, nunca un `TextBox` plano
- **No hay code-behind de sugerencias.** El ViewModel expone una sola propiedad ya mapeada y el control se bindea directo:

```csharp
[ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;  // null o vacía = popup cerrado
private readonly SuggestionDebouncer _buscador = new();

private Task RefrescarSugerenciasAsync() => _buscador.EjecutarAsync(
    _query,
    async (q, ct) =>
    {
        var r = await _repo.BuscarSugerenciasAsync(q, BuildFiltros(), ct);
        return r.Success ? r.Value!.Select(Map).ToList() : null;
    },
    items => { SuggestItems = items; HighlightIndex = -1; });

private static SuggestionItemData Map(ProductoDto p) => new() { … };
```

```xml
<controls:SuggestionSearchBox
    Query="{Binding Query, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
    SuggestItems="{Binding SuggestItems}"
    HighlightIndex="{Binding HighlightIndex, Mode=TwoWay}"
    ItemSelected="SearchBox_ItemSelected"/>
```

- Debounce 300ms en `CapaUI/Core/Controls/SuggestionDebouncer.cs` — **un solo lugar** para los 9 módulos. Por composición, no herencia (los VMs heredan de bases distintas). Liberar con `_buscador.Dispose()` en `OnDispose()`/`Dispose()`
- Lo único que varía por módulo: la lambda (qué repositorio) y `Map` (cómo se ve la sugerencia)
- `SeleccionarSugerencia(...)` debe empezar con `_buscador.Cancelar()`: asigna al campo `_query` y no a la propiedad, así que no pasa por el setter y el debounce en vuelo quedaría vivo reabriendo el popup
- Highlight via `ListBox` + `SelectedIndex="{Binding HighlightIndex, Mode=OneWay}"` + triggers `IsSelected`/`IsMouseOver`. `VisualTreeHelper` fue eliminado al resolver P-005 (2026-05-28); `OneWay` es deliberado — en `TwoWay`, cambiar el `ItemsSource` reescribiría `HighlightIndex` en el VM
- Al seleccionar sugerencia: buscar en `PageRows` por `Id`, **NO insertar** en la colección
- El ViewModel **no pre-selecciona**: `HighlightIndex = -1` siempre al llegar sugerencias nuevas
- ⚠️ **Nunca** exponer un `bool ShowSuggestions` como señal de "mostrar el popup": `[ObservableProperty]` no notifica cuando el valor no cambia, y el popup se congela. Una sola señal (`SuggestItems`) — ver P-026

---

## Modals

- Overlay `ModalOverlay` con fade (200ms entrada, 150ms salida)
- `Guardado` del modal dispara `_vm.RefrescarDatos()` en la vista padre
- Constructor recibe `ProductoDto?` (null = nuevo registro)
- `CanUserAddRows="False"` en todos los DataGrid

---

## Base de datos: Supabase — Tablas principales

| Tabla | Modelo C# | Notas |
|---|---|---|
| `productos` | `CapaDatos/Modelados/Productos/Productos.cs` | Join con fabricante, paises, categoria, presentacion |
| `fabricante` | `Fabricante.cs` | NO hereda BaseModel |
| `paises` | `Paises.cs` | |
| `categoria` | `Categoria.cs` | |
| `presentacion_producto` | `Presentacion.cs` | |
| `empleados` | `Empleados.cs` | |
