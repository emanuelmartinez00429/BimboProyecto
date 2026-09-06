---
title: Security Review — feat/fase6-IntegracionWpf/MenuPrincipal
date: 2026-05-23
tags:
  - seguridad
  - revision
  - bimbo
---

# Security Review — feat/fase6-IntegracionWpf/MenuPrincipal

**Scope:** Todos los archivos modificados en este branch vs. `main`.  
**Metodología:** Revisión manual de código + análisis de falsos positivos con umbral de confianza ≥ 8/10.  
**Resultado: 0 vulnerabilidades confirmadas.**

---

## Hallazgos Evaluados

### 1. Supabase anon key en App.config — FALSO POSITIVO

**Archivo:** `CapaUI/App.config` línea 5  
**Clasificación inicial:** HIGH

La clave incluida en el repositorio tiene payload JWT con `"role":"anon"`. Esta es la **clave pública anónima** de Supabase, equivalente a una API key de cliente. Por diseño:

- Supabase la incluye explícitamente en ejemplos de código frontend/cliente
- El acceso real está controlado por políticas **Row Level Security (RLS)** en la base de datos
- La clave peligrosa es `service_role`, que **no aparece en ningún archivo**

> [!success] Descartado
> La anon key es intencionalmente pública. La seguridad real recae en las políticas RLS de Supabase, no en la ocultación de esta clave.

---

### 2. OTP bypass en recuperación de contraseña — FALSO POSITIVO

**Archivo:** `BimboPesaje/Formularios/InicioSesion/UcForgotCodeForm`  
**Clasificación inicial:** HIGH

Este hallazgo apunta al **proyecto legacy BimboPesaje**, que está siendo descartado. La implementación activa en `CapaUI` es correcta:

```csharp
// ForgotCodePanel.xaml.cs línea 170
var session = await client.Auth.VerifyOTP(
    _email, otp, Supabase.Gotrue.Constants.EmailOtpType.Recovery);

if (session?.User == null)
    throw new Exception("Código inválido o expirado.");
```

El OTP se verifica server-side contra Supabase Auth antes de avanzar al panel de nueva contraseña.

> [!success] Descartado
> El proyecto BimboPesaje no es el path de producción. CapaUI verifica correctamente el OTP.

---

### 3. Email enumeration via password reset — FALSO POSITIVO

**Archivo:** `ForgotEmailPanel.xaml.cs`  
**Clasificación inicial:** HIGH

`client.Auth.ResetPasswordForEmail(email)` siempre retorna éxito (200 OK) independientemente de si el email existe en la base de datos. Este comportamiento está implementado a nivel de plataforma por Supabase GoTrue para prevenir exactamente este tipo de enumeración.

> [!success] Descartado
> La protección contra enumeración es responsabilidad de Supabase GoTrue, no del código de la aplicación. El comportamiento es correcto.

---

### 4. Path traversal en ServicioLogo — FALSO POSITIVO

**Archivo:** `CapaDatos/Logo/ServicioLogo.cs`  
**Clasificación inicial:** HIGH

Dos flujos analizados:

**Descarga (`ObtenerRutaLocalAsync`):** El valor `rutaBucket` (leído de BD) se pasa al SDK de Supabase Storage como identificador de objeto remoto. El archivo local **siempre se escribe en la ruta hardcodeada**:

```csharp
private static readonly string RutaLogoLocal = Path.Combine(CarpetaLocal, "logo.png");
// ...
await File.WriteAllBytesAsync(RutaLogoLocal, bytes); // destino fijo, no afectado por rutaBucket
```

**Subida (`ActualizarLogoAsync`):** `rutaArchivoLocal` es seleccionado por el usuario vía file picker del OS. En una app de escritorio, el usuario lee sus propios archivos — no existe un atacante remoto controlando este parámetro.

> [!success] Descartado
> La ruta de escritura local es hardcodeada. No hay concatenación de input externo en rutas del filesystem.

---

### 5. Política de contraseñas solo client-side — BAJO UMBRAL

**Archivo:** `ForgotNewPanel.xaml.cs` líneas 143–152  
**Clasificación inicial:** MEDIUM  
**Confianza:** ~5/10

La validación de fuerza de contraseña (longitud ≥ 8, mayúscula, dígito, carácter especial) se evalúa en el cliente antes de llamar a `client.Auth.Update()`. Si las políticas de contraseña de Supabase **no están configuradas** en el proyecto, un atacante con acceso directo a la API podría enviar contraseñas débiles bypasseando la UI.

**Por qué no supera el umbral:**
- Es un riesgo de configuración, no un defecto de código
- Supabase permite configurar políticas server-side en `Authentication → Password Policy`
- El usuario final no tiene acceso directo a la API (requeriría un OTP válido de todos modos)

> [!warning] Acción recomendada (no bloqueante)
> Verificar que en el dashboard de Supabase esté habilitada la política de contraseña mínima equivalente a las reglas del cliente: longitud ≥ 8, mayúscula, dígito, carácter especial.

---

### 6. Sin revocación de sesiones tras cambio de contraseña — FALSO POSITIVO

**Archivo:** `ForgotNewPanel.xaml.cs`  
**Clasificación inicial:** MEDIUM

El código **sí llama a `SignOut`** inmediatamente después de actualizar la contraseña:

```csharp
await client.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = NewPassword });
await client.Auth.SignOut(); // ← revoca la sesión actual
ShowSuccess();
```

> [!success] Descartado
> La sesión se invalida correctamente post-cambio.

---

### 7. Console.WriteLine para errores de auth — NO ES VULNERABILIDAD

**Archivo:** `ServicioLogo.cs` línea 66  
**Clasificación inicial:** MEDIUM

`Console.WriteLine` en una aplicación de escritorio escribe a stdout del proceso, visible únicamente al usuario que ejecuta la app (o al administrador del sistema). No contiene credenciales, solo mensajes de error genéricos (`ex.Message`). No hay riesgo de exposición de datos sensibles.

> [!success] Descartado
> No constituye vulnerabilidad en el contexto de una app de escritorio.

---

## Resumen Ejecutivo

| # | Hallazgo | Severidad inicial | Resultado | Confianza |
|---|---|---|---|---|
| 1 | Anon key en App.config | HIGH | Falso positivo | 9/10 |
| 2 | OTP bypass (BimboPesaje) | HIGH | Falso positivo (legacy) | 9/10 |
| 3 | Email enumeration | HIGH | Falso positivo | 8/10 |
| 4 | Path traversal logo | HIGH | Falso positivo | 9/10 |
| 5 | Password policy client-side | MEDIUM | Bajo umbral (config) | 5/10 |
| 6 | Sin revocación post-cambio | MEDIUM | Falso positivo | 9/10 |
| 7 | Console.WriteLine auth | MEDIUM | No es vulnerabilidad | — |

**Vulnerabilidades confirmadas (≥ 8/10): 0**

---

## Acción Pendiente

Verificar en el dashboard de Supabase (`Authentication → Password Policy`) que las políticas server-side coincidan con las reglas del cliente: mínimo 8 caracteres, al menos una mayúscula, un dígito y un carácter especial.
