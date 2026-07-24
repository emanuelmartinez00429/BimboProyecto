---
title: "Sesión 2026-05-22 — Refactor Login + QA Fix Perfil"
tags:
  - bitacora
  - wpf
  - clean-architecture
  - autenticacion
  - refactor
  - performance
date: 2026-05-22
---

# Sesión 2026-05-22 — Refactor Login + QA Fix Perfil

## Contexto

Dos bloques de trabajo en esta sesión:

1. **Refactor del login** — la lógica de autenticación vivía directamente en `LoginWindow.xaml.cs` (capa de Presentación). Se movió a la capa de Aplicación siguiendo [[Clean Architecture]].
2. **QA Fix #4** — `ServicioPerfilUsuario` cargaba toda la tabla `usuarios` para quedarse con un solo registro. Corregido con query individual server-side.

---

## Bloque 1 — Refactor de Login a CapaAplicacion

### Problema

`LoginWindow.xaml.cs` contenía directamente:
- Llamada a `client.Auth.SignInWithPassword()`
- Consulta a `RepositorioUsuario`
- Lógica de validación de credenciales

Violación de Clean Architecture: la capa de Presentación no debe conocer la infraestructura de datos.

### Solución — Archivos creados

#### `CapaAplicacion4/Auth/Dtos/LoginResultDto.cs` *(nuevo)*
```csharp
namespace CapaAplicacion.Auth.Dtos;

public sealed class LoginResultDto
{
    public int    IdUsuario { get; init; }
    public string Email     { get; init; } = "";
    public int    IdRol     { get; init; }
}
```

#### `CapaAplicacion4/Auth/Interfaces/IAuthService.cs` *(nuevo)*
```csharp
using CapaAplicacion.Auth.Dtos;
using CapaAplicacion.Common;

namespace CapaAplicacion.Auth.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResultDto>> LoginAsync(string email, string password, CancellationToken ct = default);
}
```

#### `CapaDatos/Auth/AuthService.cs` *(nuevo)*
```csharp
public class AuthService : IAuthService
{
    public async Task<Result<LoginResultDto>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        try
        {
            var client  = await ConexionSupabase.GetClientAsync();
            var session = await client.Auth.SignInWithPassword(email, password);

            if (session?.User == null)
                return Result<LoginResultDto>.Fail("Credenciales incorrectas.");

            var usuario = await RepositorioUsuario.ObtenerPorUuidAsync(session.User.Id!);
            if (usuario == null)
            {
                await client.Auth.SignOut();
                return Result<LoginResultDto>.Fail("Usuario no registrado en el sistema.");
            }

            return Result<LoginResultDto>.Ok(new LoginResultDto
            {
                IdUsuario = usuario.idUsuario,
                Email     = email,
                IdRol     = usuario.idRol,
            });
        }
        catch (Exception ex)
        {
            return Result<LoginResultDto>.Fail("Error al iniciar sesión: " + ex.Message);
        }
    }
}
```

### Archivos modificados

| Archivo | Cambio |
|---|---|
| `CapaServicios/servicioSesionActual.cs` | `Iniciar(int, string)` — elimina parámetro `Session`; setters privados; `Sesion` queda null permanentemente |
| `CapaDatos/DependencyInjection.cs` | `services.AddTransient<IAuthService, AuthService>()` |
| `CapaUI/App.xaml.cs` | `services.AddTransient<LoginWindow>()` + resolve via DI |
| `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs` | Inyecta `IAuthService`; `IngresarAsync` solo consume el resultado |
| `CapaUI/Formularios/Principal/MainViewModel.cs` | Fallback de `SesionActual` → `servicioSesionActual` |

### `LoginWindow.xaml.cs` — antes vs después

**Antes:** llamadas directas a Supabase + RepositorioUsuario + manejo de sesión
```csharp
// Todo mezclado en la vista:
var session = await client.Auth.SignInWithPassword(email, password);
var usuario = await RepositorioUsuario.ObtenerPorUuidAsync(session.User.Id);
servicioSesionActual.Iniciar(usuario.idUsuario, email);
await SesionPermisos.CargarAsync(usuario.idRol);
```

