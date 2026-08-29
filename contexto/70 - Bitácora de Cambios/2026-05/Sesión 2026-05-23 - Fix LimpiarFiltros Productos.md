---
title: Fix — LimpiarFiltros no reseteaba ComboBoxes
type: sesion
status: vigente
tags:
  - bugfix
  - productos
  - mvvm
  - eventos
date: 2026-05-23
updated: 2026-05-23
summary: "El botón \"Limpiar Filtros\" del módulo Productos actualizaba correctamente la tabla (recargaba datos sin filtros), pero los ComboBoxes de Fabricante y País, y el…"
scope: []
symbols:
  - CmbFabricante
  - CmbPais
  - FiltrosLimpiados
  - GetFabricantesAsync
  - ItemsSource
  - OnPropertyChanged
  - RbHabilitados
  - SelectedIndex
  - SelectionChanged
---

# Fix — LimpiarFiltros no reseteaba los ComboBoxes

## El problema

El botón "Limpiar Filtros" del módulo Productos actualizaba correctamente la tabla (recargaba datos sin filtros), pero los ComboBoxes de Fabricante y País, y el RadioButton de Estado, quedaban visualmente con el valor anterior.

---

## Causa raíz

Existía un **mismatch arquitectónico de dos vías** entre los controles de filtro y el ViewModel.

| Dirección | Mecanismo | ¿Funcionaba? |
|---|---|---|
| **View → VM** | `SelectionChanged` → `_vm.FabricanteIdFiltro = ...` | ✅ |
| **VM → View** | `OnPropertyChanged(nameof(FabricanteIdFiltro))` | ❌ Nadie escuchaba |

Los ComboBoxes se poblan manualmente con `Items.Add()` (no via `ItemsSource` binding). La única función que reseteaba su `SelectedIndex` era `PoblarFabricantes()` / `PoblarPaises()`, pero estas solo se disparaban cuando cambiaba **la lista completa** (carga inicial), no cuando cambiaba **el valor seleccionado**.

Al ejecutar `LimpiarFiltros()`, el ViewModel reseteaba sus campos internos y emitía `OnPropertyChanged`, pero ningún elemento de la View respondía a esas notificaciones para los ComboBoxes ni para el RadioButton.

---

## Opciones evaluadas

### Opción A — Evento `FiltrosLimpiados` ✅ Elegida

Agregar un evento en el ViewModel que la View suscribe para resetear los controles imperativamente.

```csharp
// ViewModel
public event Action? FiltrosLimpiados;

private void LimpiarFiltros()
{
    _estadoFiltro       = EstadoFilter.Todos;
    _fabricanteIdFiltro = null;
    _paisIdFiltro       = null;
    _page = 1;
    FiltrosLimpiados?.Invoke();
    _ = CargarPaginaAsync();
}
```

```csharp
// View — UserControl_Loaded
_vm.FiltrosLimpiados += () =>
{
    _suppressFilterChange = true;
    RbTodos.IsChecked           = true;
    CmbFabricante.SelectedIndex = 0;
    CmbPais.SelectedIndex       = 0;
    _suppressFilterChange = false;
};
```

### Opción B — Binding completo MVVM

Convertir los ComboBoxes a `ItemsSource="{Binding Fabricantes}"` + `SelectedValue="{Binding FabricanteIdFiltro, Mode=TwoWay}"` + `SelectedValuePath="Id"`. El ViewModel inyecta `FiltroItem { Id = null, Nombre = "(Todos)" }` como primer ítem de la lista. Al hacer `_fabricanteIdFiltro = null`, WPF selecciona automáticamente el ítem "(Todos)".

---

## Por qué se eligió la Opción A

1. **Consistencia con el patrón ya establecido.** El ViewModel ya usa eventos para side effects de UI que no puede/debe hacer directamente:
   ```csharp
   public event Action?              SolicitarNuevo;
   public event Action<ProductoDto>? SolicitarEditar;
   public event Action?              SolicitarSalir;
   ```
   `FiltrosLimpiados` sigue exactamente este mismo patrón — es la forma idiomática del proyecto.

2. **Riesgo mínimo.** Solo 2 archivos modificados, cambio quirúrgico, cero impacto en otras pantallas.

3. **La Opción B requería cambios en cascada:** modificar el XAML (ItemsSource, SelectedValue, SelectedValuePath), cambiar `GetFabricantesAsync` para incluir el ítem "(Todos)" o agregarlo en el ViewModel, y crear un converter para los RadioButtons. Todo eso como refactor mayor en otro sprint.

4. **La Opción B es válida como refactor futuro**, cuando se revise el módulo Productos de forma completa para full-MVVM binding.

---

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `CapaUI/.../Productos/ProductosViewModel.cs` | Agregado `event Action? FiltrosLimpiados`, invocado en `LimpiarFiltros()`. Eliminados `OnPropertyChanged` redundantes de campos privados que nadie escuchaba. |
| `CapaUI/.../Productos/ProductosView.xaml.cs` | Suscripción a `_vm.FiltrosLimpiados` en `UserControl_Loaded`. Resetea `RbHabilitados`, `CmbFabricante` y `CmbPais` con `_suppressFilterChange = true` para evitar disparar filtros en cadena. |

---

## Ajuste posterior — Estado por defecto: Habilitados

Tras aplicar el fix se detectó que el filtro de estado debía arrancar en **Habilitados** (no en Todos), tanto al cargar la vista como al limpiar filtros. La razón de negocio es que el caso de uso normal es gestionar productos activos; ver todos (incluidos deshabilitados) es la excepción.

**Cambios:**

```csharp
// ProductosViewModel.cs — campo inicial
private EstadoFilter _estadoFiltro = EstadoFilter.Habilitados; // era Todos

// LimpiarFiltros() — reset
_estadoFiltro = EstadoFilter.Habilitados; // era Todos
```

```csharp
// ProductosView.xaml.cs — FiltrosLimpiados handler
RbHabilitados.IsChecked = true; // era RbTodos
```

---

## Relaciones

- [[Observer Pattern]] — El evento `FiltrosLimpiados` es una aplicación del patrón Observer entre VM y View
- [[Caso 01 - CRUD con Paginación]] — Módulo donde se aplicó el fix
