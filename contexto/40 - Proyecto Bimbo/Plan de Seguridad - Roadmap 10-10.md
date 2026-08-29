---
title: Plan de Seguridad — Roadmap hacia 10/10
type: plan
status: en-progreso
tags:
  - seguridad
  - plan
  - bimbo
date: 2026-05-23
updated: 2026-07-26
summary: "Mejoras de postura de seguridad tras un review con 0 vulnerabilidades confirmadas: pasar de segura a auditablemente segura, por impacto/esfuerzo."
summary_fijo: true
scope:
  - CapaDatos/Logo
  - CapaUI/Formularios/Principal
  - ServicioConexión/Conexion
symbols:
  - AuthService
  - ForgotCodePanel
  - ForgotNewPanel
  - IAuditoriaService
  - MainWindow
  - OnExit
  - Serilog
  - SesionPermisos
  - Settings
  - SignOut
---

# Plan de Seguridad — Roadmap hacia 10/10

> [!note] Contexto
> El security review del branch encontró **0 vulnerabilidades confirmadas**. Lo que sigue no son correcciones de bugs, sino mejoras de *postura de seguridad* para llevar la app de "segura" a "auditablemente segura". Ordenadas por impacto/esfuerzo.

---

## Lo que ya está bien ✅

Antes de lo que falta, es importante saber qué no hay que tocar:

- **OTP verificado server-side** — `ForgotCodePanel` llama `VerifyOTP()` correctamente
- **SignOut tras cambio de contraseña** — `ForgotNewPanel` llama `SignOut()` post-update
- **Verificación de cuenta activa** — `AuthService` comprueba `idEstado == 1` antes de permitir login
- **JWT invalidado en cierre** — `MainWindow.OnClosing` llama `SignOut()` con timeout de 5s
- **RLS en Supabase** — anon key es intencionalmente pública, controlada por Row Level Security
- **Sistema de permisos** — `SesionPermisos` limita la UI según rol
- **Errores de login sin detalles** — `AuthService` devuelve mensaje genérico, no el mensaje de Supabase

---

## Fase 1 — Secretos y Configuración ✅
**Esfuerzo: Bajo | Impacto: Alto**

### 1.1 Sacar la anon key del repositorio ✅ `2026-05-23`

**Problema:** `CapaUI/App.config` contiene la anon key hardcodeada y se commitea al repo. Aunque la key es pública por diseño, no es buena práctica tenerla en el historial de git.

**Solución:** Leer la key desde variable de entorno o desde un archivo excluido del repo.

```csharp
// ConexionSupabase.cs
private static readonly string Url = 
    Environment.GetEnvironmentVariable("SUPABASE_URL") 
    ?? ConfigurationManager.AppSettings["SUPABASE_URL"]!;

private static readonly string Key = 
    Environment.GetEnvironmentVariable("SUPABASE_KEY") 
    ?? ConfigurationManager.AppSettings["SUPABASE_KEY"]!;
```

Agregar al `.gitignore`:
```
CapaUI/App.config
BimboPesaje/App.config
```

Y documentar en un `App.config.example` con valores placeholder.

**Archivos:** `ServicioConexión/Conexion/ConexionSupabase.cs`, `.gitignore`

> [!success] Completado
> - `ConexionSupabase.cs` lee de env var con fallback a `App.config`
> - `CapaUI/App.config` agregado al `.gitignore`
> - Creado `CapaUI/App.config.example` con valores placeholder
> - **Pendiente:** rotar la anon key en Supabase (`Settings → API → Regenerate anon key`) ya que el historial de git previo la contiene

---

### 1.2 Verificar política de contraseñas en Supabase ⏳ pendiente config

**Problema:** La validación de fuerza de contraseña solo existe en el cliente (`ForgotNewPanel`). Si alguien llama directamente a la API de Supabase (con un OTP válido), puede poner cualquier contraseña.

**Acción:** En el dashboard de Supabase → `Authentication → Password Policy`:
- Longitud mínima: 8 caracteres
- Requerir: mayúsculas, números, caracteres especiales

Esto es config, no código — pero si no está hecho, la validación del cliente es solo UX.