**Después:** solo consume el contrato de aplicación
```csharp
var result = await _authService.LoginAsync(email, password);
if (!result.Success) { VolverAlLogin(result.Error); return; }
servicioSesionActual.Iniciar(result.Value!.IdUsuario, email);
await SesionPermisos.CargarAsync(result.Value.IdRol);
await ServicioPerfilUsuario.CargarAsync();
```

### Nota de tech debt

`ServicioPerfilUsuario` sigue en `CapaServicios` (namespace `CapaDominio`) con dependencia directa a `CapaDatos`. Es una violación conocida de Clean Architecture. El refactor completo requiere convertir `RepositorioUsuario` de métodos estáticos a instancia con DI. Pendiente para sesión futura.

---

## Bloque 2 — QA Fix #4: Perfil de usuario query O(n) → O(1)

### Problema

`ServicioPerfilUsuario.CargarAsync()` usaba `obtenerUsuarios()` que trae **todos** los registros con joins, filtrando en cliente:

```csharp
// ANTES — O(n): trae toda la tabla
var lista   = await RepositorioUsuario.obtenerUsuarios();
var usuario = lista.FirstOrDefault(u => u.idUsuario == idActual);
```

SQL generado:
```sql
SELECT *, roles(*), empleados(*)
FROM usuarios
ORDER BY id_usuario ASC
-- Sin WHERE — toda la tabla
```

### Solución

**Nuevo método** en `CapaDatos/Repositorios/Usuario/RepositorioUsuario.cs`:

```csharp
public static async Task<usuarioVista?> ObtenerPorIdAsync(int idUsuario)
{
    var client = await ConexionSupabase.GetClientAsync();
    var resultado = await client
        .From<usuarioVista>()
        .Select("*, roles(*), empleados(*)")
        .Filter("id_usuario", Operator.Equals, idUsuario.ToString())
        .Get();
    return resultado?.Models?.FirstOrDefault();
}
```

SQL generado:
```sql
SELECT *, roles(*), empleados(*)
FROM usuarios
WHERE id_usuario = 42
-- 1 registro exacto
```

**`ServicioPerfilUsuario.cs`** actualizado:

```csharp
// DESPUÉS — O(1): query directa por PK
var usuario = await RepositorioUsuario.ObtenerPorIdAsync(idActual);
```

### Impacto

| | Antes | Después |
|---|---|---|
| Registros traídos de BD | N (todos) | 1 |
| Joins evaluados | N × (roles + empleados) | 1 × (roles + empleados) |
| Filtrado | En memoria (cliente) | Server-side (Supabase) |

---

## Resultado

- Build: **0 errores**
- Login refactorizado a arquitectura limpia con `IAuthService`
- `ServicioPerfilUsuario` ya no carga toda la tabla al iniciar sesión
- Patrones aplicados: [[Result Pattern]], [[Repository Pattern]], [[Clean Architecture]]

---

## Auditoría QA — Estado de hallazgos

| # | Hallazgo | Estado |
|---|---|---|
| 1 | Token Supabase descartado | ✅ Falso positivo — SDK lo gestiona internamente |
| 2 | Sin rate limiting | ✅ Aceptable — Supabase lo maneja server-side |
| 3 | ex.Message expuesto en catch | ✅ **Corregido** — log interno, mensaje genérico al usuario |
| 4 | PerfilUsuario carga toda la tabla | ✅ **Corregido** — query server-side por PK |
| 5 | Permisos hardcodeados | 📋 Tech debt conocido (TODO en código) |
| 6 | IAuthService incompleto | 📋 Refactor de diseño futuro |
| 7 | No valida id_estado al login | ✅ **Corregido** — SignOut + Fail si id_estado ≠ 1 |
| 8 | Sin refresh token | ✅ Falso positivo — SDK auto-refresh |
| 9 | Silent catch en reenvío OTP | ✅ **Corregido** — muestra error y restaura botón |
| 10 | Password toggle en texto plano | ✅ Comportamiento estándar WPF |
| 11 | Logout reentrada potencial | ✅ Mitigado con flag `_cerrando` |
| 12 | Exit sin limpieza de sesión | ✅ Aceptable para app interna |
| 13 | SesionActual.cs código muerto | ⛔ No eliminable — BimboPesaje lo referencia en 5 archivos |
| 14 | Transient vs Scoped en DI | ✅ Falso positivo — Transient es correcto en WPF |
