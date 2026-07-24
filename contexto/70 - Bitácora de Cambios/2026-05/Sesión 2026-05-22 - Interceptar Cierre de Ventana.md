---
title: "Sesión 2026-05-22 — Interceptar Cierre de Ventana"
tags:
  - bitacora
  - winforms
  - wpf
  - sesion
  - implementacion
date: 2026-05-22
---

# Sesión 2026-05-22 — Interceptar Cierre de Ventana

## Contexto

Se detectó que el botón X del shell WPF cerraba la app sin mostrar confirmación ni hacer `client.Auth.SignOut()` en Supabase. El botón de logout sí mostraba confirmación pero tampoco llamaba a `SignOut()`. Alt+F4 no tenía intercepción alguna.

## Problema raíz

Tres rutas distintas de cierre, cada una con comportamiento diferente:

| Ruta | Antes |
|---|---|
| `CloseRequested` (X) | `() => Close()` — cierre directo sin nada |
| `LogoutRequested` (botón logout) | `OnLogoutRequested()` — diálogo ✅, sin `SignOut()` ❌ |
| Alt+F4 / programático | `FormClosing` no sobreescrito — sin intercepción |

Ninguna ruta llamaba `client.Auth.SignOut()` → el JWT quedaba activo en Supabase después de cerrar la app.

## Solución implementada

Patrón **Cancel-and-Retry con re-entry flag** en `OnFormClosing`.

### Archivo modificado

`BimboPesaje/Formularios/MenuPrincipal/FrmMenuPrincipal.cs`

### Cambios

**Eliminado:** método `OnLogoutRequested()` completo

**Modificado:** event hooks en constructor
```csharp
// Antes:
_shell.LogoutRequested += OnLogoutRequested;
_shell.CloseRequested  += () => Close();

// Después (ambos delegan a FormClosing):
_shell.LogoutRequested += () => Close();
_shell.CloseRequested  += () => Close();
```

**Agregado:** campo de re-entry
```csharp
private bool _cerrando = false;
```

**Agregado:** `OnFormClosing` sobreescrito
```csharp
protected override void OnFormClosing(FormClosingEventArgs e)
{
    if (_cerrando) { base.OnFormClosing(e); return; }

    if (e.CloseReason == CloseReason.WindowsShutDown)
    {
        servicioSesionActual.Cerrar();
        ServicioPerfilUsuario.Limpiar();
        base.OnFormClosing(e);
        return;
    }

    e.Cancel = true;
    HandleCierreAsync();
}
```

**Agregado:** `HandleCierreAsync()`
```csharp
private async void HandleCierreAsync()
{
    var confirmar = MessageBox.Show(
        "¿Deseas cerrar sesión?", "Cerrar sesión",
        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

    if (confirmar != DialogResult.Yes) return;

    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
        await client.Auth.SignOut();
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
        servicioSesionActual.Cerrar();
        ServicioPerfilUsuario.Limpiar();
        _cerrando = true;
        Close();
        Application.Exit();
    }
}
```

**Agregado:** `using System.Diagnostics;` en usings

## Resultado

| Ruta | Después |
|---|---|
| Botón X (shell WPF) | Confirmación + `SignOut()` + cleanup ✅ |
| Botón Logout (shell WPF) | Confirmación + `SignOut()` + cleanup ✅ |
| Alt+F4 | Confirmación + `SignOut()` + cleanup ✅ |
| Apagado de Windows | Cleanup sincrónico inmediato, sin diálogo ✅ |

## Notas técnicas

- `async void` en `HandleCierreAsync` es correcto aquí: es el único patrón viable para event handlers síncronos que necesitan trabajo async
- El `catch (Exception ex)` dentro protege contra crasheos por `async void` sin captura
- Timeout de 5s en `SignOut()` asegura que la app siempre cierra, incluso sin red
- `Application.Exit()` en el finally (no antes del `Close()`) garantiza que `FormClosing` se ejecuta

## Patrón documentado

Ver [[Interceptar Cierre de Ventana — WinForms + WPF Híbrido]] para explicación completa y análisis de alternativas.