---

## Fase 2 — Logging Estructurado y Auditoría
**Esfuerzo: Medio | Impacto: Alto**

### 2.1 Reemplazar Console.WriteLine con Serilog ✅ `2026-05-23`

**Problema:** Hay `Console.WriteLine` en al menos 5 archivos:
- `AuthService.cs:46` — errores de login
- `RepositorioUsuario.cs:24,44,60,79,107` — errores de BD
- `ServicioLogo.cs:66,106` — errores de logo

En producción, estos logs desaparecen. No hay forma de auditar qué falló ni cuándo.

**Solución:** Agregar `Serilog` con dos sinks:
1. **Archivo rotativo** → `%AppData%\BimboPesaje\Logs\app-.log` (rotación diaria, 30 días)
2. **Tabla Supabase** → solo para eventos de seguridad

```csharp
// Program.cs / App.xaml.cs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(logFolder, "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();
```

Reemplazar cada `Console.WriteLine(...)` por `Log.Warning(...)` o `Log.Error(...)`.

**Paquetes NuGet:** `Serilog`, `Serilog.Sinks.File`

> [!success] Completado
> - Instalado `Serilog 4.3.1` + `Serilog.Sinks.File 7.0.0` en `CapaDatos`
> - Configurado en `App.xaml.cs` — logs en `%AppData%\BimboPesaje\Logs\app-.log`, rotación diaria, 30 días
> - `Log.CloseAndFlush()` en `OnExit`
> - Reemplazados **30 `Console.WriteLine`** en 12 archivos por `Serilog.Log.Error/Warning` con excepción completa y parámetros estructurados

### 2.1b Regla — Nunca loguear secretos ✅ `2026-07-26`

> [!danger] Regla permanente
> **Jamás escribir tokens, contraseñas, API keys ni fragmentos de ellos en ningún log** (Serilog, `Debug.WriteLine`, `Console.WriteLine`, Output de VS). "Solo los primeros 20 caracteres" también cuenta como fuga. Si se necesita diagnosticar estado de sesión, loguear el **hecho booleano** (`sesión activa: sí/no`) o el email — nunca la credencial.

**Origen:** P-014 — `UsuarioRepository.CrearAsync` logueaba prefijos del access token en Output (detectado en QA 2026-07-23, corregido 2026-07-26).

**Cómo cumplirla:**
```csharp
// ❌ MAL — fuga parcial de token
Debug.WriteLine($"token={session?.AccessToken?[..20]}...");

// ✅ BIEN — hecho booleano con Serilog estructurado
Serilog.Log.Debug("Sesión admin restaurada: {Restaurada}", session is not null);
```

---

### 2.2 Tabla de Auditoría en Supabase ⏳ planificada

**Problema:** No hay rastro de quién inició sesión, cuándo, ni qué operaciones CRUD realizó.

**Solución:** Crear tabla `audit_log` en Supabase:

```sql
CREATE TABLE audit_log (
    id          BIGSERIAL PRIMARY KEY,
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    id_usuario  INT REFERENCES usuarios(id_usuario),
    accion      TEXT NOT NULL,    -- 'LOGIN', 'LOGOUT', 'CREAR_PRODUCTO', etc.
    entidad     TEXT,             -- 'Producto', 'Usuario', etc.
    id_entidad  TEXT,             -- ID del registro afectado
    detalle     JSONB,            -- datos adicionales
    ip_origen   TEXT              -- para futuras versiones web
);

-- Solo INSERT permitido para anon/authenticated (no UPDATE, no DELETE)
ALTER TABLE audit_log ENABLE ROW LEVEL SECURITY;
CREATE POLICY "insert_only" ON audit_log FOR INSERT TO authenticated WITH CHECK (true);
```

Registrar al menos:
- `LOGIN_OK` / `LOGIN_FAIL` (sin exponer contraseña ni detalles de error interno)
- `LOGOUT`
- `PASSWORD_CHANGE`
- `CREAR` / `EDITAR` / `ELIMINAR` por módulo

**Servicio sugerido:** `IAuditoriaService` con método `RegistrarAsync(string accion, string? entidad, string? idEntidad, object? detalle)` inyectado en `AuthService` y los repositorios CRUD.

