---
title: "Sesión 2026-08-21 — QA Login y migración de validaciones al dominio"
tags:
  - sesion
  - qa
  - login
  - autenticacion
  - dominio
  - refactorizacion
  - tests
date: 2026-08-21
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Fernando / Antigravity (agente)
---

# Sesión 2026-08-21 — QA Login y migración de validaciones al dominio

> [!success] Resultado
> Se crearon 112 tests de QA para el inicio de sesión (P0–P3), se detectó y corrigió un bug en el medidor de fortaleza de contraseña, y se migró toda la lógica de validación desde la CapaUI al dominio mediante dos nuevas clases de C# puro: `ReglasContrasena` y `ReglasLogin`.

---

## Problema / motivo

1. **Sin cobertura de tests en login:** El flujo de inicio de sesión y recuperación de contraseña no tenía ningún test automatizado. Cualquier regresión en la validación de credenciales, OTP o nueva contraseña pasaba desapercibida.

2. **Lógica duplicada en la UI:** Las reglas de validación (formato de correo, criterios de contraseña, longitud del OTP) estaban hardcodeadas en cada panel de la CapaUI de forma independiente, sin fuente única de verdad.

3. **Bug en el medidor de fortaleza:** La contraseña `"Bi1!"` (3 chars, mayúscula + número + símbolo) mostraba score 3 = "Aceptable" en el medidor visual, pero el botón Actualizar permanecía deshabilitado porque `Length < 8`. El usuario veía una señal visual contradictoria sin poder entender por qué el botón no se habilitaba.

---

## Cambios realizados

### 1. Suite de QA — `LoginQATests.cs`

**Ubicación:** `BimboProyecto.Tests/Auth/LoginQATests.cs`

112 tests organizados en 4 niveles de prioridad:

| Nivel | Descripción | Tests |
|---|---|---|
| 🔴 P0 Crítico | Credenciales de acceso al sistema | 13 |
| 🟠 P1 Alto | Flujo completo de recuperación de contraseña | 17 |
| 🟡 P2 Medio | Reglas de nueva contraseña y medidor de fortaleza | 19 |
| 🟢 P3 Bajo | Casos límite: trim, temporizador, indicadores visuales | 8 |

**Resultado:** 112/112 ✅

### 2. Fix — Medidor de fortaleza consistente

**Archivo:** `CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml.cs`

La longitud mínima (`>= 8`) pasó a ser **prerequisito** del score en lugar de un criterio aditivo al mismo nivel. Sin longitud mínima, el score es 0 independientemente de cuántos otros criterios se cumplan.

```
ANTES: "Bi1!" → score 3 = "Aceptable" (contradice botón deshabilitado)
DESPUÉS: "Bi1!" → score 0 = (oculto)  (coherente con botón deshabilitado)
```

### 3. Nuevas clases en el dominio (C# puro)

**`CapaDominio/Reglas/ReglasContrasena.cs`**
- `TieneLargoMinimo`, `TieneMayuscula`, `TieneNumero`, `TieneSimbolo`
- `CumpleTodasLasReglas` — fuente única para habilitar el botón
- `CalcularScore` — medidor de fortaleza 0–5

**`CapaDominio/Reglas/ReglasLogin.cs`**
- `CredencialesCompletas` — habilita botón Ingresar
- `LongitudOtp = 8` — constante única para la longitud del OTP
- `OtpValido`, `OtpCompleto`, `EsDigitoOtp`

### 4. Simplificación de la CapaUI

Los tres paneles dejaron de tener lógica propia:

| Archivo | Cambio |
|---|---|
| `LoginWindow.xaml.cs` | `CredencialesCompletas()` en lugar de `emailOk && pwdOk` inline |
| `ForgotCodePanel.xaml.cs` | `OtpCompleto`, `EsDigitoOtp`, `OtpValido`, `LongitudOtp` |
| `ForgotNewPanel.xaml.cs` | `CalcularScore`, criterios individuales, `CumpleTodasLasReglas` |

---

## Archivos creados / modificados

| Tipo | Archivo |
|---|---|
| ✅ Nuevo | `CapaDominio/Reglas/ReglasContrasena.cs` |
| ✅ Nuevo | `CapaDominio/Reglas/ReglasLogin.cs` |
| ✅ Nuevo | `BimboProyecto.Tests/Auth/LoginQATests.cs` |
| ✏️ Modificado | `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs` |
| ✏️ Modificado | `CapaUI/Formularios/InicioSesion/ForgotCodePanel.xaml.cs` |
| ✏️ Modificado | `CapaUI/Formularios/InicioSesion/ForgotNewPanel.xaml.cs` |

---

## Verificación

```
dotnet test BimboProyecto.Tests\BimboProyecto.Tests.csproj
→ Correctas! Con error: 0, Superado: 112, Omitido: 0, Total: 112
```

## Relacionado

- [[QA - Inicio de Sesión (Testing por niveles de importancia)]]
