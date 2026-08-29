---
title: Sesión 2026-08-14 — Logo de empresa dinámico en el login
type: sesion
status: vigente
tags:
  - sesion
  - login
  - storage
  - cache
date: 2026-08-14
updated: 2026-08-14
summary: "El login ya no muestra un logo fijo empacado en el .exe: lee empresa.logoempresa, lo baja del bucket público empresa-logos la primera vez y lo cachea localmente.…"
scope:
  - CapaDatos/Repositorios
  - CapaUI/Core/Empresa
  - CapaUI/Formularios/InicioSesion
symbols:
  - ActualizarLogoAsync
  - Task<string>
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Sonnet 5 (Claude Code)
---

# Sesión 2026-08-14 — Logo de empresa dinámico en el login

> [!success] Resultado
> El login ya no muestra un logo fijo empacado en el `.exe`: lee `empresa.logo_empresa`, lo baja del bucket público `empresa-logos` la primera vez y lo cachea localmente. Build (`dotnet build CapaUI/CapaUI.csproj`) sin errores `CS*` — solo falló la copia final de DLLs por tener `CapaUI.exe`/Visual Studio abiertos bloqueando los archivos (`MSB3027`/`MSB3021`, no son errores de código).

---

## Problema / motivo

`LoginWindow.xaml` tenía el logo hardcodeado:

```xml
<Image Source="pack://application:,,,/CapaUI;component/Resources/bimbo_no_bg.png"/>
```

La tabla `empresa` ya tenía `logo_empresa` (ruta en Storage) y `RepositorioEmpresa.ActualizarLogoAsync` para reescribirla, pensados para un módulo de configuración que todavía no se construyó. El pedido: que el login muestre ese logo, cacheado localmente, y que un futuro cambio de logo (hecho por ese módulo, cuando exista) se refleje solo, sin construir ya la parte administrativa.

---

## Diseño

La decisión de fondo (por qué el nombre de archivo alcanza como clave de versión, y qué alternativas se descartaron) quedó en [[ADR-016 - Logo de empresa dinamico en login con cache por nombre de archivo]]. Acá el resumen de la implementación.

### 1. Descarga — `CapaDatos`

`RepositorioEmpresa.cs` — nuevo `DescargarLogoAsync(rutaStorage, rutaLocalDestino)`:

```csharp
private const string BucketLogos = "empresa-logos";

public static async Task DescargarLogoAsync(string rutaStorage, string rutaLocalDestino)
{
    var client = await ConexionSupabase.GetClientAsync();
    var descarga = client.Storage.From(BucketLogos)
        .DownloadPublicFile(rutaStorage, rutaLocalDestino, null, null);
    var timeout = Task.Delay(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));

    var terminó = await Task.WhenAny(descarga, timeout);
    if (terminó == timeout)
        throw new TimeoutException($"Descarga del logo '{rutaStorage}' superó {ConexionSupabase.TimeoutSeconds}s.");

    await descarga; // re-lanza si la descarga falló
}
```

`Supabase.Storage` no acepta `CancellationToken` en `Download*` (a diferencia de las consultas Postgrest del resto del repositorio) — el timeout se implementa carrereando contra `Task.Delay`, no cancelando la operación de verdad.

### 2. Caché local — `CapaUI/Core/Empresa/LogoEmpresaCache.cs` (nuevo)

`%APPDATA%\BimboPesaje\LogoEmpresa\<nombre-de-archivo>` — mismo patrón de carpeta que ya usa `App.xaml.cs` para logs. `ObtenerRutaLocalAsync(rutaStorage)`:

1. `Path.GetFileName(rutaStorage)` → clave de caché.
2. Archivo local existe → se devuelve sin red.
3. No existe → se descarga a un `.tmp`, se renombra (`File.Move(overwrite: true)`).
4. Se borran del directorio los archivos con otro nombre (versión vieja) — mejor esfuerzo, con `try/catch` mudo.
5. Cualquier falla de descarga se loguea con `Serilog.Log.Warning` y devuelve `null` — nunca propaga.

### 3. Enganche — `LoginWindow.xaml.cs`

`LoginWindow_Loaded` ya leía `RepositorioEmpresa.ObtenerAsync()` para `dominio_correo`; se agregó, después de eso, `AplicarLogoEmpresaAsync(empresa)`:

```csharp
private async Task AplicarLogoEmpresaAsync(Empresa? empresa)
{
    var rutaLocal = await LogoEmpresaCache.ObtenerRutaLocalAsync(empresa?.LogoEmpresa);
    if (rutaLocal is null) return; // se queda con el logo empacado del XAML

    var bitmap = new BitmapImage();
    bitmap.BeginInit();
    bitmap.CacheOption = BitmapCacheOption.OnLoad; // suelta el archivo tras cargar
    bitmap.UriSource = new Uri(rutaLocal, UriKind.Absolute);
    bitmap.EndInit();
    bitmap.Freeze();

    LogoImage.Source = bitmap;
}
```

`CacheOption = OnLoad` es necesario para no dejar el archivo bloqueado — si un futuro módulo de configuración quisiera sobrescribirlo (mismo nombre) con la app corriendo, un handle abierto lo impediría.

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores `CS*`**. Falló la copia de DLLs (`CapaDominio.dll`, `CapaAplicacion.dll`, `CapaDatos.dll`) por `CapaUI.exe`/Visual Studio corriendo — no es un error de compilación, hace falta cerrarlos para ver el build limpio de punta a punta.
- La API de `Supabase.Storage` (`DownloadPublicFile(string, string, TransformOptions?, EventHandler<float>?)` → `Task<string>`) se confirmó por reflexión sobre el `.dll` instalado (`Supabase.Storage 2.0.2`), no solo por el XML de documentación.
- **Sin verificar en runtime:** que el bucket `empresa-logos` sea efectivamente público (se asumió — ver P-035) y que la descarga funcione contra el proyecto Supabase real con un `logo_empresa` cargado.

---

## Lo que NO se hizo

- **No existe el módulo de configuración** que sube el logo y reescribe `empresa.logo_empresa` — esta sesión resuelve solo el lado "consumo" (login lee y cachea). El lado "administración" (pantalla para subir el logo, validar que sea imagen, llamar a `ActualizarLogoAsync`) sigue sin construirse.
- **No se verificó la política RLS/pública del bucket `empresa-logos`** contra el proyecto Supabase real — se asumió pública por analogía con `empresa` (P-035).
- **No se limpió `nuevaRuta` con nombre distinto obligatorio** en `ActualizarLogoAsync` — la caché de esta sesión asume que cada logo nuevo llega con un nombre de archivo distinto; si el futuro módulo de configuración reutiliza el mismo nombre al reemplazar el logo, el caché local queda desactualizado (ver alternativas descartadas en el ADR).

## Estado en git

- Sin commitear al cierre de esta nota. Archivos tocados: `CapaDatos/Repositorios/RepositorioEmpresa.cs`, `CapaUI/Core/Empresa/LogoEmpresaCache.cs` (nuevo), `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs`.

---

## Relaciones

- [[ADR-016 - Logo de empresa dinamico en login con cache por nombre de archivo]] — decisión de fondo
- [[ADR-004 - GhostTextBox Autocompletado de Dominio en Login]] — mismo punto de entrada (`LoginWindow_Loaded`), mismo `RepositorioEmpresa.ObtenerAsync()`
- [[Deuda Técnica - Pendientes]] — P-035
- [[Arquitectura Actual]]
