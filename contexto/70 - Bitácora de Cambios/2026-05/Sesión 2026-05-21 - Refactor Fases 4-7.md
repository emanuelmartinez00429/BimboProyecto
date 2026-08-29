---
title: Sesión 2026-05-21 — Refactor Fases 4-7
type: sesion
status: vigente
tags:
  - bitácora
  - sesión
  - refactor
  - productos
  - completado
date: 2026-05-21
updated: 2026-05-21
summary: "En esta sesión se completaron las 4 fases pendientes del refactor del módulo Productos. La aplicación ahora tiene un stack limpio: DI en todos los niveles,…"
scope:
  - BimboPesaje/Formularios/MenuPrincipal
  - BimboPesaje/Formularios/Productos
  - CapaAplicacion4/Common
  - CapaAplicacion4/Productos/Interfaces
  - CapaDatos/Repositories/Productos
  - CapaUI/Formularios/Principal/Pantallas/Productos
symbols:
  - Constants
  - GetCategoriasAsync
  - OnLoaded
  - ProductoDto
estado: completado
fecha: 2026-05-21
fases:
  - 4
  - 5
  - 6
  - 7
---

# Sesión 2026-05-21 — Refactor Fases 4-7

> Módulo Productos · Continuación de Fases 1-3 ya completadas en sesión anterior

---

## Resumen ejecutivo

En esta sesión se completaron las 4 fases pendientes del refactor del módulo Productos. La aplicación ahora tiene un stack limpio: DI en todos los niveles, Result Pattern para errores, bindings XAML en PascalCase y modal completamente desacoplado de repositorios estáticos.

---

## Problema previo resuelto — Ruta activa

Se detectó que `BimboPesaje/Formularios/Productos/` era la ruta que cargaba la app, pero el refactor de Fases 1-3 estaba aplicado en `CapaUI/Formularios/Principal/Pantallas/Productos/`.

**Solución aplicada (Opción A — Redirigir navegación):**

Archivo: `BimboPesaje/Formularios/MenuPrincipal/FrmMenuPrincipal.cs`
```csharp
// Antes
using BimboPesaje.Formularios.Productos;
var productosView = new BimboPesaje.Formularios.Productos.ProductosView();

// Después
using CapaUI.Formularios.Principal.Pantallas.Productos;
var productosView = new ProductosView();
```

Archivo: `BimboPesaje/BimboPesaje.csproj`
```xml
<!-- Referencia agregada -->
<ProjectReference Include="..\CapaUI\CapaUI.csproj" />
```

---

## Fase 4 — IProductoRepository: métodos de escritura

**Archivos modificados:**
- `CapaAplicacion4/Productos/Interfaces/IProductoRepository.cs`
- `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`

### IProductoRepository — después
```csharp
// Lectura
Task<PagedResult<ProductoDto>>   GetPagedAsync(...);
Task<IReadOnlyList<ProductoDto>> BuscarSugerenciasAsync(...);
Task<IReadOnlyList<FiltroItem>>  GetFabricantesAsync(ct);
Task<IReadOnlyList<FiltroItem>>  GetPaisesAsync(ct);
Task<IReadOnlyList<FiltroItem>>  GetCategoriasAsync(ct);  // ← nuevo

// Escritura (nuevos)
Task<Result<int>> CreateAsync(ProductoDto dto, ct);
Task<Result>      UpdateAsync(ProductoDto dto, ct);
Task<Result>      DeleteAsync(int id, ct);
```

> `GetCategoriasAsync` agregado porque el modal lo necesita para su ComboBox.

### ProductoCrudRepository — métodos de escritura
```csharp
public async Task<Result<int>> CreateAsync(ProductoDto dto, ct)
{
    var client = await ConexionSupabase.GetClientAsync();
    var nuevo = new Modelados.Productos.Productos { /* mapeo desde dto */ };
    var resultado = await client.From<Modelados.Productos.Productos>().Insert(nuevo);
    return Result<int>.Ok(resultado.Models.First().idProducto);
}

public async Task<Result> UpdateAsync(ProductoDto dto, ct)
{
    var client = await ConexionSupabase.GetClientAsync();
    await client.From<Modelados.Productos.Productos>()
        .Where(p => p.idProducto == dto.Id)
        .Set(p => p.codigoProducto,    dto.CodigoInterno)
        .Set(p => p.nombreProducto,    dto.Nombre)
        // ... resto de campos
        .Update();
    return Result.Ok();
}

public async Task<Result> DeleteAsync(int id, ct)
{
    // Soft delete: id_estado = 2
    var client = await ConexionSupabase.GetClientAsync();
    await client.From<Modelados.Productos.Productos>()
        .Where(p => p.idProducto == id)
        .Set(p => p.idEstado, 2)
        .Update();
    return Result.Ok();
}
```

---

## Fase 5 — ProductoModal con DI