---

## Fase 3 — Hardening de Mensajes de Error ✅
**Esfuerzo: Bajo | Impacto: Medio**

### 3.1 No exponer ex.Message a la UI ✅ `2026-05-23`

**Problema:** Dos paneles exponen el mensaje de excepción directamente al usuario:

```csharp
// ForgotCodePanel.xaml.cs:181
LblError.Text = "Código incorrecto o expirado. " + ex.Message;

// ForgotNewPanel.xaml.cs:173
LblError.Text = "Error al actualizar: " + ex.Message;
```

`ex.Message` de Supabase puede contener detalles como "invalid JWT", "connection refused", o URLs internas. Esto da información útil a un atacante.

**Solución:** Mapear excepciones conocidas a mensajes amigables:

```csharp
// ForgotCodePanel — reemplazar
LblError.Text = ex.Message.Contains("expired") || ex.Message.Contains("invalid")
    ? "Código incorrecto o expirado."
    : "Error al verificar el código. Inténtalo de nuevo.";

// ForgotNewPanel — reemplazar
LblError.Text = "No se pudo actualizar la contraseña. Inténtalo de nuevo.";
Log.Error(ex, "Error en ActualizarContraseña para {Email}", _email);
```

**Archivos:** `ForgotCodePanel.xaml.cs:179-186`, `ForgotNewPanel.xaml.cs:172-176`

> [!success] Completado
> - `ForgotCodePanel` → mensaje genérico fijo, sin `ex.Message`
> - `ForgotNewPanel` → mensaje genérico fijo, sin `ex.Message`

---

### 3.2 Validar tipo de archivo en logo upload ✅ `2026-05-23`

**Problema:** `ServicioLogo.ActualizarLogoAsync` no valida que el archivo sea una imagen antes de subirlo al bucket.

```csharp
// Actual — sin validación
byte[] bytes = await File.ReadAllBytesAsync(rutaArchivoLocal);
await client.Storage.From(BucketNombre).Upload(bytes, nuevaRutaBucket);
```

**Solución:** Validar extensión Y magic bytes del archivo:

```csharp
private static readonly HashSet<string> _extensionesPermitidas = [".png", ".jpg", ".jpeg", ".webp"];

// Antes de leer:
string ext = Path.GetExtension(rutaArchivoLocal).ToLowerInvariant();
if (!_extensionesPermitidas.Contains(ext))
    throw new InvalidOperationException("Solo se permiten imágenes PNG, JPG o WEBP.");

// Limitar tamaño (5 MB máx):
var info = new FileInfo(rutaArchivoLocal);
if (info.Length > 5 * 1024 * 1024)
    throw new InvalidOperationException("El logo no puede superar 5 MB.");
```

**Archivo:** `CapaDatos/Logo/ServicioLogo.cs:78`

> [!success] Completado
> - Validación de extensión: solo `.png`, `.jpg`, `.jpeg`, `.webp`
> - Validación de tamaño: máximo 5 MB
> - Ambas validaciones antes de `File.ReadAllBytesAsync`

---

## Fase 4 — Gestión de Sesiones
**Esfuerzo: Medio | Impacto: Medio**

### 4.1 Timeout por inactividad ⏳ pendiente — se implementa junto con pantalla Mi Usuario

**Problema:** No hay cierre de sesión automático. Si el usuario deja la app abierta y se aleja del equipo, la sesión permanece activa indefinidamente.

**Solución:** Timer de inactividad en `MainWindow` que resetea con cualquier input del usuario. Si pasa X minutos sin actividad → `SignOut` + volver al login.

```csharp
// MainWindow.cs
private DispatcherTimer _inactividadTimer;
private const int MINUTOS_TIMEOUT = 30;

// En constructor:
_inactividadTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(MINUTOS_TIMEOUT) };
_inactividadTimer.Tick += (_, _) => Dispatcher.Invoke(Close);
_inactividadTimer.Start();

// En PreviewMouseMove, PreviewKeyDown:
_inactividadTimer.Stop();
_inactividadTimer.Start();
```

