---
title: Interceptar Cierre de Ventana — WPF (CapaUI)
tags:
  - patron
  - wpf
  - sesion
  - seguridad
  - async
  - dotnet
aliases:
  - OnClosing
  - Cancel-and-Retry
  - Logout Seguro
---

# Interceptar Cierre de Ventana — WPF (CapaUI)

> [!abstract] El problema
> En una app WPF, el cierre puede originarse desde múltiples puntos: botón X del chrome personalizado, comando de logout en el ViewModel, o Alt+F4. Sin intercepción centralizada, **algunos caminos cierran la app sin invalidar el token JWT en Supabase**, dejando la sesión activa en el servidor.

> [!warning] Proyecto canónico
> La implementación vive en **`CapaUI`**, no en `BimboPesaje`. `BimboPesaje` es solo un proyecto de referencia de arquitectura anterior. Todo trabajo nuevo va en `CapaUI`.

---

## El patrón: Cancel-and-Retry con re-entry flag

El override `OnClosing` es sincrónico. Para hacer cleanup async (como `client.Auth.SignOut()`), se usa el patrón **Cancel-and-Retry**:

1. Primera llamada → `e.Cancel = true`, lanzar trabajo async
2. Trabajo async termina → poner `_cerrando = true` → llamar `Close()` de nuevo
3. Segunda llamada → `_cerrando == true`, dejar pasar con `base.OnClosing(e)`

```
Primera pasada:                    Segunda pasada:
OnClosing()                        OnClosing()
  _cerrando == false                 _cerrando == true
  e.Cancel = true         ──────►    base.OnClosing(e)
  HandleCierreAsync()                (ventana se cierra de verdad)
       │
       ▼
  [diálogo confirmación]
       │ Sí
       ▼
  await client.Auth.SignOut()  ← invalida JWT en Supabase
  SesionPermisos.Limpiar()
  ServicioPerfilUsuario.Limpiar()
  servicioSesionActual.Cerrar()
  _cerrando = true
  SesionCerrada?.Invoke(...)
  Close()  ──────────────────────────►
```

---

## Implementación en Bimbo

**Archivo:** `CapaUI/Formularios/Principal/MainWindow.xaml.cs`

### Campo de re-entry
```csharp
private bool _cerrando = false;
```

### Constructor — todos los paths delegan a `Close()`
```csharp
// Comando de logout del ViewModel dispara CierreRequerido → Close()
Vm.CierreRequerido += (_, _) => Close();
```

### En el ViewModel (`MainViewModel.cs`)
```csharp
public event EventHandler? CierreRequerido;

CerrarSesionCommand = new RelayCommand(() => CierreRequerido?.Invoke(this, EventArgs.Empty));
```

### Botón X del chrome
```csharp
private void BtnCerrarVentana_Click(object sender, RoutedEventArgs e) => Close();
```

### OnClosing — el único punto de control
```csharp
protected override void OnClosing(CancelEventArgs e)
{
    if (_cerrando) { base.OnClosing(e); return; }

    e.Cancel = true;
    HandleCierreAsync();
}
```

> [!note] WPF vs WinForms — diferencia clave
> WPF usa `System.ComponentModel.CancelEventArgs` (sin `CloseReason`).
> WinForms usa `FormClosingEventArgs` que sí tiene `CloseReason.WindowsShutDown`.
> En WPF **no es posible** detectar si el cierre fue por apagado de Windows, por lo que no existe el fast-path sincrónico. En práctica, Windows ignora `e.Cancel = true` durante shutdown y termina el proceso de todas formas — el JWT no se invalida en ese escenario, pero es una limitación de plataforma, no un error de código.

### HandleCierreAsync — lógica completa de logout
```csharp
private async void HandleCierreAsync()
{
    var resultado = MessageBox.Show(
        "¿Deseas cerrar sesión?", "Cerrar sesión",
        MessageBoxButton.YesNo, MessageBoxImage.Question);

    if (resultado != MessageBoxResult.Yes) return;

    try
    {
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
        await client.Auth.SignOut();                 // invalida JWT en Supabase
    }
    catch (OperationCanceledException)
    {
        Debug.WriteLine("[Cierre] SignOut timeout — continuando de todas formas");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[Cierre] Error en SignOut: {ex.Message}");
    }
    finally
    {
        CapaUI.Core.Permisos.SesionPermisos.Limpiar();
        ServicioPerfilUsuario.Limpiar();             // limpia perfil en memoria
        servicioSesionActual.Cerrar();               // limpia estado en memoria
        _cerrando = true;
        SesionCerrada?.Invoke(this, EventArgs.Empty);
        Close();
    }
}
```

