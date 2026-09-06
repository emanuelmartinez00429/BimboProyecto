# Sesión 2026-05-26 — ComboBox Fabricante Buscable

**Tipo:** UX / Filtros  
**Módulo:** [[Módulo Productos]]  
**Archivos modificados:** `ProductosView.xaml`, `ProductosView.xaml.cs`

---

## Problema

El ComboBox de **Fabricante** mostraba todos los ítems como lista estática, sin posibilidad de escribir para filtrar. El ComboBox de **País** ya tenía búsqueda inline implementada. La inconsistencia hacía que Fabricante fuera menos usable, especialmente con muchos fabricantes.

---

## Solución

Replicar exactamente el patrón ya implementado para el ComboBox de País: `ICollectionView` + filtro en memoria + `IsEditable`.

---

## Patrón usado (idéntico al de País)

```
_todosFabricantes (List<FiltroItem>)
       ↓ CollectionViewSource.GetDefaultView()
_fabricantesView (ICollectionView)
       ↓ asignado a CmbFabricante.ItemsSource
       ↓ Filter = predicado ILike en memoria al escribir
```

El filtrado es **client-side en memoria** — los fabricantes ya están cargados al inicio en `CargarDatosAsync()`, por lo que no se hace ninguna llamada HTTP adicional al escribir.

---

## Cambios en XAML (`ProductosView.xaml`)

Se agregaron cinco propiedades al `CmbFabricante`:

```xml
<!-- ANTES -->
<ComboBox x:Name="CmbFabricante"
          Width="170" Height="26"
          FontFamily="Segoe UI" FontSize="13"
          BorderBrush="#CBD5E1"
          SelectionChanged="CmbFabricante_SelectionChanged"/>

<!-- DESPUÉS -->
<ComboBox x:Name="CmbFabricante"
          Width="170" Height="26"
          FontFamily="Segoe UI" FontSize="13"
          BorderBrush="#CBD5E1"
          IsEditable="True"
          IsTextSearchEnabled="False"
          StaysOpenOnEdit="True"
          DisplayMemberPath="Nombre"
          SelectionChanged="CmbFabricante_SelectionChanged"
          PreviewKeyUp="CmbFabricante_PreviewKeyUp"/>
```

| Propiedad | Propósito |
|---|---|
| `IsEditable="True"` | Habilita campo de texto dentro del ComboBox |
| `IsTextSearchEnabled="False"` | Desactiva el autocompletado nativo por primera letra |
| `StaysOpenOnEdit="True"` | Mantiene el dropdown abierto mientras se escribe |
| `DisplayMemberPath="Nombre"` | Muestra la propiedad `Nombre` de `FiltroItem` en el campo de texto |
| `PreviewKeyUp="CmbFabricante_PreviewKeyUp"` | Conecta el handler de filtrado en tiempo real |

---

## Cambios en code-behind (`ProductosView.xaml.cs`)

### 1. Campos nuevos

```csharp
private List<FiltroItem> _todosFabricantes = new();
private System.ComponentModel.ICollectionView? _fabricantesView;
```

### 2. `PoblarFabricantes()` — ahora usa `CollectionViewSource`

```csharp
// ANTES: ítems directos → no filtrables
private void PoblarFabricantes()
{
    _suppressFilterChange = true;
    CmbFabricante.Items.Clear();
    CmbFabricante.Items.Add(new FiltroItem { Id = null, Nombre = "(Todos)" });
    foreach (var f in _vm.Fabricantes)
        CmbFabricante.Items.Add(f);
    CmbFabricante.SelectedIndex = 0;
    _suppressFilterChange = false;
}

// DESPUÉS: CollectionViewSource → permite filtrado en memoria
private void PoblarFabricantes()
{
    _suppressFilterChange = true;
    _todosFabricantes = new List<FiltroItem> { new FiltroItem { Id = null, Nombre = "(Todos)" } };
    _todosFabricantes.AddRange(_vm.Fabricantes);
    _fabricantesView = CollectionViewSource.GetDefaultView(_todosFabricantes);
    CmbFabricante.ItemsSource = _fabricantesView;
    CmbFabricante.SelectedIndex = 0;
    _suppressFilterChange = false;
}
```

### 3. `CmbFabricante_PreviewKeyUp` — nuevo handler de filtrado

```csharp
private void CmbFabricante_PreviewKeyUp(object sender, WpfKey e)
{
    if (e.Key is Key.Return or Key.Enter or Key.Up or Key.Down or Key.Escape or Key.Tab)
        return;

    if (_fabricantesView == null) return;
    var texto = CmbFabricante.Text?.Trim() ?? "";
    _fabricantesView.Filter = string.IsNullOrEmpty(texto)
        ? null
        : o => o is FiltroItem f && (f.Nombre?.Contains(texto, StringComparison.OrdinalIgnoreCase) == true);
    CmbFabricante.IsDropDownOpen = true;
}
```

Las teclas de navegación (`↑ ↓ Enter Escape Tab`) no disparan el filtrado para no interferir con la selección por teclado.

### 4. `CmbFabricante_SelectionChanged` — limpia filtro al seleccionar

```csharp
// LÍNEA AGREGADA al método existente:
if (_fabricantesView != null) _fabricantesView.Filter = null;
```

Al seleccionar un ítem, el filtro se limpia para que la próxima vez que se abra el dropdown muestre todos los fabricantes.

### 5. `FiltrosLimpiados` — incluye fabricantes

```csharp
// LÍNEA AGREGADA al handler existente:
if (_fabricantesView != null) _fabricantesView.Filter = null;
CmbFabricante.SelectedIndex = 0;
```

---

## Comportamiento resultante

| Acción | Resultado |
|---|---|
| Abrir el ComboBox sin escribir | Muestra todos los fabricantes |
| Escribir "bimbo" | Filtra en tiempo real, case-insensitive |
| Seleccionar un ítem | Aplica filtro + limpia el texto de búsqueda del view |
| Presionar Escape/Enter/Tab | No dispara filtrado, comportamiento nativo |
| Pulsar "Limpiar Filtros" | Restablece a "(Todos)" y limpia el filtro de búsqueda |

---

## Notas

- El filtrado es **completamente client-side** — cero llamadas HTTP al escribir.
- El patrón es 100% simétrico con el ComboBox de País — mismos campos, mismo handler, mismo flujo.
- `StringComparison.OrdinalIgnoreCase` cubre búsquedas con mayúsculas/minúsculas mezcladas.
- Compilación: 0 errores CS (warnings solo de copia de DLL por app abierta en VS).