**Archivo:** `CapaUI/Formularios/Principal/MainWindow.xaml.cs`

---

### 4.2 Almacenamiento seguro del "Recordar usuario"

**Problema actual:** No está implementado (solo guarda el email en `Settings`). Si algún día se implementara guardar credenciales, usar **Windows DPAPI**.

```csharp
// Para guardar
byte[] encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(email),
    null,
    DataProtectionScope.CurrentUser);

// Para leer
byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
```

`DataProtectionScope.CurrentUser` cifra con la clave del perfil de Windows — nunca en texto plano.

---

## Fase 5 — Auditoría de RLS en Supabase
**Esfuerzo: Bajo | Impacto: Alto**

### 5.1 Verificar que todas las tablas tienen RLS habilitado

En el dashboard de Supabase → `Table Editor` → verificar que cada tabla tenga el candado cerrado (RLS habilitado):

| Tabla | RLS | Política mínima esperada |
|---|---|---|
| `usuarios` | ✓ | Solo lectura propia para `authenticated` |
| `productos` | ✓ | Lectura: `authenticated`, Escritura: rol con permiso |
| `empleados` | ✓ | Igual que usuarios |
| `audit_log` | ✓ | Solo INSERT para `authenticated`, SELECT para admin |
| `empresa` | ✓ | SELECT público, UPDATE solo admin |

Si alguna tabla tiene RLS deshabilitado → datos accesibles con solo la anon key.

---

## Fase 6 — Build y Distribución
**Esfuerzo: Bajo | Impacto: Medio**

### 6.1 Escaneo de vulnerabilidades en NuGet

Agregar al proceso de build (o como tarea manual periódica):

```bash
dotnet list package --vulnerable --include-transitive
```

Integrar en CI (GitHub Actions) como step bloqueante.

### 6.2 Firma del ejecutable

Para distribución interna, firmar el `.exe` con un certificado de Code Signing (puede ser auto-firmado para red interna):

```xml
<!-- CapaUI.csproj -->
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
  <SignAssembly>true</SignAssembly>
  <AssemblyOriginatorKeyFile>bimbo.pfx</AssemblyOriginatorKeyFile>
</PropertyGroup>
```

Esto previene que alguien distribuya una versión modificada del ejecutable en la red interna.

---

## Resumen — Prioridad de Implementación

| Fase | Tarea | Esfuerzo | Impacto | Prioridad |
|---|---|---|---|---|
| Fase | Tarea | Estado |
|---|---|---|
| 1.1 | Sacar anon key del repo | ✅ Completado |
| 1.2 | Verificar password policy Supabase | ⏳ Pendiente (config Supabase) |
| 2.1 | Serilog (reemplazar Console.WriteLine) | ✅ Completado |
| 2.2 | Tabla audit_log + IAuditoriaService | ⏳ Planificada |
| 3.1 | No exponer ex.Message en UI | ✅ Completado |
| 3.2 | Validar tipo/tamaño de logo | ✅ Completado |
| 4.1 | Timeout por inactividad | ⏳ Pendiente — junto con pantalla Mi Usuario |
| 4.2 | DPAPI para credenciales | ⏳ Baja prioridad |
| 5.1 | Auditar RLS en Supabase | ⏳ Pendiente (config Supabase) |
| 6.1 | Escaneo NuGet vulnerable | ⏳ Planificada |
| 6.2 | Firma del ejecutable | ⏳ Baja prioridad |

\* *Bajo impacto porque no hay funcionalidad de guardado de credenciales aún.*

---

## Qué daría realmente un 10/10

Un 10/10 no es solo "sin vulnerabilidades en el código". Es:

1. **Secretos fuera del repo** (Fase 1.1)
2. **Todo error logueado de forma persistente** (Fase 2.1)
3. **Rastro de auditoría en BD** (Fase 2.2) — para saber quién hizo qué y cuándo
4. **Mensajes de error sin detalles internos** (Fase 3.1)
5. **Validación de archivos subidos** (Fase 3.2)
6. **Timeout de sesión** (Fase 4.1)
7. **RLS auditado** (Fase 5.1)
8. **Password policy server-side** (Fase 1.2)