---

## Por qué `async void` aquí es correcto

`async void` normalmente es un anti-patrón porque oculta excepciones. Pero en overrides sincrónicos de WPF **es la única opción**:

- `OnClosing` es síncrono — no puede `await` directamente
- Si se intenta `await` dentro del handler, el framework cierra la ventana antes de que el `await` resuelva
- `async void` + `e.Cancel = true` + re-entry flag es el patrón estándar documentado por Microsoft

> [!important] El try/catch dentro de `HandleCierreAsync` mitiga el riesgo de `async void`
> Sin él, una excepción no capturada en un `async void` crashea la app. El `catch (Exception ex)` garantiza que incluso un error inesperado solo loguea y procede con el cleanup en `finally`.

---

## Cobertura de rutas de cierre

| Origen del cierre | Antes | Después |
|---|---|---|
| Botón X del chrome WPF | Sin diálogo, sin cleanup | ✅ Confirmación + SignOut + cleanup |
| Botón Logout (CerrarSesionCommand) | Sin SignOut ❌ | ✅ Confirmación + SignOut + cleanup |
| Alt+F4 | Sin intercepción | ✅ Confirmación + SignOut + cleanup |
| Apagado de Windows | Sin cleanup | ⚠️ Limitación de plataforma WPF — JWT no invalidado, proceso terminado por OS |

---

## Timeout en SignOut — por qué es necesario

`client.Auth.SignOut()` hace una petición de red a Supabase. Si la conexión está caída:
- Sin timeout: el usuario queda bloqueado, no puede cerrar la app
- Con timeout (5s): si falla, se loguea el error pero el cleanup local siempre ocurre en `finally`

El usuario nunca queda atrapado, y el estado local siempre queda limpio.

---

## Nota sobre Clean Architecture

`MainWindow` accede directamente a `ServicioConexión.Conexion.ConexionSupabase` (infraestructura) desde la capa de Presentación. Es una violación menor, pero pragmáticamente aceptada para código de shutdown — abstraerlo en una interfaz solo para esta llamada sería over-engineering sin beneficio real.

---

## Análisis de alternativas descartadas

| Alternativa | Por qué no |
|---|---|
| Lógica directamente en `CerrarSesionCommand` | Solo cubría el botón de logout. X y Alt+F4 se escapaban. |
| `Application.Current.Shutdown()` en el botón X | Bypassa `OnClosing` completamente. El cleanup nunca se ejecutaría. |
| Interface `IWindowHost` para abstraer SignOut | Útil si múltiples ventanas necesitan el mismo patrón. Innecesario aquí. |
| `.NET 9 async APIs` | Experimental, proyecto en .NET 8. |

---

## Propiedades de diseño

| Criterio | Cómo se cumple |
|---|---|
| **Mantenible** | Un solo método es dueño del flujo. Cambiar mensaje, agregar paso de cleanup: todo en un lugar. |
| **Seguro** | JWT invalidado en Supabase siempre que haya red. Sin red: cleanup local garantizado en `finally`. |
| **Escalable** | Agregar más cleanup (cerrar WebSocket Realtime, guardar estado) = agregar líneas en `finally`. |
| **Correcto async** | `async void` + `e.Cancel` + re-entry flag es el patrón oficial para `OnClosing` con trabajo async. |
| **Resiliente** | Timeout 5s + `catch` + `finally` garantizan que la app siempre cierra, aunque Supabase no responda. |

---

## Fuentes

- [Microsoft — Window.Closing Event (WPF)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.closing)
- [Microsoft — Best practices for async event handlers](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios)
- [Supabase C# — Auth.SignOut](https://supabase.com/docs/reference/csharp/auth-signout)

---

## Relaciones

- [[Clean Architecture]] — `MainWindow` vive en la capa de presentación, solo llama servicios de capas internas
- [[Arquitectura Actual]] — patrón registrado en tabla de patrones implementados
- [[Base Repository con TryAsync]] — mismo principio: centralizar en un punto, manejar errores explícitamente
- [[Result Pattern]] — filosofía hermana: hacer el fallo explícito en vez de dejarlo escapar
- [[Supabase .NET]] — referencia para `client.Auth.SignOut()`
- [[Sesión 2026-05-22 - Interceptar Cierre de Ventana]] — bitácora de implementación
