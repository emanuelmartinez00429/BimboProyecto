---
title: "Convenciones de código"
type: patron
status: vigente
date: 2026-05-21
updated: 2026-11-09
summary: "Convenciones de arquitectura, MVVM, repositorios Supabase, inyección de dependencias y controles para BimboProyecto"
autor: fernando
tags: [patron, csharp, mvvm, supabase, wpf]
---

# Convenciones de código — Proyecto Bimbo Honduras

## ¿Qué es este proyecto?
Portal interno de gestión para **Bimbo Honduras**. Maneja catálogo de productos, operaciones de pesaje, usuarios y empleados. App de escritorio **.NET 10** en **WPF** (ejecutable único `CapaUI`).

## Solución y proyectos
Solución principal: `BimboProyecto.sln`.
Ubicación: raíz del repositorio.

---

## Arquitectura de capas

```
CapaUI → CapaAplicacion4 ← CapaDatos
              ↓
         CapaDominio
```

| Proyecto | Rol | Dependencias |
|---|---|---|
| `CapaUI/` | Vistas WPF + ViewModels | CapaAplicacion4, CapaDatos, CapaDominio |
| `CapaAplicacion4/` | Interfaces, DTOs, estrategias de búsqueda | CapaDominio |
| `CapaDatos/` | Implementaciones Supabase, repositorios | CapaDominio, **CapaAplicacion4** |
| `CapaDominio/` | Entidades de dominio, interfaces genéricas | — |

> El cliente Supabase vive en `CapaDatos/Conexion.cs`. No existen `BimboPesaje`, `CapaServicios` (eliminados 2026-05-29) ni `ServicioConexión` (eliminado 2026-09-10, P-056).

> **Regla de oro:** `CapaAplicacion4` nunca referencia `CapaDatos`. La dependencia va en sentido contrario — `CapaDatos` implementa contratos definidos en `CapaAplicacion4`.

---

## Shell de navegación e Inyección de Dependencias

- **WPF Shell:** `CapaUI/Formularios/Principal/MainWindow.xaml`
- Vistas WPF se cargan dentro del shell principal o se abren como ventanas/modales independientes.
- **Inyección de Dependencias pura (Sin Service Locator):**
  Todas las vistas, páginas y modales deben recibir sus dependencias por constructor. Queda prohibido usar Service Locator (`App.Services.GetRequiredService<T>()`) dentro del code-behind de vistas resueltas por el contenedor (Regla de oro 9).

```csharp
// Configuración en CapaUI/App.xaml.cs:
private static IServiceProvider ConfigureServices()
{
    var services = new ServiceCollection();
    services.AddDataLayer();        // CapaDatos/DependencyInjection.cs
    services.AddApplicationLayer(); // CapaAplicacion4/DependencyInjection.cs
    services.AddTransient<ProductosViewModel>();
    services.AddTransient<ProductosView>();
    return services.BuildServiceProvider();
}

// Inyección por constructor en la vista:
public partial class ProductosView : UserControl
{
    public ProductosView(ProductosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
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
  ProductoFiltros.cs   — { int? IdEstado, int? IdFabricante, int? IdPais, int? IdProveedor, int? IdCategoria }
Interfaces/
  IProductoRepository.cs — contrato para el formulario
```

### `IProductoRepository` (contrato del formulario)
```csharp
Task<PagedResult<ProductoDto>>   GetPagedAsync(int page, int size, ProductoFiltros filtros, ct);
Task<IReadOnlyList<ProductoDto>> BuscarSugerenciasAsync(string termino, ProductoFiltros filtros, ct);
Task<IReadOnlyList<FiltroItem>>  GetFabricantesAsync(ct);
Task<IReadOnlyList<FiltroItem>>  GetPaisesAsync(ct);
Task<IReadOnlyList<FiltroItem>>  GetCategoriasAsync(ct);
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

---

## Patrones MVVM (CommunityToolkit.Mvvm 8.x)

**Siempre usar en ViewModels de WPF:**

```csharp
public partial class MiViewModel : ObservableObject
{
    // Propiedad simple → generador de código
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

- La clase debe ser `partial` para los source generators.
- `[RelayCommand]` en método `Foo()` genera `FooCommand`.
- `[ObservableProperty]` en campo `_foo` genera propiedad `Foo`.
- Para comandos de paginación: llamar `XxxCommand.NotifyCanExecuteChanged()` manualmente.

**NO usar:**
- `INotifyPropertyChanged` manual.
- `RelayCommand` local por módulo (ej. `ProductosRelayCommand`).
- Constructor sin parámetros en ViewModels que necesiten repositorios.

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
query.Or(new List<IPostgrestQueryFilter>
{
    new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
    new QueryFilter("codigo_producto", Op.ILike, $"%{termino}%"),
});
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

## Búsqueda con sugerencias (SuggestionSearchBox)

- El buscador es **siempre** `controls:SuggestionSearchBox`, nunca un `TextBox` plano.
- **Sin code-behind de sugerencias.** El ViewModel expone una sola propiedad ya mapeada y el control se bindea directo:

```csharp
[ObservableProperty] private IReadOnlyList<SuggestionItemData>? _suggestItems;
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

- Contrato de `SuggestItems`: `null` = sin búsqueda activa (popup cerrado); lista vacía = búsqueda sin resultados ("Sin resultados"); lista con items = resultados visibles.
- Debounce 300ms en `CapaUI/Core/Controls/SuggestionDebouncer.cs`.
- Liberar con `_buscador.Dispose()` al cerrar.

---

## Modales

- Overlay `ModalOverlay` con fade (200ms entrada, 150ms salida).
- Evento `Guardado` del modal dispara `_vm.RefrescarDatos()` en la vista padre.
- Constructor recibe DTO nullable (null = nuevo registro).
- `CanUserAddRows="False"` en todos los DataGrid.
- Dirty tracking tipado con `ChangeTracker<TSnapshot>` usando un `record` posicional privado para campos editables.

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

---

## Relaciones

- [[Arquitectura Actual]]
- [[Convenciones de UI (WPF) — leer antes de tocar XAML]]
- [[Base Repository con TryAsync]]
