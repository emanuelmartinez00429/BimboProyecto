---
title: Sesión 2026-05-29 — SuggestionSearchBox Compartido y Fix 5 Bugs Buscador
type: sesion
status: vigente
tags:
  - sesion
date: 2026-05-29
updated: 2026-05-29
summary: "El buscador con sugerencias estaba duplicado en los 4 formularios (Productos, Proveedores, Fabricantes, Categorías): cada uno tenía su propia clase…"
scope:
  - CapaUI/Core/Controls
symbols:
  - ActualizarSuggestions
  - CategoriaDto
  - CategoriasView
  - DockPanel
  - FabricanteDto
  - FabricantesView
  - HighlightIndex
  - IReadOnlyList<T>
  - IndexOf
  - ItemSelected
---

# Sesión 2026-05-29 — SuggestionSearchBox Compartido y Fix 5 Bugs Buscador

**Fecha:** 2026-05-29  
**Estado:** Completado

---

## Contexto

El buscador con sugerencias estaba duplicado en los 4 formularios (Productos, Proveedores, Fabricantes, Categorías): cada uno tenía su propia clase `SuggestionItemData` local, su propio bloque XAML con `TextBox + Popup`, y sus propios handlers de teclado/ratón. Además había 5 bugs de UX en la navegación del popup.

---

## Bugs corregidos

| # | Bug | Causa raíz | Fix |
|---|---|---|---|
| 1 | Últimos items cortados cuando hay 10 sugerencias | `StackPanel` da altura infinita al `ListBox`, el `ScrollViewer` interno nunca se activa | Reemplazado por `DockPanel`; el `ListBox` ocupa el espacio restante y hace scroll correctamente |
| 2 | Flecha ↓ posiciona en el 2º item, no en el 1º | VM inicializaba `HighlightIndex = 0` al recibir sugerencias | `HighlightIndex = -1` siempre al llegar nuevas sugerencias |
| 3 | Con un solo item, a veces no se colorea como seleccionado | El DP `HighlightIndex` no dispara el callback si el valor no cambia (ej. ya era -1 y vuelve a -1) | Reset explícito `SuggestionsList.SelectedIndex = -1` en `OnSuggestItemsChanged` antes de reasignar |
| 4 | Las flechas a veces no responden | Al llegar al extremo sin wrap-around, el valor no cambia y el callback no se dispara | Navegación circular: los extremos vuelven al otro lado |
| 5 | Sin wrap-around: flecha ↓ en el último item se queda atascada | `Math.Min(idx + 1, count - 1)` no avanza más | `(idx + 1) % count` para ↓, `idx <= 0 ? count - 1 : idx - 1` para ↑ |

---

## Cambios realizados

### C1 — `SuggestionItemData` compartida (nueva)

**Archivo:** `CapaUI/Core/Controls/SuggestionItemData.cs`

Clase única con `Source` tipado como `object` para ser reutilizable por cualquier módulo:

```csharp
public class SuggestionItemData
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Meta   { get; set; } = "";
    public bool   Activo { get; set; }
    public object Source { get; set; } = null!;
}
```

Cada vista hace cast de `e.Source` a su DTO concreto en `SearchBox_ItemSelected`.

---

### C2 — `SuggestionSearchBox` UserControl compartido (nuevo)

**Archivos:** `CapaUI/Core/Controls/SuggestionSearchBox.xaml` + `.xaml.cs`

UserControl centraliza toda la lógica del popup:

- **XAML:** `DockPanel` con header fijo (`DockPanel.Dock="Top"`) y `ListBox` que ocupa el espacio restante. `ScrollViewer.VerticalScrollBarVisibility="Auto"` en el `ListBox` para scroll interno real.
- **DPs expuestos:** `Query` (TwoWay), `Placeholder`, `SuggestItems` (dispara apertura/cierre del popup), `HighlightIndex` (TwoWay)
- **Evento:** `ItemSelected` con argumento `SuggestionItemData`
- **Teclado:** ↓↑ con wrap-around, Enter selecciona, Escape limpia, Tab cierra
- **Ratón:** hover actualiza `HighlightIndex` con loop `ReferenceEquals` (no `IndexOf`, `IReadOnlyList<T>` no lo tiene)
- **Fix bug #3:** `OnSuggestItemsChanged` resetea `SuggestionsList.SelectedIndex = -1` manualmente antes de reasignar `ItemsSource`
- **Guard `_updatingText`:** evita loop infinito entre `TextBox.TextChanged` y el callback del DP `Query`

---

### C3 — `ProductosView` migrada al UserControl

**Archivos:** `ProductosView.xaml`, `ProductosView.xaml.cs`

