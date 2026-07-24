---
tags: [proyecto, refactor, plan, productos]
---

# Plan de Refactor — Estado y Fases

> Módulo Productos · Última revisión: 2026-05-21 · **COMPLETADO ✅**

---

## Estado general

| Fase | Descripción | Estado |
|---|---|---|
| 1 | Server-side filtering | ✅ Completada |
| 2 | Contratos en CapaAplicacion (DTOs, interfaces) | ✅ Completada |
| 3 | Implementación en CapaDatos + ViewModel | ✅ Completada |
| — | Verificar ruta activa (BimboPesaje vs CapaUI) | ✅ Resuelta |
| 4 | IProductoRepository: métodos de escritura | ✅ Completada |
| 5 | ProductoModal con DI | ✅ Completada |
| 6 | Bindings XAML al DTO | ✅ Completada |
| 7 | Result Pattern | ✅ Completada |

> Ver detalle completo: [[Sesión 2026-05-21 - Refactor Fases 4-7]]

---

## ✅ Fase 1 — Server-side filtering

- `IRepository<T>.FindAsync(Expression)` → `SearchAsync(string term)`
- Eliminadas las 4 clases Specification (no usadas)
- `SupabaseRepository` abstracto con `SearchAsync` override obligatorio
- `EmpleadoRepository`, `ClienteRepository` → filtros ILike server-side
- Las 3 estrategias del buscador universal llaman `_repo.SearchAsync(term, ct)` directamente

---

## ✅ Fase 2 — Contratos en CapaAplicacion

Nuevos archivos creados en `CapaAplicacion4/Productos/`:
```
Dtos/ProductoDto.cs        — DTO completo con FKs e init setters
Dtos/FiltroItem.cs         — { int? Id, string Nombre } para ComboBox
Queries/PagedResult.cs     — { Items, Total, Activos, Inactivos }
Queries/ProductoFiltros.cs — { IdEstado?, IdFabricante?, IdPais? }
Interfaces/IProductoRepository.cs — contrato de lectura
```

---

## ✅ Fase 3 — Implementación en CapaDatos

- `ProductoCrudRepository` implementa `IProductoRepository` (lectura)
- `ProductoSearchRepository` implementa `IRepository<Producto>`
- `CapaDatos.csproj` referencia a `CapaAplicacion4`
- `DependencyInjection.cs` registra ambos repositorios
- `ProductosViewModel` reescrito con `ObservableObject` + `[ObservableProperty]` + `[RelayCommand]`

---

## ✅ Ruta activa — Redirección a CapaUI

`FrmMenuPrincipal.cs` redirigido de `BimboPesaje/Formularios/Productos/` → `CapaUI/Formularios/Principal/Pantallas/Productos/`

`BimboPesaje.csproj` con referencia a `CapaUI.csproj` agregada.

---

## ✅ Fase 4 — IProductoRepository: métodos de escritura

`IProductoRepository` extendido con:
```csharp
Task<IReadOnlyList<FiltroItem>>  GetCategoriasAsync(ct);
Task<Result<int>> CreateAsync(ProductoDto dto, ct);
Task<Result>      UpdateAsync(ProductoDto dto, ct);
Task<Result>      DeleteAsync(int id, ct);
```

`ProductoCrudRepository` implementa los 3 métodos con try/catch → `Result`.

Soft delete: `DeleteAsync` setea `id_estado = 2`, no elimina el registro.

---

## ✅ Fase 5 — ProductoModal con DI

Constructor del modal recibe `IProductoRepository`:
```csharp
public ProductoModal(IProductoRepository repo, ProductoDto? producto)
```

`OnLoaded` usa `_repo.GetFabricantesAsync()` y `_repo.GetCategoriasAsync()` para poblar ComboBoxes.

`ProductosView` resuelve el repo desde `App.Services.GetRequiredService<IProductoRepository>()` y lo pasa al constructor.

---

## ✅ Fase 6 — Bindings XAML al DTO

8 bindings del DataGrid actualizados de propiedades snake_case del modelo de BD a propiedades PascalCase del `ProductoDto`:
`CodigoInterno`, `Nombre`, `Fabricante`, `Pais`, `Contenido`, `Presentacion`, `Categoria`, `IdEstado`.

---

## ✅ Fase 7 — Result Pattern

Creado `CapaAplicacion4/Common/Result.cs` con `Result<T>` y `Result` sealed.

`ProductoModal.BtnGuardar_Click` ahora:
- Verifica `r.Success` antes de invocar `Guardado`
- Muestra `r.Error` en MessageBox si falla
- No usa excepciones como flujo de control

---

## Archivos modificados (resumen)

| Archivo | Cambio |
|---|---|
| `BimboPesaje/Formularios/MenuPrincipal/FrmMenuPrincipal.cs` | Redirigir navegación a CapaUI |
| `BimboPesaje/BimboPesaje.csproj` | Agregar referencia a CapaUI |
| `CapaAplicacion4/Common/Result.cs` | Nuevo — Result Pattern |
| `CapaAplicacion4/Productos/Interfaces/IProductoRepository.cs` | Agregar write methods + GetCategorias |
| `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs` | Implementar Create/Update/Delete/GetCategorias |
| `CapaDatos/Repositories/Search/EmpleadoRepository.cs` | Fix `using static` |
| `CapaDatos/Repositories/Search/ProductoSearchRepository.cs` | Fix namespace conflict con type alias |
| `CapaUI/.../ProductosView.xaml` | 8 bindings a ProductoDto |
| `CapaUI/.../ProductosView.xaml.cs` | Abrir modal via DI |
| `CapaUI/.../ProductoModal.xaml.cs` | DI constructor + Result Pattern en guardar |

---

## Próximo paso

Compilar desde VS2022 (`Ctrl+Shift+B`) y testear el flujo completo:
1. Navegar a Productos → lista carga correctamente
2. Crear nuevo producto → aparece en la lista
3. Editar producto → cambios persistidos en Supabase
4. Intentar guardar con error → MessageBox muestra el mensaje del Result

---

*Relacionado: [[Módulo Productos]] · [[Repository Pattern]] · [[Result Pattern]] · [[Arquitectura Actual]] · [[Sesión 2026-05-21 - Refactor Fases 4-7]]*
