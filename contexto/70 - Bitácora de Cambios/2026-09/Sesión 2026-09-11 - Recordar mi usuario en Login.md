---
title: "Sesión 2026-09-11 — Recordar mi usuario en Login (Persistencia Local Aislada)"
date: 2026-09-11
tags:
  - bitacora
  - sesion
  - wpf
  - auth
  - seguridad
  - clean-architecture
  - local-storage
aliases:
  - Recordar mi usuario
  - Persistencia de credenciales login
---

# Sesión 2026-09-11 — Recordar mi usuario en Login (Persistencia Local Aislada)

## Resumen

Se implementó la funcionalidad de **"Recordar mi usuario"** en la pantalla de inicio de sesión. La solución persiste exclusivamente el correo electrónico (nunca contraseñas ni tokens) de manera 100% local a la máquina del operador (`%APPDATA%\BimboPesaje\Preferencias\ultimo_usuario.txt`), sin llamadas de red ni tocar Supabase. Diseñada bajo Clean Architecture y con la directiva de seguridad para terminales compartidas de planta.

## Contexto y Motivación de Seguridad

1. **Terminales compartidas en planta (patrón de riesgo de [[Deuda Técnica - Pendientes#P-048|P-048]]):** en básculas y terminales de planta, varios operarios comparten la misma sesión de Windows a lo largo de distintos turnos. Si el checkbox viniera pre-tildado por defecto, un operario heredaría sin querer el correo del turno anterior. Por eso **el checkbox arranca destildado (`false`) por default**, salvo que ya exista una preferencia guardada deliberadamente.
2. **Exclusión total de contraseñas:** nunca se persiste la contraseña ni ningún dato confidencial en disco.
3. **Persistencia local pura:** mismo patrón ya probado en `IconoSidebarCache.cs` — `Environment.SpecialFolder.ApplicationData`, ruta `%APPDATA%\BimboPesaje\Preferencias\ultimo_usuario.txt`. Nada de Registro de Windows, `Properties.Settings` ni bases remotas.

## Arquitectura y Decisiones Técnicas

Para preservar la regla de oro #2 de `AGENTS.md` (`CapaAplicacion` nunca referencia `CapaDatos`) y que `LoginViewModel` siga testeable en un proyecto `net10.0` sin WPF:

### 1. Contrato desacoplado (`CapaAplicacion.Auth.Interfaces`)

```csharp
public interface IPreferenciasInicioSesionService
{
    string? ObtenerUltimoUsuario();
    void GuardarUltimoUsuario(string? email);
}
```

Sin dependencias de WPF ni asincronía — es I/O local instantáneo.

### 2. Implementación (`CapaDatos.Preferencias.PreferenciasInicioSesionService`)

- `ObtenerUltimoUsuario()`: comprueba si existe `ultimo_usuario.txt`, lo lee en UTF-8 bajo `try/catch` silencioso y retorna el correo, o `null` si está vacío o falla la lectura.
- `GuardarUltimoUsuario(string? email)`: si `email` es nulo o vacío, borra el archivo si existía (limpia el recuerdo). Si trae un correo, crea el directorio y lo escribe en UTF-8. `try/catch` silencioso para no bloquear el login ante un problema de disco o permisos.

### 3. Registro en DI (`CapaDatos.DependencyInjection`)

```csharp
services.AddSingleton<IPreferenciasInicioSesionService, PreferenciasInicioSesionService>();
```

### 4. `LoginViewModel`

- Recibe `IPreferenciasInicioSesionService? preferencias = null` en el constructor (opcional, para no romper los tests viejos de 2 argumentos — en producción `LoginWindow.xaml.cs` siempre pasa la instancia real).
- Nueva propiedad observable `RecordarUsuario` (`bool`, default `false`).
- En el constructor: si `preferencias.ObtenerUltimoUsuario()` devuelve un correo, precarga `Email` y activa `RecordarUsuario = true`. Si no hay nada guardado, `Email` queda vacío y `RecordarUsuario` en `false`.
- Al terminar un ingreso exitoso en `IngresarAsync`, justo antes de retornar `Exitoso`:
  ```csharp
  _preferencias?.GuardarUltimoUsuario(RecordarUsuario ? Email : null);
  ```

### 5. Vista (`LoginWindow.xaml` / `LoginWindow.xaml.cs`)

```xml
<CheckBox Grid.Column="0" Content="Recordar mi usuario"
          IsChecked="{Binding RecordarUsuario, Mode=TwoWay}"
          Style="{StaticResource CustomCheckBox}"/>
```

- El constructor de `LoginWindow` recibe `IPreferenciasInicioSesionService` por DI y se lo pasa a `LoginViewModel`.
- Se agregó `DataContext = _vm;` (antes no estaba seteado — el checkbox es el único `{Binding}` de todo el archivo, así que no afecta nada más).
- Si `_vm.Email` viene precargado, se copia a `TxtEmail.Text` (el `GhostTextBox` no está bindeado, se maneja por code-behind igual que la contraseña) y en `LoginWindow_Loaded` el foco salta directo a `TxtPassword`.

### 6. Pruebas unitarias (`BimboProyecto.Tests`)

Doble de prueba `PreferenciasFalsas : IPreferenciasInicioSesionService`, y suite nueva `P1_PreferenciasRecordarUsuario` con 4 casos:

1. Login exitoso con `RecordarUsuario = true` guarda el correo.
2. Login exitoso con `RecordarUsuario = false` guarda `null` y limpia lo que hubiera.
3. Constructor con usuario previo precarga `Email` y tilda el checkbox.
4. Constructor sin usuario previo arranca vacío y destildado.

## Verificación

- **Compilación:** `dotnet build BimboProyecto.sln -m:1` → **0 Advertencias, 0 Errores**.
- **Pruebas:** `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` → **423/423 (100%)**.
- Verificado de forma independiente por Claude el 2026-09-11: mismos resultados, más revisión de que `LoginWindow.xaml.cs` sí pasa la instancia real del servicio (no depende del `null` default) y que `DataContext = _vm` no afecta ningún otro binding del archivo.

## Relaciones

- [[Deuda Técnica - Pendientes]] — P-048, patrón de riesgo de terminal compartida
- [[Auditoría Externa — Optimizaciones WPF de Antigravity vs. Investigaciones QA]]
- [[Arquitectura Actual]]
