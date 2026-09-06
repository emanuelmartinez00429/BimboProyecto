---
title: "Sesión 2026-05-28 — Auditoría Deuda Técnica y Fix Memory Leak"
tags:
  - bitácora
  - deuda-técnica
  - memory-leak
  - wpf
  - fix
fecha: 2026-05-28
estado: Completado
---

# Sesión 2026-05-28 — Auditoría Deuda Técnica y Fix Memory Leak

## Resumen

Sesión de auditoría y corrección. Se verificó en código el estado real de los ítems de deuda técnica P-001 a P-008 (todos ya estaban resueltos pero el dashboard los marcaba como pendientes), se eliminó el botón "Salir" redundante del formulario de Productos, y se diagnosticó y corrigió un memory leak grave que causaba subida de memoria al abrir formularios repetidamente.

---

## 1. Auditoría de Deuda Técnica (P-001 a P-008)

El dashboard de Obsidian tenía todos los ítems marcados como `⏳ Pendiente`. Se leyó el código real y se encontró que **todos estaban ya implementados** en sesiones anteriores.

### Verificación ítem por ítem

| ID | Solución encontrada en código |
|---|---|
| P-001 | `AplicarFiltros()` centraliza filtros en `GetPagedInternal` y `BuscarSugerenciasInternal` |
| P-002 | `EstadoRegistro.Activo`/`.Inactivo` + `using static` en ViewModel y repositorio |
| P-003 | `ResolverFilteredCount()` — método estático privado, llamado desde los 3 puntos de actualización |
| P-004 | Handler `PropertyChanged` tiene `switch` con 8 casos, no las 30+ condiciones originales |
| P-005 | `SuggestionItemData` + `ListBox.SelectedIndex` OneWay binding reemplazó `VisualTreeHelper` |
| P-006 | Aceptado — diseño deliberado |
| P-007 | TODO comment en `GetConteosAsync` documenta el workaround del SDK |
| P-008 | `_pkColumns` dictionary con 12 tablas + warning log cuando tabla no está mapeada |

### Acción
Se actualizó `Dashboard.md` en Obsidian: sección renombrada de "⚠️ Deuda Técnica — Pendientes" a "✅ Deuda Técnica — Resuelta", todos los estados actualizados con detalle de cómo se resolvió cada uno.

---

## 2. Eliminación del botón "Salir"

**Motivo:** La barra lateral de `MainWindow` ya tiene navegación genérica. El botón "Salir" dentro del formulario era redundante y ocupaba espacio.

### Archivos modificados

**`ProductosView.xaml`** — Se eliminó el `<Button Command="{Binding SalirCommand}">` completo del `DockPanel` de acciones (ROW 5).

**`ProductosViewModel.cs`** — Se eliminaron:
- `public event Action? SolicitarSalir;`
- `[RelayCommand] private void Salir() => SolicitarSalir?.Invoke();`

**`ProductosView.xaml.cs`** — Se eliminó:
- `_vm.SolicitarSalir += () => SalirSolicitado?.Invoke();`

> **Nota:** `public event Action? SalirSolicitado` se **conservó** en la vista porque `FrmMenuPrincipal` de `BimboPesaje` suscribe a este evento. Eliminarlo causaría error de compilación en ese proyecto. El evento queda declarado pero nunca se dispara — comportamiento inocuo.

---

## 3. Fix Memory Leak — Diagnóstico y Corrección

### Síntoma
Al abrir el formulario de Productos varias veces, la memoria subía y nunca bajaba.

### Diagnóstico

#### Causa raíz: VM nunca se disponía

El patrón de navegación usa marcadores (`ProductosVM : ViewModelBase`) como `VistaActual`. El hook `OnVistaActualChanging` en `MainViewModel` intenta disponer el valor anterior:

```csharp
partial void OnVistaActualChanging(object? oldValue)
{
    (oldValue as IDisposable)?.Dispose();
}
```

