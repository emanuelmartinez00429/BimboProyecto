---
title: "Sesión 2026-05-22 — Recuperación de Contraseña"
tags:
  - bitacora
  - wpf
  - supabase
  - autenticacion
  - implementacion
date: 2026-05-22
---

# Sesión 2026-05-22 — Recuperación de Contraseña

## Contexto

Los 3 paneles de recuperación de contraseña existían en `CapaUI` con la UI completa pero la lógica de Supabase estaba incompleta o ausente. El flujo real con Supabase OTP no funcionaba.

---

## Problemas encontrados

| Panel | Estado inicial | Problema |
|---|---|---|
| `ForgotEmailPanel` | ✅ Correcto | Llamaba `ResetPasswordForEmail()` — OK |
| `ForgotCodePanel` | ❌ Falso | `VerifyAsync()` solo hacía `Task.Delay(800)` y navegaba sin verificar |
| `ForgotNewPanel` | ⚠️ Incompleto | Llamaba `Auth.Update()` pero sin sesión activa (VerifyOTP nunca se llamó) |

Errores adicionales:
- `InvalidCastException` al tipear: `(int)box.Tag` falla porque en WPF el `Tag` de XAML es `string`, no `int`
- 6 dígitos en UI vs 8 dígitos que envía Supabase
- Panel cortado visualmente: cajas de 56px × 8 no caben en el ancho disponible

---

## Cambios implementados

### 1. `CapaUI/Formularios/InicioSesion/ForgotCodePanel.xaml.cs`

**`VerifyAsync()` — de falso a real:**
```csharp
// ANTES:
await Task.Delay(800);
_win.NavigateTo(new ForgotNewPanel(_win, _email), "Nueva contraseña");

// DESPUÉS:
string otp  = GetCode();
var client  = await ServicioConexión.Conexion.ConexionSupabase.GetClientAsync();
var session = await client.Auth.VerifyOTP(
    _email, otp, Supabase.Gotrue.Constants.EmailOtpType.Recovery);
if (session?.User == null)
    throw new Exception("Código inválido o expirado.");
_timer?.Stop();
_win.NavigateTo(new ForgotNewPanel(_win, _email), "Nueva contraseña");
```

**Fix `InvalidCastException`:**
```csharp
// ANTES: int idx = (int)box.Tag;       ← crash: Tag es string en WPF
// DESPUÉS:
int idx = Convert.ToInt32(box.Tag);
```

**Null guards** en `CheckComplete`, `Digit_TextChanged`, `Digit_PreviewKeyDown`:
```csharp
if (_digits == null) return; // _digits se inicializa en Loaded, no en el constructor
```

**8 dígitos:** array actualizado a `[D1, D2, D3, D4, D5, D6, D7, D8]`, límites de navegación a `idx < 7`, paste verifica `text.Length == 8`

**Subtítulo:** `"código de 6 dígitos"` → `"código de 8 dígitos"`

---

### 2. `CapaUI/Formularios/InicioSesion/ForgotCodePanel.xaml`

- 6 → 8 TextBox de dígitos (`D7` con `Tag="6"`, `D8` con `Tag="7"`)
- Margen entre cajas: `10px` → `7px`

---

### 3. `CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml.cs`

**SignOut después del Update:**
```csharp
await client.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = NewPassword });
await client.Auth.SignOut(); // ← agregado: limpia sesión Recovery
ShowSuccess();
```

---

### 4. `CapaUI/Formularios/InicioSesion/LoginResources.xaml`

**Tamaño del `PinDigitBox` reducido** para que 8 cajas quepan en el panel:

| Propiedad | Antes | Después |
|---|---|---|
| Width | 56px | 44px |
| Height | 64px | 52px |
| FontSize | 26 | 20 |

Cálculo: `44×8 + 7×7 = 401px` — cabe en el panel sin cortar.

---

### 5. `BimboPesaje/Formularios/MenuPrincipal/FrmMenuPrincipal.cs` *(excepción)*

> [!warning] Excepción única a la regla de no tocar BimboPesaje
> Errores de compilación pre-existentes impedían buildar la solución completa.

**Fix:** `GestionProveedores`, `GestionFabricantes`, `GestionCategorias` no encontradas.

```csharp
// Causa: namespace faltante. Fix con alias para evitar ambigüedad con ProductosView:
using BimboPesajeProductos = BimboPesaje.Formularios.Productos;

// Uso:
AbrirFormHijo(new BimboPesajeProductos.GestionProveedores());
AbrirFormHijo(new BimboPesajeProductos.GestionFabricantes());
AbrirFormHijo(new BimboPesajeProductos.GestionCategorias());
```

---

## Configuración externa requerida

En **Supabase Dashboard → Authentication → Email Templates → Reset Password**, cambiar `{{ .ConfirmationURL }}` por `{{ .Token }}` para que el email llegue con el código de 8 dígitos en lugar de un link.

---

## Resultado

- Flujo completo de recuperación funciona end-to-end con Supabase OTP
- UI de 8 dígitos cabe correctamente en el panel
- `InvalidCastException` eliminado
- Solución completa compila con 0 errores

---

## Patrón documentado

Ver [[Recuperación de Contraseña con Supabase OTP]] para explicación completa del flujo, arquitectura y decisiones de diseño.
