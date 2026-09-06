---
title: "Sesión 2026-05-28 — Eliminación Memory Leaks Ciclo Completo"
tags:
  - bitácora
  - memory-leak
  - wpf
  - dispose
  - lifecycle
fecha: 2026-05-28
estado: Completado
---

# Sesión 2026-05-28 — Eliminación Memory Leaks Ciclo Completo

## Resumen

Auditoría profunda de memory leaks en toda la aplicación. Se identificaron 7 fuentes de leak adicionales al fix anterior de `ProductosView`, distribuidas en 3 ciclos de vida: login/logout, navegación de ViewModels, y timers internos. Se corrigieron todas con 0 errores de compilación.

---

## Contexto

En la sesión anterior se corrigió el leak principal: `ProductosViewModel` nunca se disponía al navegar entre formularios (fix en `ProductosView.Unloaded`). El usuario reportó que la memoria seguía subiendo en ciertos escenarios. Se realizó una auditoría completa de todos los archivos de CapaUI para encontrar todas las fuentes restantes.

---

## Diagnóstico completo

Se auditaron todos los code-behind, ViewModels, ventanas y paneles de CapaUI. Se clasificaron 7 problemas en 3 grupos:

### Grupo A: Ciclo Login/Logout

Cada vez que el usuario cierra sesión y vuelve a entrar, las ventanas anteriores no se liberaban.

| ID | Archivo | Problema |
|---|---|---|
| A1 | `App.xaml.cs` | Lambdas anónimas en `LoginExitoso` y `SesionCerrada` capturaban las ventanas por closure, impidiendo GC |
| A2 | `MainWindow.xaml.cs` | `HwndSource.AddHook(WndProc)` nunca llamaba `RemoveHook` — el `HwndSource` retenía toda la ventana |
| A3 | `MainWindow.xaml.cs` | `Vm.CierreRequerido += (_, _) => Close()` — lambda anónima con referencia circular VM↔Window |
| A4 | `LoginWindow.xaml.cs` | Spinner con `RepeatBehavior.Forever` no se detenía en login exitoso, solo en error |

### Grupo B: ViewModels sin cleanup

| ID | Archivo | Problema |
|---|---|---|
| B1 | `MainViewModel.cs` | `_searchVm.ResultSelected += OnResultadoBusquedaSeleccionado` nunca desuscrito. VM no era `IDisposable` |
| B2 | `UniversalSearchViewModel.cs` | `CancellationTokenSource _cts` nunca dispuesto al abandonar el VM |

### Grupo C: Timers

| ID | Archivo | Problema |
|---|---|---|
| C1 | `ForgotCodePanel.xaml.cs` | `DispatcherTimer` solo se detenía en `BtnBack_Click` o verificación exitosa. Cerrar la ventana directamente dejaba el timer corriendo |

### Descartados (sin leak)

| Archivo | Razón |
|---|---|
| `ProductoModal.xaml.cs` | `ModalContent.Content = null` en `CerrarModal()` rompe la referencia correctamente |
| `RealtimeService.cs` | `CerrarCanal` llama `Unsubscribe` y limpia `_canales`. Correcto |
| DI `AddScoped` sin scope | Repos stateless, actúan como singletons. Sin leak real, solo semánticamente incorrecto |

---

## Correcciones aplicadas

### Fase 1: Ciclo Login/Logout (A1–A4)

**`App.xaml.cs`** — Lambdas reemplazadas por métodos nombrados con desuscripción explícita:

```csharp
private static LoginWindow? _loginActual;
private static MainWindow?  _mainActual;

// MostrarLogin: suscribe con +=, OnLoginExitoso desuscribe con -=, nullea ref, llama MostrarPrincipal
// MostrarPrincipal: suscribe con +=, OnSesionCerrada desuscribe con -=, nullea ref, llama MostrarLogin
```

La ventana cerrada ya no es retenida por ninguna lambda ni campo estático.

**`MainWindow.xaml.cs`** — 3 fixes:

