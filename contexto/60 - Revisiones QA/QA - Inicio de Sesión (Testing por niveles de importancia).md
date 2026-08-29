---
title: QA — Inicio de Sesión (Testing por niveles de importancia)
type: qa
status: vigente
tags:
  - qa
  - revision
  - login
  - autenticacion
  - seguridad
  - dominio
  - tests
date: 2026-08-21
updated: 2026-08-21
summary: "Se creó una suite completa de 112 tests para el módulo de inicio de sesión, divididos en 4 niveles de prioridad (P0–P3). Se detectó un bug real en el medidor de…"
scope:
  - CapaDominio/Reglas
symbols:
  - CalcularScore
  - CumpleTodasLasReglas
  - EsDigitoOtp
  - LongitudOtp
  - OtpCompleto
  - OtpValido
  - ReglasContrasena
  - ReglasLogin
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente QA) — Sesión Fernando
revisor: Antigravity QA
estado: Cerrado
---

# QA — Inicio de Sesión (Testing por niveles de importancia)

> [!success] Resultado
> Se creó una suite completa de 112 tests para el módulo de inicio de sesión, divididos en 4 niveles de prioridad (P0–P3). Se detectó un bug real en el medidor de fortaleza de contraseña. Se migró **toda la lógica de validación** de la CapaUI al dominio en C# puro, creando `ReglasContrasena` y `ReglasLogin` como fuente única de verdad.

---

## Alcance del QA

El módulo de inicio de sesión cubre cuatro pantallas:

| Pantalla | Archivo |
|---|---|
| Login principal | `LoginWindow.xaml.cs` |
| Recuperar contraseña — Email | `ForgotEmailPanel.xaml.cs` |
| Recuperar contraseña — Código OTP | `ForgotCodePanel.xaml.cs` |
| Recuperar contraseña — Nueva contraseña | `ForgotNewPanel.xaml.cs` |

---

## Tests creados — `LoginQATests.cs`

**Total: 112 tests — 112/112 ✅ (0 fallos)**

Los tests se dividen en 4 niveles de importancia:

### 🔴 P0 · Crítico — Autenticación básica

> Si falla, el acceso al sistema queda completamente bloqueado.

| Clase de test | Qué verifica |
|---|---|
| `P0_CredencialesCompletas` | `ReglasLogin.CredencialesCompletas` habilita el botón solo con email Y password |
| `P0_ValidacionCorreoLogin` | Correos válidos/inválidos/vacíos contra `ReglasFormato.EsCorreo` |

### 🟠 P1 · Alto — Recuperación de contraseña

> Afecta directamente la seguridad y el acceso de usuarios bloqueados.

| Clase de test | Qué verifica |
|---|---|
| `P1_RecuperacionEmail` | Email válido habilita botón "Enviar código" en ForgotEmailPanel |
| `P1_ValidacionOTP` | Longitud OTP = 8, solo dígitos, pegado válido/inválido, casillas completas |

### 🟡 P2 · Medio — Nueva contraseña y fortaleza

> Su fallo permite contraseñas débiles o no habilita el guardado correctamente.

| Clase de test | Qué verifica |
|---|---|
| `P2_CriteriosIndividuales` | Cada regla por separado: largo, mayúscula, número, símbolo |
| `P2_ValidacionCompleta` | `CumpleTodasLasReglas` habilita el botón Actualizar |
| `P2_ConfirmacionContrasenna` | Coincidencia de contraseñas |
| `P2_ScoreFortaleza` | Score 0–5 consistente con la lógica de habilitación del botón |

### 🟢 P3 · Bajo — Casos límite y polish

> No bloquean el acceso pero pueden confundir al usuario.

| Clase de test | Qué verifica |
|---|---|
| `P3_TrimCorreo` | El correo se trimea antes de enviarse |
| `P3_TemporizadorReenvio` | Timer OTP: inicia en 45 s, controla visibilidad del botón Reenviar |
| `P3_IndicadoresCoherentes` | Indicadores R1–R4 usan las mismas funciones del dominio que la validación real |

---

## Bug detectado

### Medidor de fortaleza inconsistente con la validación real

**Severidad:** Media (P2)
**Archivo afectado:** `ForgotNewPanel.xaml.cs` — `UpdateStrength()`

**Problema:** El score visual podía mostrar "Aceptable" (3 barras amarillas) para contraseñas de menos de 8 caracteres, mientras que el botón Actualizar permanecía deshabilitado sin ninguna explicación visual clara.

```
"Bi1!" → score visual = 3 ("Aceptable") ❌
         botón = deshabilitado            ✅
         → contradicción
```

**Causa raíz:** La longitud mínima (`>= 8`) era uno de los 5 criterios sumables al mismo nivel que mayúscula, número y símbolo. Una contraseña corta con mayúscula + número + símbolo sumaba 3 puntos aunque la longitud fallara.

**Fix aplicado:** La longitud mínima se convirtió en prerequisito. Si `Length < 8`, el score es 0 sin importar los demás criterios.

```
"Bi1!" → score visual = 0 (oculto)  ✅
         botón = deshabilitado       ✅
         → consistente
```

> [!note] El fix fue aplicado en la misma sesión por otro agente antes de la migración al dominio.

---

## Refactorización — Validaciones al dominio

Se crearon dos clases de **C# puro sin dependencias externas** en `CapaDominio/Reglas/`:

### `ReglasContrasena.cs`

| Método | Descripción |
|---|---|
| `TieneLargoMinimo(pwd)` | `Length >= 8` |
| `TieneMayuscula(pwd)` | Al menos una letra mayúscula |
| `TieneNumero(pwd)` | Al menos un dígito |
| `TieneSimbolo(pwd)` | Al menos un carácter no alfanumérico |
| `CumpleTodasLasReglas(pwd)` | Los 4 criterios juntos |
| `CalcularScore(pwd)` | Score 0–5 para el medidor visual |

### `ReglasLogin.cs`

| Miembro | Descripción |
|---|---|
| `CredencialesCompletas(email, pwd)` | Email con contenido Y password no vacío |
| `LongitudOtp` | Constante: `8` |
| `OtpValido(otp)` | 8 caracteres, todos dígitos |
| `OtpCompleto(casillas)` | Todas las casillas tienen 1 carácter |
| `EsDigitoOtp(c)` | El carácter es un dígito numérico |

### Archivos de UI actualizados

Los siguientes archivos dejaron de tener lógica propia y ahora consumen el dominio:

- `LoginWindow.xaml.cs` → `ReglasLogin.CredencialesCompletas()`
- `ForgotCodePanel.xaml.cs` → `OtpCompleto`, `EsDigitoOtp`, `OtpValido`, `LongitudOtp`
- `ForgotNewPanel.xaml.cs` → `CalcularScore`, criterios individuales, `CumpleTodasLasReglas`

> [!important] Beneficio clave: los tests de QA ahora llaman directamente a las clases del dominio. Si una regla cambia en producción, los tests lo detectan automáticamente sin necesidad de actualizarlos.

---

## Resultado final

| Métrica | Valor |
|---|---|
| Tests totales | 112 |
| Tests pasando | 112 ✅ |
| Tests fallando | 0 |
| Bugs encontrados | 1 (medidor de fortaleza) |
| Bugs corregidos | 1 ✅ |
| Archivos nuevos (dominio) | 2 (`ReglasContrasena`, `ReglasLogin`) |
| Archivos UI simplificados | 3 |

## Relacionado

- [[Sesión 2026-08-21 - QA Login y migración de validaciones al dominio]]
