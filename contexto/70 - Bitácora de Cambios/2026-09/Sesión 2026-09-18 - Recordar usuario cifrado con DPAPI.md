---
title: "Sesión 2026-09-18 — «Recordar usuario» cifrado con DPAPI en LocalAppData"
tags:
  - sesion
  - seguridad
  - auth
  - local-storage
date: 2026-09-18
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente) con Fernando
---

# Sesión 2026-09-18 — «Recordar usuario» cifrado con DPAPI en LocalAppData

> [!success] Resultado
> El correo de «Recordar usuario», que **también es el usuario de login**, ya no se guarda en texto plano. Ahora queda cifrado con DPAPI (`ProtectedData`, `CurrentUser`) en `%LOCALAPPDATA%\BimboPesaje\Preferencias\ultimo_usuario.bin`. El `.txt` viejo de `%APPDATA%` se migra una vez y se borra.

---

## Problema / motivo

Fernando preguntó si guardar el correo en una carpeta local era una vulnerabilidad. Evaluación:

- **No es crítica.** En diseño de seguridad el usuario no es un secreto:
  - la contraseña y la sesión **no** se escriben en disco;
  - el login responde «Credenciales incorrectas» sin distinguir entre un usuario inexistente y una contraseña mala;
  - la recuperación de contraseña no enumera cuentas.
- **Sí conviene endurecerla, porque el correo es el usuario.** Quien lo ve tiene la mitad de las credenciales, lo que permite un ataque dirigido.
  - El archivo estaba en texto plano.
  - Estaba en `%APPDATA%` (Roaming), que en un dominio se sincroniza a otros equipos.

Era el punto 4.2 de [[Plan de Seguridad - Roadmap 10-10]].

## Cambios aplicados

- **`CapaDatos/Preferencias/PreferenciasInicioSesionService.cs`:**
  - `ProtectedData.Protect`/`Unprotect` con `DataProtectionScope.CurrentUser` y entropía `"BimboPesaje.UltimoUsuario.v1"`, que ata el blob a este uso. No hay ninguna clave en el código.
  - Ruta nueva en `LocalApplicationData` → `ultimo_usuario.bin`.
  - **Migración:** si existe el `.txt` viejo en Roaming, se cifra, se guarda y se borra. Quien ya tenía «Recordar usuario» no lo pierde.
  - «Olvidar» borra el blob **y** el `.txt` viejo.
  - Un blob que no se puede descifrar (copiado de otra cuenta o equipo, o corrupto) se descarta y el login arranca vacío.
  - La escritura es atómica: archivo temporal y después `File.Move(..., overwrite: true)`.
  - Guarda `OperatingSystem.IsWindows()`: `CapaDatos` compila para `net10.0` (no `-windows`) y sin esa guarda el analizador CA1416 rompe la regla de 0 advertencias.
  - Hay un constructor `internal` con rutas explícitas para los tests.
- **`CapaDatos/CapaDatos.csproj`:**
  - `PackageReference System.Security.Cryptography.ProtectedData 10.0.5`. Antes llegaba de forma indirecta vía `ConfigurationManager`; ahora queda declarado.
  - `InternalsVisibleTo BimboProyecto.Tests`.
- **`BimboProyecto.Tests/Auth/PreferenciasInicioSesionServiceTests.cs`** (7 tests, en una carpeta temporal, nunca en el perfil real):
  - al leer vuelve el mismo correo;
  - el archivo no contiene el correo en claro (ni UTF-8 ni UTF-16);
  - «olvidar» borra;
  - sin archivo devuelve `null`;
  - migra el `.txt` y lo borra;
  - «olvidar» también borra el `.txt`;
  - un blob indescifrable se descarta.

## Verificación

- `dotnet test` → **572 en verde**, incluidos los 7 nuevos y los de `LoginViewModelTests`.
- `CapaUI` compilado en una carpeta de salida aparte → **0 advertencias**. La app estaba abierta y bloqueaba los DLL de `bin`.
- Prueba en la app real, aprobada por Fernando.

## Lo que NO cambió y límite conocido

- El contrato `IPreferenciasInicioSesionService` y `LoginViewModel` no cambian.
- **DPAPI no protege contra quien usa la misma sesión de Windows:** la app lo descifra y lo muestra prellenado. Protege el archivo copiado a otro equipo o leído desde otra cuenta. En las **terminales compartidas del andén** la medida real es no marcar «Recordar usuario». Queda como opción futura una configuración por equipo que lo desactive.
- Sigue pendiente revisar en Supabase (Authentication → Rate Limits y política de contraseñas) que conocer el usuario no alcance para forzar la cuenta.

---

## Relaciones

- [[Sesión 2026-09-11 - Recordar mi usuario en Login]] — la implementación original en texto plano
- [[Plan de Seguridad - Roadmap 10-10]] — punto 4.2, ahora hecho
- [[Arquitectura Actual]]