1. **HwndSource (A2):** Campo `_hwndSource` guardado en `OnSourceInitialized`. `RemoveHook` llamado en `HandleCierreAsync` finally.

2. **CierreRequerido (A3):** Lambda `(_, _) => Close()` reemplazada por método `OnCierreRequerido`. Desuscrito con `-=` en el finally de cierre.

3. **Dispose del VM:** `Vm.Dispose()` invocado en el finally de `HandleCierreAsync`, encadenando la limpieza de `MainViewModel` → `UniversalSearchViewModel`.

**`LoginWindow.xaml.cs` (A4):** `SpinnerRotate.BeginAnimation(null)` agregado justo antes de `LoginExitoso?.Invoke()` para detener la animación infinita en el path exitoso.

### Fase 2: ViewModels (B1–B2)

**`MainViewModel.cs`** — Implementa `IDisposable`:

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    (VistaActual as IDisposable)?.Dispose();           // disponer vista actual
    _searchVm.ResultSelected -= OnResultadoBusquedaSeleccionado;  // desuscribir
    (_searchVm as IDisposable)?.Dispose();              // encadenar dispose
}
```

Esto también beneficia a `OnVistaActualChanging` que ya disponía vistas al navegar — ahora `UniversalSearchViewModel` es `IDisposable` y puede ser limpiado cuando deja de ser `VistaActual`.

**Fix adicional:** Se corrigió el parámetro de `OnVistaActualChanging` de `oldValue` a `value` para coincidir con la firma generada por CommunityToolkit source generator (eliminó warning CS8826).

**`UniversalSearchViewModel.cs`** — Implementa `IDisposable`:

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    _cts?.Cancel();
    _cts?.Dispose();
}
```

### Fase 3: Timer (C1)

**`ForgotCodePanel.xaml.cs`** — Agregado en constructor:

```csharp
Unloaded += (_, _) => _timer?.Stop();
```

Cubre el caso donde el usuario cierra la ventana de login sin haber pulsado "Atrás" ni completado la verificación.

---

## Archivos modificados

| Archivo | Cambios |
|---|---|
| `CapaUI/App.xaml.cs` | Campos `_loginActual`/`_mainActual`, métodos `OnLoginExitoso`/`OnSesionCerrada` |
| `CapaUI/.../MainWindow.xaml.cs` | Campo `_hwndSource`, método `OnCierreRequerido`, `RemoveHook` + `Vm.Dispose()` en finally |
| `CapaUI/.../LoginWindow.xaml.cs` | `BeginAnimation(null)` antes de `LoginExitoso` |
| `CapaUI/.../MainViewModel.cs` | `IDisposable`, `Dispose()`, fix parámetro `OnVistaActualChanging` |
| `CapaUI/ViewModels/Search/UniversalSearchViewModel.cs` | `IDisposable`, `Dispose()` |
| `CapaUI/.../InicioSesion/ForgotCodePanel.xaml.cs` | `Unloaded` handler para detener timer |

## Compilación

| Proyecto | Errores |
|---|---|
| CapaAplicacion | 0 ✅ |
| CapaDatos | 0 ✅ |
| CapaUI | 0 ✅ |
| BimboPesaje | No tocado ✅ |

---

## Patrón establecido para módulos futuros

Todo componente WPF que adquiera recursos debe liberarlos simétricamente:

| Adquisición | Liberación |
|---|---|
| `Loaded` → resolver VM de DI, suscribir eventos | `Unloaded` → desuscribir eventos, `_vm.Dispose()`, `DataContext = null` |
| Constructor Window → suscribir a VM events | `OnClosing`/cierre → desuscribir, `Vm.Dispose()` |
| `HwndSource.AddHook` | `HwndSource.RemoveHook` |
| `BeginAnimation(RepeatBehavior.Forever)` | `BeginAnimation(null)` antes de cerrar |
| `DispatcherTimer.Start()` | `Unloaded` o cierre → `_timer.Stop()` |
| Lambda anónima en `+=` | **Nunca** — usar método nombrado para poder hacer `-=` |
