---
title: "Sesión 2026-09-11 — Bloquear el código OTP para cuentas deshabilitadas"
date: 2026-09-11
tags:
  - bitacora
  - sesion
  - seguridad
  - auth
  - supabase
  - rpc
aliases:
  - Riesgo residual P-059 cerrado
  - verificar_cuenta_habilitada_por_correo
---

# Sesión 2026-09-11 — Bloquear el código OTP para cuentas deshabilitadas

## Resumen

Cierra el riesgo residual que había quedado documentado al traer P-059: el chequeo de `id_estado` corría **después** de validar el OTP, así que una cuenta deshabilitada todavía podía pedir el código y consumir un envío de correo, aunque no pudiera terminar el cambio de contraseña. Ahora `EnviarCodigoAsync` verifica el estado de la cuenta **antes** de llamar a `client.Auth.ResetPasswordForEmail`, usando una función nueva y de solo lectura en Supabase.

## Motivación y decisión de producto

Fernando reportó el comportamiento como un bug. La discusión que siguió (documentada acá porque es relevante para quien lo audite después) fue sobre **cómo** cerrarlo sin abrir un hueco de enumeración de cuentas:

1. Los dos chequeos de `id_estado` que ya existían (`AuthService.LoginAsync`, `VerificarCodigoAsync`) solo pueden correr **después** de que el usuario demostró ser el dueño de la cuenta (contraseña u OTP correcto). Revisar el estado **antes** de esa prueba — con solo el correo — es una superficie nueva: cualquiera podría usarla para preguntar "¿este correo tiene una cuenta deshabilitada?" sin demostrar nada.
2. Se descartó reutilizar `crear_usuario_empleado_seguro` (la única función existente que ya consulta `auth.users` por correo) — exige permiso `USUARIOS_CREAR` y tiene efecto secundario de inserción; mezclarla con un chequeo anónimo de solo lectura habría sido un mal atajo.
3. **Decisión final de Fernando:** función nueva, de solo lectura, que solo distingue "cuenta que existe y está deshabilitada" de todo lo demás (no existe, existe y está habilitada) — y el mensaje que ve el usuario es **deliberadamente genérico** ("No pudimos procesar tu solicitud..."), no "cuenta deshabilitada", para que alguien probando correos al azar no pueda distinguir una cuenta deshabilitada de un error técnico cualquiera.

## Cambios aplicados

### 1. Función nueva en Supabase — `verificar_cuenta_habilitada_por_correo(p_correo text) RETURNS boolean`

`supabase/migrations/20260911103124_verificar_cuenta_habilitada_por_correo.sql`. `SECURITY DEFINER`, `search_path` fijo, otorgada a `anon` y `authenticated` (tiene que ser llamable sin sesión). Busca el correo en `auth.users` (mismo patrón que `crear_usuario_empleado_seguro`); si no hay coincidencia, o la cuenta de Auth no tiene fila en `public.usuarios`, devuelve `true` (deja pasar, igual que hoy). Solo devuelve `false` cuando hay una fila en `usuarios` con `id_estado <> 1`.

### 2. `CapaDatos/Auth/RecuperacionPasswordService.cs` — `EnviarCodigoAsync`

Antes de `client.Auth.ResetPasswordForEmail(email)`, llama a la función nueva vía `client.Rpc(...)`. Si devuelve `false`, corta ahí mismo con `Result.Fail("No pudimos procesar tu solicitud. Si el problema continúa, contacta a soporte.")` — sin mandar el correo. La vista (`ForgotEmailPanel`) no necesitó ningún cambio: el mensaje ya se muestra en el `ErrorContainer`/`LblError` rojo que existe debajo del campo de correo desde antes.

## Verificación

- Probado directo contra la base viva: `verificar_cuenta_habilitada_por_correo` con un correo inexistente → `true`; con dos correos de cuentas reales deshabilitadas → `false` en ambos.
- `get_advisors` (security) corrido después de aplicar: la función nueva no aparece en ninguna advertencia.
- **Compilación:** `dotnet build BimboProyecto.sln -m:1` → 0 Advertencias, 0 Errores.
- **Pruebas:** `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` → 424/424.

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-059 (donde había quedado registrado este riesgo residual)
- [[Sesión 2026-09-10 - Cierre de P-022 P-056 P-059]] — sesión original de P-059, con la advertencia de riesgo residual que esta sesión cierra
