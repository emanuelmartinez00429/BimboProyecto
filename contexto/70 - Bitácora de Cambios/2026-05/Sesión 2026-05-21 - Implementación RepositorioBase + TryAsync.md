---
tags: [bitácora, sesión, error-handling, repositorio, completado]
fecha: 2026-05-21
estado: completado
---

# Sesión 2026-05-21 — Implementación RepositorioBase + TryAsync

> Continuación de la sesión anterior (Fases 4-7). Se diseñó, analizó y ejecutó la estrategia centralizada de manejo de errores para el módulo Productos.

---

## Contexto previo identificado

Se detectó que el sistema tenía **dos estrategias de error coexistiendo**:

| Componente | Estrategia anterior |
|---|---|
| `ProductoCrudRepository` (escritura) | ✅ Result Pattern parcial |
| `ProductoCrudRepository` (lectura) | ❌ Sin protección — explota si Supabase falla |
| `ProductoModal.OnLoaded` | ❌ `catch { }` vacíos — errores silenciosos |
| Repos legacy (`Repositorios/`) | ❌ `catch + throw` — crash en WinForms |

---

## Decisiones de diseño tomadas

### Patrón elegido: RepositorioBase con TryAsync
Una clase base abstracta con dos métodos estáticos protegidos. Todo el `try/catch` del sistema vive ahí. Los repos solo llaman `TryAsync(...)`.

Ver documentación completa: [[Base Repository con TryAsync]]

### Segunda opinión revisada y verificada técnicamente
Se recibió una segunda opinión con 4 observaciones. Análisis completo: [[Sesión 2026-05-21 - Análisis Segunda Opinión TryAsync]]

**Resumen de decisiones:**
- ✅ `OperationCanceledException` se re-lanza (no convierte a `Result.Fail`)
- ✅ `Debug.WriteLine` obligatorio en `TryAsync`
- ✅ Repos legacy **no se tocan** (cambio de firma rompería WinForms)
- ✅ No se agrega helper `ref bool ok` en ViewModel (4 calls no lo justifican)

---

## Archivos modificados

### 1. `CapaDatos/Repositories/RepositorioBase.cs` — **NUEVO**

```csharp
public abstract class RepositorioBase
{
    protected static async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> operacion, string contexto = "Operación")
    {
        try { return Result<T>.Ok(await operacion()); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[{contexto}] {ex}");
            return Result<T>.Fail($"{contexto}: {ex.Message}");
        }
    }

    protected static async Task<Result> TryAsync(
        Func<Task> operacion, string contexto = "Operación")
    {
        try { await operacion(); return Result.Ok(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[{contexto}] {ex}");
            return Result.Fail($"{contexto}: {ex.Message}");
        }
    }
}
```

### 2. `CapaAplicacion4/Productos/Interfaces/IProductoRepository.cs`

Métodos de lectura actualizados a `Result<T>`:

```csharp
Task<Result<PagedResult<ProductoDto>>>   GetPagedAsync(...);
Task<Result<IReadOnlyList<ProductoDto>>> BuscarSugerenciasAsync(...);
Task<Result<IReadOnlyList<FiltroItem>>>  GetFabricantesAsync(...);
Task<Result<IReadOnlyList<FiltroItem>>>  GetPaisesAsync(...);
Task<Result<IReadOnlyList<FiltroItem>>>  GetCategoriasAsync(...);
```

### 3. `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`

- Hereda `RepositorioBase`
- **Cero `try/catch`** en el archivo — todos los métodos usan `TryAsync`
- Lógica Supabase extraída a métodos privados `...Internal()`

```csharp
public class ProductoCrudRepository : RepositorioBase, IProductoRepository
{
    public Task<Result<PagedResult<ProductoDto>>> GetPagedAsync(...) =>
        TryAsync(() => GetPagedInternal(...), "Cargar productos");

    public Task<Result<int>> CreateAsync(ProductoDto dto, ...) =>
        TryAsync(async () => { /* insert */ return id; }, "Crear producto");
    // ...
}
```

### 4. `CapaDatos/Repositories/Search/SupabaseRepository.cs`

```csharp
// antes
public abstract class SupabaseRepository<TDomain, TSupabase> : IRepository<TDomain>

// después
public abstract class SupabaseRepository<TDomain, TSupabase> : RepositorioBase, IRepository<TDomain>
```

### 5. `CapaUI/.../Productos/ProductosViewModel.cs`

- Nueva propiedad `[ObservableProperty] private string _errorCarga = ""`
- `CargarDatosAsync` — maneja `Result<T>` de fabricantes y países
- `CargarPaginaAsync` — maneja `Result<PagedResult<ProductoDto>>`
- `RefrescarSugerenciasAsync` — maneja `Result<IReadOnlyList<ProductoDto>>`; cambio de `catch (TaskCanceledException)` → `catch (OperationCanceledException)`

```csharp
var rFab = await _repo.GetFabricantesAsync();
if (!rFab.Success) { ErrorCarga = rFab.Error; IsLoading = false; return; }
Fabricantes = rFab.Value!.ToList();
```

### 6. `CapaUI/.../Productos/ProductoModal.xaml.cs`

Eliminados dos `catch { }` vacíos en `OnLoaded`. Ahora muestran `MessageBox` y deshabilitan `BtnGuardar` si falla la carga:

```csharp
// antes — error silencioso
try { var fab = await _repo.GetFabricantesAsync(); ... }
catch { }

// después — explícito
var rFab = await _repo.GetFabricantesAsync();
if (rFab.Success) { /* poblar ComboBox */ }
else { MessageBox.Show(...); BtnGuardar.IsEnabled = false; }
```

---

## Estado final del sistema de errores

```
✅ RepositorioBase        — TryAsync centralizado, una sola clase
✅ IProductoRepository    — lectura + escritura retornan Result<T>
✅ ProductoCrudRepository — cero try/catch, todo via TryAsync
✅ SupabaseRepository     — hereda RepositorioBase
✅ ProductosViewModel     — maneja Result<T> en 3 métodos, ErrorCarga observable
✅ ProductoModal          — sin catch vacíos, errores visibles al usuario
⏸ Repos legacy           — intactos hasta migración WPF (decisión documentada)
```

---

## Pendiente verificar

- [ ] Compilar desde VS2022 (`Ctrl+Shift+B`) — sin errores de compilación
- [ ] Abrir módulo Productos → lista carga correctamente
- [ ] Simular sin conexión → `ErrorCarga` se muestra en UI
- [ ] Abrir modal → ComboBoxes cargan; si falla, aparece aviso

---

*Relacionado: [[Base Repository con TryAsync]] · [[Result Pattern]] · [[Módulo Productos]] · [[Sesión 2026-05-21 - Refactor Fases 4-7]]*