- XAML: bloque TextBox+Popup (~212 líneas) reemplazado por `<controls:SuggestionSearchBox x:Name="SearchBox" .../>`
- Code-behind: eliminados 7 handlers locales; nuevo `ActualizarSuggestions` + `SearchBox_ItemSelected`
- `Codigo = p.CodigoInterno`, `Meta = "{Fabricante} · {Pais} · {Categoria}"`

---

### C4 — `ProveedoresView` migrada al UserControl

**Archivos:** `ProveedoresView.xaml`, `ProveedoresView.xaml.cs`

- `Meta = p.Rtn`
- `SearchBox_ItemSelected` castea `e.Source` a `ProveedorDto`

---

### C5 — `FabricantesView` migrada al UserControl

**Archivos:** `FabricantesView.xaml`, `FabricantesView.xaml.cs`

- `Meta = "{NombreProveedor} · {NombrePais}"`
- `SearchBox_ItemSelected` castea `e.Source` a `FabricanteDto`

---

### C6 — `CategoriasView` migrada al UserControl

**Archivos:** `CategoriasView.xaml`, `CategoriasView.xaml.cs`

- Eliminada clase `SuggestionItemData` local (era la única vista que la definía dentro del namespace en vez de como clase separada)
- `Activo = c.EstadoCategoria` (bool directo, no `c.IdEstado == Activo`)
- `SearchBox_ItemSelected` castea `e.Source` a `CategoriaDto`

---

### C7 — Fix `HighlightIndex` en ViewModels (bug #2)

**Archivos:** `ProductosViewModel.cs`, `ProveedoresViewModel.cs`, `FabricantesViewModel.cs`, `CategoriasViewModel.cs`

```csharp
// Antes:
HighlightIndex = r.Value!.Count > 0 ? 0 : -1;

// Después:
HighlightIndex = -1;
```

El UserControl gestiona el highlight; el VM no debe pre-seleccionar nada.

---

### C8 — Eliminación evento muerto `SalirSolicitado` (CS0067)

**Archivo:** `ProductosView.xaml.cs`

`public event Action? SalirSolicitado;` nunca tenía suscriptores ni se invocaba. Eliminado para quitar el warning CS0067.

---

## Patrón resultante para nuevos módulos

Para agregar un nuevo formulario con buscador:

1. En el XAML: `<controls:SuggestionSearchBox x:Name="SearchBox" Placeholder="..." ItemSelected="SearchBox_ItemSelected"/>`
2. En el VM: exponer `Query`, `HighlightIndex`, `ShowSuggestions`, `Suggestions`, `SeleccionarSugerencia()`; inicializar `HighlightIndex = -1`
3. En el code-behind:
   ```csharp
   private void ActualizarSuggestions()
   {
       SearchBox.SuggestItems = (_vm.ShowSuggestions && _vm.Suggestions.Count > 0)
           ? _vm.Suggestions.Select(x => new SuggestionItemData { Nombre = x.Nombre, Meta = ..., Activo = ..., Source = x }).ToList()
           : null;
   }
   private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
   {
       _vm.SeleccionarSugerencia((MiDto)e.Source);
       SeleccionarEnTabla();
   }
   ```

---

## Archivos modificados

| Archivo | Tipo de cambio |
|---|---|
| `CapaUI/Core/Controls/SuggestionItemData.cs` | Nuevo |
| `CapaUI/Core/Controls/SuggestionSearchBox.xaml` | Nuevo |
| `CapaUI/Core/Controls/SuggestionSearchBox.xaml.cs` | Nuevo |
| `CapaUI/.../Productos/ProductosView.xaml` | Modificado |
| `CapaUI/.../Productos/ProductosView.xaml.cs` | Modificado |
| `CapaUI/.../Productos/ProductosViewModel.cs` | Modificado |
| `CapaUI/.../Proveedores/ProveedoresView.xaml` | Modificado |
| `CapaUI/.../Proveedores/ProveedoresView.xaml.cs` | Modificado |
| `CapaUI/.../Proveedores/ProveedoresViewModel.cs` | Modificado |
| `CapaUI/.../Fabricantes/FabricantesView.xaml` | Modificado |
| `CapaUI/.../Fabricantes/FabricantesView.xaml.cs` | Modificado |
| `CapaUI/.../Fabricantes/FabricantesViewModel.cs` | Modificado |
| `CapaUI/.../Categorias/CategoriasView.xaml` | Modificado |
| `CapaUI/.../Categorias/CategoriasView.xaml.cs` | Modificado |
| `CapaUI/.../Categorias/CategoriasViewModel.cs` | Modificado |
