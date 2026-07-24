---
title: Convenciones C# — Bimbo
tags:
  - referencia
  - convenciones
  - dotnet
---

# Convenciones C# — Bimbo

---

## Naming

| Elemento | Convención | Ejemplo |
|---|---|---|
| Clases | PascalCase | `ProductoCrudRepository` |
| Interfaces | I + PascalCase | `IProductoRepository` |
| Campos privados | `_camelCase` | `_fabricanteIdFiltro` |
| Propiedades | PascalCase | `FabricanteIdFiltro` |
| Métodos async | Sufijo `Async` | `GetPagedAsync` |
| DTOs | Sufijo `Dto` | `ProductoDto` |
| Resultados paginados | Prefijo `Paged` | `PagedResult<T>` |
| Filtros | Sufijo `Filtros` | `ProductoFiltros` |

---

## Estructura de ViewModel

```csharp
public partial class XxxViewModel : ObservableObject
{
    // 1. Repositorio(s) inyectados
    private readonly IXxxRepository _repo;

    // 2. Estado privado (no observable)
    private int _page = 1;

    // 3. Propiedades observables simples
    [ObservableProperty] private bool _isLoading;

    // 4. Propiedades observables con notificaciones encadenadas
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccionado))]
    private XxxDto? _seleccionado;

    // 5. Propiedades calculadas (readonly)
    public bool HaySeleccionado => _seleccionado is not null;

    // 6. Propiedades con side effects (manual)
    public int Page { get => _page; set { _page = value; OnPropertyChanged(); _ = CargarAsync(); } }

    // 7. Eventos para comunicación VM → View
    public event Action<XxxDto>? SolicitarEditar;

    // 8. Constructor con DI
    public XxxViewModel(IXxxRepository repo) => _repo = repo;

    // 9. Métodos públicos (llamados por la View)
    public async Task CargarDatosAsync() { ... }

    // 10. Commands
    [RelayCommand] private void Nuevo() => ...;
    [RelayCommand(CanExecute = nameof(HaySeleccionado))] private void Editar() => ...;

    // 11. Métodos privados
    private async Task CargarPaginaAsync() { ... }
}
```

---

## Reglas DRY en el proyecto

> [!tip] No repetir
> - `BuildFiltros()` centraliza la construcción de `ProductoFiltros` — no repetir la lógica del switch de estado en cada método
> - `Map(Productos p)` en el repositorio — un solo lugar para mapear modelo → DTO
> - `SupabaseRepository<TDomain, TSupabase>` — base abstracta que centraliza la conexión

---

## Reglas de arquitectura

```
✅ ViewModel → IProductoRepository (interfaz)
❌ ViewModel → ProductoCrudRepository (implementación)

✅ CapaDatos → implementa IProductoRepository
❌ CapaAplicacion → referencia CapaDatos

✅ ProductoDto en Application Layer (tiene FKs para UI)
✅ Producto en Domain Layer (sin FKs, entidad limpia)
❌ Usar Productos (modelo Supabase) fuera de CapaDatos
```

---

## Relaciones

- [[Clean Architecture]] — Por qué estas reglas existen
- [[SOLID]] — Los principios detrás de las convenciones
- [[CommunityToolkit.Mvvm]] — Herramienta para los ViewModels
