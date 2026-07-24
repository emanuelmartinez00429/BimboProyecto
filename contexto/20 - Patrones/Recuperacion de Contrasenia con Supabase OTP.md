---
title: Recuperación de Contraseña con Supabase OTP
tags:
  - patron
  - wpf
  - supabase
  - autenticacion
  - seguridad
  - dotnet
aliases:
  - Password Reset OTP
  - ForgotPassword
  - Recuperar Contraseña
---

# Recuperación de Contraseña con Supabase OTP

> [!abstract] El problema
> En una app de escritorio WPF, el flujo estándar de Supabase para recuperación de contraseña envía un **link** al email del usuario. Los links contienen tokens en el URL fragment (`#`), que una app de escritorio no puede interceptar ni parsear. Se necesita un flujo alternativo basado en **OTP de 8 dígitos** que el usuario ingresa manualmente.

---

## Flujo completo

```
[ForgotEmailPanel]              [ForgotCodePanel]                    [ForgotNewPanel]
ResetPasswordForEmail(email)    VerifyOTP(email, otp, Recovery)      Auth.Update({ Password })
→ Supabase genera OTP           → Supabase valida código             → Contraseña actualizada
→ Envía email con {{ .Token }}  → Auto-setea sesión Recovery         → Auth.SignOut()
                                → Navega a ForgotNewPanel            → Volver al login
```

---

## Patrón estructural: Chain of Responsibility

Los 3 paneles forman una cadena donde cada uno hace su paso y pasa el contexto al siguiente:

```
LoginWindow
    │
    ├─► ForgotEmailPanel  ──email──►  ForgotCodePanel  ──email──►  ForgotNewPanel
    │        │                              │                            │
    │   ResetPassword()              VerifyOTP()                   Auth.Update()
    │                                                               Auth.SignOut()
    │                                                                    │
    └◄───────────────────────── NavigarAlLogin() ◄──────────────────────┘
```

`LoginWindow` actúa como **contexto compartido**: provee `NavigateTo(UserControl, title)` y `NavigarAlLogin()` que cada panel usa para navegar sin conocerse entre sí.

---

## Implementación en Bimbo

**Archivos:** `CapaUI/Formularios/InicioSesion/`

### Panel 1 — ForgotEmailPanel.xaml.cs

```csharp
private async void BtnSend_Click(object sender, RoutedEventArgs e)
{
    var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
    await client.Auth.ResetPasswordForEmail(email);
    _win.NavigateTo(new ForgotCodePanel(_win, email), "Verificar código");
}
```

### Panel 2 — ForgotCodePanel.xaml.cs

```csharp
private async Task VerifyAsync()
{
    string otp  = GetCode(); // concatena los 8 TextBox de dígitos
    var client  = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
    var session = await client.Auth.VerifyOTP(
        _email, otp, Supabase.Gotrue.Constants.EmailOtpType.Recovery);

    if (session?.User == null)
        throw new Exception("Código inválido o expirado.");

    _timer?.Stop();
    _win.NavigateTo(new ForgotNewPanel(_win, _email), "Nueva contraseña");
}
```

> [!note] `VerifyOTP` auto-setea la sesión
> Después de una llamada exitosa, el singleton de `ConexionSupabase` tiene activa una sesión de tipo `PasswordRecovery`. `Auth.Update()` en el siguiente panel la usa automáticamente sin necesidad de pasar tokens.

### Panel 3 — ForgotNewPanel.xaml.cs

```csharp
private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
{
    var client = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
    await client.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = NewPassword });
    await client.Auth.SignOut(); // limpia sesión Recovery — usuario debe loguearse con nueva contraseña
    ShowSuccess();
}
```

---

## Dónde genera Supabase el OTP

`ResetPasswordForEmail()` hace `POST /recover` al servidor de Supabase Auth (Go). El servidor llama:

```
internal/api/mail.go → sendPasswordRecovery()
    └─► internal/crypto/crypto.go → GenerateOtp(otpLength)
            └─► crypto/rand.Int(rand.Reader, big.NewInt(10^digits))
```

El código se genera con `crypto/rand` de Go (criptográficamente seguro). Tu app **nunca ve el OTP** — solo llega al email del usuario. Supabase no lo devuelve en la respuesta del `/recover`.

---

## Configuración requerida en Supabase Dashboard

> [!warning] Sin esto el flujo no funciona
> **Authentication → Email Templates → Reset Password**
>
> El template debe usar `{{ .Token }}` (código de dígitos), NO `{{ .ConfirmationURL }}` (link).

Template recomendado:
```html
<h2>Recuperar contraseña — Bimbo Honduras</h2>
<p>Tu código de verificación es:</p>
<p style="font-size: 32px; font-weight: bold; letter-spacing: 8px;">{{ .Token }}</p>
<p>Este código expira en 10 minutos.</p>
```

El OTP length (8 dígitos) es el configurado por defecto en el proyecto de Supabase.

---

## UI del panel de código (ForgotCodePanel)

- **8 TextBox** individuales (`D1`–`D8`), cada uno acepta 1 dígito
- `Tag` en XAML es `string` → se lee con `Convert.ToInt32(box.Tag)` (no `(int)box.Tag` que tira `InvalidCastException`)
- Navegación automática entre cajas al escribir/borrar/flechas
- Soporte de **paste**: si se pega un string de 8 dígitos, se distribuye automáticamente
- **Countdown de 45s** antes de mostrar "Reenviar código"
- Tamaño de caja: `44×52px`, fuente `20px` (ajustado para que quepan 8 en el panel)

---

## Validación de contraseña nueva (ForgotNewPanel)

El panel exige que la nueva contraseña cumpla 4 reglas antes de habilitar el botón:

| Regla | Mínimo |
|---|---|
| Longitud | 8 caracteres |
| Mayúsculas | Al menos 1 |
| Números | Al menos 1 |
| Símbolos | Al menos 1 carácter no alfanumérico |

Incluye medidor de fortaleza visual (5 barras: Muy débil → Fuerte) y confirmación de contraseña con validación en tiempo real.

---

## Sesión Recovery vs sesión normal

| Característica | Sesión normal (login) | Sesión Recovery (VerifyOTP) |
|---|---|---|
| Tipo | `SignedIn` | `PasswordRecovery` |
| Propósito | Usar la app | Solo cambiar contraseña |
| Después de usar | Persiste | Se limpia con `Auth.SignOut()` |
| Acceso a datos | Completo | Limitado a `Auth.Update()` |

Por eso se llama `Auth.SignOut()` después del `Update()` exitoso — el usuario no debe quedar logueado con una sesión Recovery.

---

## Propiedades de diseño

| Criterio | Cómo se cumple |
|---|---|
| **Seguro** | OTP validado server-side, sesión Recovery limpiada tras uso |
| **Resiliente** | Errores de VerifyOTP y Update tienen catch que resetea la UI |
| **Mantenible** | Cada panel es responsable de un solo paso — Chain of Responsibility |
| **UX** | Countdown, reenvío, paste de código, medidor de fortaleza, reglas visibles |

---

## Relaciones

- [[Clean Architecture]] — paneles en CapaUI/Presentación, ServicioConexión en infraestructura
- [[Interceptar Cierre de Ventana]] — mismo proyecto de autenticación
- [[Supabase .NET]] — referencia para `VerifyOTP`, `Auth.Update`, `Auth.SignOut`
- [[Arquitectura Actual]] — módulo de login/auth
- [[Sesión 2026-05-22 - Recuperación de Contraseña]] — bitácora de implementación