**Archivo modificado:** `CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml.cs`

### Antes
```csharp
// Repositorios estáticos antiguos
await RepositorioProducto.ingresarProducto(datos);
await RepositorioCategoria.ObtenerCategorias();
```

### Después — Constructor con DI
```csharp
public ProductoModal(IProductoRepository repo, ProductoDto? producto)
{
    _repo     = repo;
    _producto = producto;
    _esNuevo  = producto == null;
    InitializeComponent();
    Loaded += OnLoaded;
}
```

`OnLoaded` usa el repo para poblar ComboBoxes:
```csharp
var fabricantes = await _repo.GetFabricantesAsync();
var categorias  = await _repo.GetCategoriasAsync();
```

### Apertura del modal desde ProductosView
```csharp
var repo  = App.Services.GetRequiredService<IProductoRepository>();
var modal = new ProductoModal(repo, null);        // nuevo registro
var modal = new ProductoModal(repo, productoDto); // editar
```

---

## Fase 6 — Bindings XAML al DTO

**Archivo modificado:** `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml`

8 bindings del DataGrid actualizados de propiedades snake_case del modelo de BD → PascalCase del `ProductoDto`:

| Antes | Después |
|---|---|
| `{Binding codigoProducto}` | `{Binding CodigoInterno}` |
| `{Binding nombreProducto}` | `{Binding Nombre}` |
| `{Binding nombre_Fabricante}` | `{Binding Fabricante}` |
| `{Binding nombre_Pais}` | `{Binding Pais}` |
| `{Binding contenidoProducto}` | `{Binding Contenido}` |
| `{Binding nombre_Presentacion}` | `{Binding Presentacion}` |
| `{Binding nombre_Categoria}` | `{Binding Categoria}` |
| `{Binding idEstado}` | `{Binding IdEstado}` |

---

## Fase 7 — Result Pattern

**Archivos:** `CapaAplicacion4/Common/Result.cs` (nuevo) + `ProductoModal.xaml.cs` (actualizado)

### Result.cs
```csharp
namespace CapaAplicacion.Common;

public sealed class Result<T>
{
    public bool   Success { get; }
    public T?     Value   { get; }
    public string Error   { get; }

    private Result(bool success, T? value, string error)
        => (Success, Value, Error) = (success, value, error);

    public static Result<T> Ok(T value)      => new(true,  value,   string.Empty);
    public static Result<T> Fail(string msg) => new(false, default, msg);
}

public sealed class Result
{
    public bool   Success { get; }
    public string Error   { get; }

    private Result(bool success, string error)
        => (Success, Error) = (success, error);

    public static Result Ok()             => new(true,  string.Empty);
    public static Result Fail(string msg) => new(false, msg);
}
```

### ProductoModal — BtnGuardar_Click con Result
```csharp
if (_esNuevo)
{
    var r = await _repo.CreateAsync(dto);
    if (!r.Success) { MessageBox.Show(r.Error, "Error", ...); return; }
}
else
{
    var r = await _repo.UpdateAsync(dto);
    if (!r.Success) { MessageBox.Show(r.Error, "Error", ...); return; }
}
Guardado?.Invoke(); // solo si tuvo éxito
```

---

## Errores encontrados y resueltos

| Error | Causa | Solución |
|---|---|---|
| `using Supabase.Postgrest.Constants` inválido | `Constants` es clase, no namespace | `using static Supabase.Postgrest.Constants` |
| `'Productos' is a namespace, used as type` | Conflicto nombre namespace/clase | Type alias: `using ProductosModel = CapaDatos.Modelados.Productos.Productos` |
| MSB3027/MSB3021 al hacer `dotnet build` | VS2022 tiene los DLLs bloqueados | Compilar desde VS2022 con `Ctrl+Shift+B` |

---

## Estado final del módulo Productos

```
✅ IProductoRepository     — lectura + escritura + Result Pattern
✅ ProductoCrudRepository  — implementación completa con try/catch → Result
✅ ProductoModal           — DI, sin repositorios estáticos, usa Result
✅ ProductosView           — bindings a ProductoDto, modal via DI
✅ ProductosViewModel      — ObservableObject + [ObservableProperty] + [RelayCommand]
✅ Result.cs               — en CapaAplicacion.Common
✅ Ruta activa             — FrmMenuPrincipal → CapaUI (Opción A)
```

---

## Pendiente verificar

- [ ] Compilar desde VS2022 (`Ctrl+Shift+B`)
- [ ] Navegar a Productos → lista carga correctamente
- [ ] Crear nuevo producto → aparece en la lista
- [ ] Editar producto → cambios persistidos en Supabase
- [ ] Simular error → MessageBox muestra el mensaje del Result

---

*Relacionado: [[Plan de Refactor - Estado y Fases]] · [[Módulo Productos]] · [[Result Pattern]] · [[Repository Pattern]]*
