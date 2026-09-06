---
title: "Sesión 2026-05-28 — Refactor P-004 (switch) y P-005 (ListBox)"
tags:
  - sesion
  - refactor
  - deuda-tecnica
  - wpf
  - productos
  - code-behind
date: 2026-05-28
---

# Sesión 2026-05-28 — Refactor P-004 y P-005

> [!success] Resultado
> `CapaUI` compila con **0 errores**.
> El `PropertyChanged` handler usa `switch`. `VisualTreeHelper` eliminado del código de highlight.

---

## P-004 — `PropertyChanged` handler: 9 `if` → `switch`

**Archivo:** `CapaUI/.../Pantallas/Productos/ProductosView.xaml.cs`

### Antes
```csharp
_vm.PropertyChanged += (s, ev) =>
{
    if (ev.PropertyName == nameof(ProductosViewModel.PageRows))    RefrescarPaginacion();
    if (ev.PropertyName == nameof(ProductosViewModel.IsLoading))   { /* 8 líneas */ }
    if (ev.PropertyName == nameof(ProductosViewModel.NoResults))   EmptyState.Visibility = ...
    // ... 6 if más
};
```

Todos eran `if` (no `if/else if`) — en cada invocación el runtime evaluaba las 9 condiciones completas sin cortocircuito.

### Después
```csharp
_vm.PropertyChanged += (s, ev) =>
{
    switch (ev.PropertyName)
    {
        case nameof(ProductosViewModel.PageRows):        RefrescarPaginacion();  break;
        case nameof(ProductosViewModel.IsLoading):       ActualizarCarga();      break;
        case nameof(ProductosViewModel.NoResults):
            EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
            break;
        case nameof(ProductosViewModel.HaySeleccionado):
            SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
            break;
        case nameof(ProductosViewModel.Seleccionado):    SeleccionarEnTabla();   break;
        case nameof(ProductosViewModel.Fabricantes):     PoblarFabricantes();    break;
        case nameof(ProductosViewModel.Paises):          PoblarPaises();         break;
        case nameof(ProductosViewModel.ShowSuggestions): ActualizarSuggestions();break;
        // HighlightIndex: resuelto por ListBox.SelectedIndex OneWay binding
    }
};
```

El bloque `IsLoading` fue extraído al método privado `ActualizarCarga()` para mantener el switch legible.

El `case` de `HighlightIndex` desapareció por completo — P-005 lo vuelve innecesario.

---

## P-005 — `VisualTreeHelper` eliminado: `ItemsControl` → `ListBox`

### Problema raíz
`ActualizarHighlight()` usaba `VisualTreeHelper.GetChild(container, 0)` asumiendo que el primer hijo de cada `ContentPresenter` en el `ItemsControl` era un `Border`. Si el XAML del template cambiaba, el cast fallaba silenciosamente.

### Solución
Reemplazar el `ItemsControl` por un `ListBox` con `SelectedIndex="{Binding HighlightIndex, Mode=OneWay}"` y un `ItemContainerStyle` que aplica el highlight mediante triggers de WPF.

**XAML — `ItemContainerStyle` del `ListBox`:**
```xml
<ListBox x:Name="SuggestionsList"
         SelectedIndex="{Binding HighlightIndex, Mode=OneWay}"
         Background="Transparent" BorderThickness="0"
         FocusVisualStyle="{x:Null}"
         ScrollViewer.HorizontalScrollBarVisibility="Disabled">
    <ListBox.ItemContainerStyle>
        <Style TargetType="ListBoxItem">
            <Setter Property="Padding" Value="0"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
            <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ListBoxItem">
                        <Border x:Name="ItemBd" Background="Transparent">
                            <ContentPresenter/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="ItemBd" Property="Background" Value="#EEF2F8"/>
                            </Trigger>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="ItemBd" Property="Background" Value="#D1DCF5"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </ListBox.ItemContainerStyle>
    ...
</ListBox>
```

El trigger `IsSelected` viene después de `IsMouseOver` — WPF aplica el último trigger que coincida, por lo que el color de selección tiene prioridad sobre el de hover.

### Por qué `Mode=OneWay`
El binding es `OneWay` (VM → ListBox, no bidireccional). Si fuera `TwoWay`, cualquier cambio en `SelectedIndex` (incluyendo cuando cambia el `ItemsSource`) escribiría de vuelta a `HighlightIndex` en el VM, disparando efectos secundarios no deseados.

El flujo de highlight queda así:
- **Teclado:** code-behind sets `_vm.HighlightIndex` → binding actualiza `ListBox.SelectedIndex` → trigger `IsSelected` pinta el ítem
- **Mouse hover:** `IsMouseOver` trigger pinta visualmente + `SuggestionItem_MouseEnter` actualiza `_vm.HighlightIndex` para que Enter funcione
- **Click:** `SuggestionItem_Click` en el Border (sin cambios) → llama `SeleccionarSugerencia`

### Código eliminado
```csharp
// Eliminado completamente:
private static readonly SolidColorBrush _highlightBrush = new(Color.FromRgb(0xD1, 0xDC, 0xF5));
private static readonly SolidColorBrush _transparentBrush = Brushes.Transparent;

private void ActualizarHighlight()
{
    SuggestionsList.UpdateLayout();
    for (int i = 0; i < SuggestionsList.Items.Count; i++)
    {
        var container = SuggestionsList.ItemContainerGenerator
            .ContainerFromIndex(i) as ContentPresenter;
        if (container == null) continue;
        var border = VisualTreeHelper.GetChildrenCount(container) > 0
            ? VisualTreeHelper.GetChild(container, 0) as Border : null;
        if (border == null) continue;
        border.Background = (i == _vm.HighlightIndex) ? _highlightBrush : _transparentBrush;
    }
}
```

---

## Magic number extra corregido

`ActualizarSuggestions` tenía `p.IdEstado == 1` — reemplazado por `p.IdEstado == Activo` (usando `using static EstadoRegistro`), consistente con P-002.

---

## Resultado de build

| Proyecto | Errores | Estado |
|---|---|---|
| `CapaAplicacion` | 0 | ✅ |
| `CapaDatos` | 0 | ✅ |
| `CapaUI` | 0 | ✅ |
| `BimboPesaje` | 3 (preexistentes — `ServicioPerfilUsuario`) | ⚠️ |

---

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-004, P-005 marcados como resueltos
- [[Sesión 2026-05-28 - Refactor Críticos P001-P003]] — sesión anterior (misma jornada)
- [[Módulo Productos]] — módulo refactorizado