El problema: `ProductosVM` (marcador) **no es `IDisposable`**, por lo tanto el cast falla y el `Dispose()` nunca se llama. El `ProductosViewModel` real se resuelve en `UserControl_Loaded` de la vista y **nunca se disponía**.

#### Factor amplificador: Singleton retiene Transient

`IRealtimeService` es Singleton. Su `_suscriptores` dictionary contiene:
```
"productos" → List<Action<CambioRealtime>> { OnCambioProducto (del VM) }
```

Cada `Action<CambioRealtime>` es un delegate que apunta al método de instancia del `ProductosViewModel`. Esto significa que el **Singleton tiene una referencia fuerte al Transient**. El GC no puede colectar el VM aunque nadie más lo referencie.

Resultado: cada apertura del formulario acumulaba un VM más → cada uno con su suscripción Realtime activa → procesando eventos en background → memoria estática que no baja.

#### Problema secundario: lambdas anónimas no desuscribibles

```csharp
// No se puede hacer -= con lambdas anónimas
_vm.FiltrosLimpiados += () => { ... };
_vm.PropertyChanged  += (s, ev) => { switch(...) };
```

### Corrección aplicada

**`ProductosView.xaml`** — Se agregó `Unloaded="UserControl_Unloaded"`.

**`ProductosView.xaml.cs`** — Tres cambios:

1. Lambdas anónimas extraídas a métodos nombrados:
   - `OnFiltrosLimpiados()` — contiene la lógica de reset de controles de filtro
   - `OnVmPropertyChanged(object? s, PropertyChangedEventArgs ev)` — contiene el switch de propiedades

2. Suscripciones actualizadas a métodos nombrados:
```csharp
_vm.FiltrosLimpiados += OnFiltrosLimpiados;
_vm.PropertyChanged  += OnVmPropertyChanged;
```

3. Handler `Unloaded` agregado:
```csharp
private void UserControl_Unloaded(object sender, RoutedEventArgs e)
{
    if (_vm == null) return;
    _vm.SolicitarNuevo   -= AbrirModalNuevo;
    _vm.SolicitarEditar  -= AbrirModalEditar;
    _vm.FiltrosLimpiados -= OnFiltrosLimpiados;
    _vm.PropertyChanged  -= OnVmPropertyChanged;
    _vm.Dispose();
    DataContext = null;
}
```

`_vm.Dispose()` llama a `Desuscribir("productos", OnCambioProducto)` en `IRealtimeService`, removiendo la referencia fuerte del Singleton al Transient. El GC puede colectar el VM en el siguiente ciclo.

### ¿Viola la arquitectura?

No. La View resolvió el VM de DI en `Loaded` — tomó ownership del ciclo de vida. Es coherente y simétrico que lo libere en `Unloaded`. Esto no cruza ninguna frontera de capas.

La alternativa más pura sería que el route factory retornara el `ProductosViewModel` real como DataContext, y `OnVistaActualChanging` lo dispondría automáticamente. Eso requeriría refactorizar el patrón de navegación completo — queda como deuda técnica futura si se replica el problema en otros módulos.

### Patrón a replicar en módulos futuros

Todo módulo con ViewModel que implemente `IDisposable` y sea resuelto desde DI en `Loaded` debe seguir este patrón:

```csharp
// En UserControl_Loaded:
_vm.AlgunEvento += AlgunMetodoNombrado; // NO lambdas anónimas

// En UserControl_Unloaded:
_vm.AlgunEvento -= AlgunMetodoNombrado;
_vm.Dispose();
DataContext = null;
```

---

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `ProyectoBimboContexto/00 - MOC/Dashboard.md` | Deuda técnica P-001–P-008 marcada como resuelta |
| `CapaUI/.../ProductosView.xaml` | Botón Salir eliminado; atributo `Unloaded` agregado |
| `CapaUI/.../ProductosView.xaml.cs` | `Unloaded` handler; lambdas → métodos nombrados; suscripción `SolicitarSalir` eliminada |
| `CapaUI/.../ProductosViewModel.cs` | `SolicitarSalir` event y `SalirCommand` eliminados |
