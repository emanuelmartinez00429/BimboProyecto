---
title: "Sesión 2026-11-06 — Integración inicial de Velopack en CapaUI"
type: sesion
status: vigente
date: 2026-11-06
updated: 2026-11-06
autor: emanuel
agente: opencode
rama: pruebas-remote-update
tags: [sesion, velopack, despliegue, actualizaciones]
summary: "`CapaUI` ya tiene el punto de entrada que Velopack exige y una versión de producto declarada. La app compila sin errores y arranca igual que antes. Todavía **no** consulta ni aplica actualizaciones: esta sesión solo prepara el terreno."
---

# Sesión 2026-11-06 — Integración inicial de Velopack en CapaUI

> [!success] Resultado
> `CapaUI` ya tiene el punto de entrada que Velopack exige y una versión de producto declarada. La app compila sin errores y arranca igual que antes. Todavía **no** consulta ni aplica actualizaciones: esta sesión solo prepara el terreno.

Commit: `326e559` — `fase inicial de velopack + git releases`, rama `pruebas-remote-update`.

---

## Problema / motivo

El objetivo de producto es que el cliente instale la app una sola vez y reciba correcciones desde GitHub Releases sin volver a correr un instalador. Ver [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]] y [[Plan de CI-CD y Actualizaciones Remotas]].

Antes de escribir lógica de actualización había que resolver tres bloqueantes que no son de negocio sino de mecánica:

1. **WPF no expone un punto de entrada propio.** `CapaUI` dependía del `Main` que genera WPF desde `App.xaml`. Velopack necesita ejecutarse antes de que exista la `Application`.
2. **No existía versión de producto.** Ningún `.csproj` declaraba `<Version>`; `vpk pack` no tenía de dónde tomar el número.
3. **El directorio de salida de Velopack no estaba ignorado** por git.

## Cambios aplicados

### `CapaUI/CapaUI.csproj`

- `<PackageReference Include="Velopack" Version="1.2.161" />`.
- `<StartupObject>CapaUI.App</StartupObject>` — fuerza el `Main` propio como punto de entrada.
- `<ApplicationDefinition Remove="App.xaml" />` + `<Page Include="App.xaml" />` — `App.xaml` deja de generar un `Main` automático, pero conserva su `x:Class` y su `InitializeComponent()` para seguir mergeando `Resources/Styles.xaml`.
- `<Version>1.0.0</Version>` — fuente única de verdad. El SDK deriva `AssemblyVersion`, `FileVersion` e `InformationalVersion` de esa propiedad.

### `CapaUI/App.xaml.cs`

Se agregó `using Velopack;` y un `Main` explícito dentro de la clase `App`:

```csharp
[STAThread]
private static void Main(string[] args)
{
    VelopackApp.Build().Run();

    App app = new();
    app.InitializeComponent();
    app.Run();
}
```

El resto de la clase quedó intacto: lazy-init de `App.Services` (C13), `OnStartup`, Serilog, `MostrarLogin`.

### `.gitignore`

Se agregó `Releases/` con comentario, junto al ya existente `publish/`.

## Cómo se hizo (y por qué de cada decisión)

### Por qué `Main` propio y no `OnStartup`

`VelopackApp.Build().Run()` debe correr **antes de que exista la `Application`**. Velopack reutiliza el mismo ejecutable para instalar, crear accesos directos, desinstalar y aplicar actualizaciones: esos eventos llegan por línea de comandos y deben resolverse antes de levantar nada de WPF.

Si la llamada se hubiera puesto en `OnStartup`, el `Main` generado por WPF ya habría hecho `new App()` + `InitializeComponent()` — y `App.xaml` mergea `Resources/Styles.xaml`, que es un árbol de recursos considerable. Cada hook de instalación habría pagado ese costo para una operación que solo debía escribir un `.lnk`.

La doc oficial de Velopack pide exactamente esta receta (Main propio + `StartupObject` + `App.xaml` como `Page`), y advierte que `vpk pack` puede imprimir un falso "VelopackApp.Run() not found" cuando el análisis estático no lo detecta. Es esperado y se ignora.

### Por qué `AppId = BimboPesaje`

Es la identidad de máquina, no un nombre para humanos. Determina `%LOCALAPPDATA%\{packId}`, la entrada de desinstalación y el nombre de los paquetes (`BimboPesaje-1.0.0-full.nupkg`).

**Es irreversible en la práctica.** Si cambia entre versiones, Velopack no reconoce la instalación previa: la app vieja queda huérfana y el instalador nuevo se instala al lado. No hay migración automática.

Se eligió `BimboPesaje` —sin puntos ni prefijo de empresa— por coherencia con la carpeta que `App.xaml.cs` ya usa para logs: `%APPDATA%\BimboPesaje`. Un id `BimboHonduras.Pesaje` habría comprado desambiguación que este caso no necesita, al costo de dos nombres distintos para la misma app en la misma máquina.

Los nombres visibles al cliente se controlan aparte, con `--packTitle "Bimbo Pesaje Honduras"`.

### Por qué `--mainExe CapaUI.exe` es obligatorio

Velopack busca por defecto `{packId}.exe`. Aquí el ejecutable es `CapaUI.exe`, así que sin `--mainExe` el empaquetado no encuentra el binario principal: es el que arranca la app tras instalar y el que procesa los hooks.

### Por qué la firma de código quedó diferida

La app corre en PCs de una planta de Bimbo Honduras, sin certeza sobre dominio ni políticas de TI. La firma **no condiciona el funcionamiento del auto-update** (Velopack no la exige); lo que condiciona es la primera instalación, por SmartScreen y por antivirus corporativos.

Datos verificados en la sesión:

- **Azure Artifact Signing** (ex Trusted Signing, ~US$10/mes) **no cubre Honduras**: solo organizaciones de US, Canadá, UE y UK.
- Un certificado **OV** sí está disponible (US$150–350/año), pero desde junio 2023 la CA/Browser Forum exige que la clave privada viva en un módulo de hardware (token o HSM en la nube). Un `.pfx` suelto ya no es opción para certificados nuevos.
- **EV** ya no da bypass instantáneo de SmartScreen desde 2024.
- **Autofirmado + GPO** da cero avisos y costo cero, pero solo si TI de la planta distribuye la raíz.

Decisión: publicar `1.0.0` sin firmar y reevaluar con evidencia de fricción real.

## Verificación

```
dotnet build BimboProyecto.sln  →  0 errores
smoke test: CapaUI.exe arrancó y se mantuvo vivo 6 s (PID 17788), luego cerrado
```

El smoke test es la verificación que importa: con `Main` propio y `App.xaml` como `Page`, un error en el entry point impediría llegar al login. La app arrancó normal.

No hay tests de UI automatizados en el proyecto, y no se ejecutó la app instalada por Velopack porque el primer empaquetado todavía no se hizo.

### Warnings del build (preexistentes, no de esta sesión)

8 advertencias `NU1902`/`NU1903` por `System.Security.Cryptography.Pkcs 8.0.1`, transitivo de `CapaDatos`, con vulnerabilidad alta conocida (GHSA-846f-f764-76mx). Se confirmó con `git diff main -- CapaDatos/CapaDatos.csproj` que ese proyecto está intacto: el warning se destapó porque `dotnet add package` forzó un restore completo. Queda como deuda a registrar.

Los `MSB3026` observados durante el trabajo eran bloqueos del build server de MSBuild; desaparecieron con `dotnet build-server shutdown`.

## Lo que NO cambió

- No hay ninguna consulta de actualizaciones en runtime. `UpdateManager` no se instanció.
- No hay UI de aviso ("Actualización disponible") ni botón de reinicio.
- No se ejecutó `dotnet publish`, `vpk pack` ni `vpk upload github`.
- No se tocó `main`. Todo vive en `pruebas-remote-update`.
- No se modificó el arranque funcional: login, DI, Serilog y cierre de sesión siguen igual.
- `BimboPesaje` (proyecto histórico) sigue sin tocarse.

---

## Divergencias detectadas contra el plan vigente

> [!warning] Revisar antes de publicar el primer release

**1. El repositorio fuente es público.** [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]] decidió *mantener privado el repositorio de desarrollo* y usar un repositorio público separado solo para binarios. La verificación real de esta sesión devolvió `private: false`, `visibility: public` sobre `emanuelmartinez00429/BimboProyecto`, y la rama publica apuntando a ese mismo repositorio. O el ADR quedó desactualizado respecto de la realidad, o la decisión de publicar binarios se implementó sobre el repositorio de fuentes. Hay que resolverlo antes del primer `vpk upload github`.

**2. Instalación per-user vs. per-machine.** El plan previo fija ámbito *por máquina*, con privilegios a validar con IT. Velopack por defecto instala por usuario (`%LOCALAPPDATA%`), sin elevación. La diferencia es operativa: per-user implica que cada cuenta Windows de la estación recibe su propia copia y su propio ciclo de actualización.

**3. `--publish` es obligatorio.** `vpk upload github` crea un release en **draft** si no se pasa ese flag. Un draft no es visible para los clientes y `UpdateManager` no lo ve: el fallo más silencioso de todo el flujo.

**4. Validación de red pendiente.** El auto-update necesita salida HTTPS a `api.github.com`, `github.com` y `objects.githubusercontent.com`. Si el proxy de la planta las bloquea, la app arranca normal y nunca se actualiza. No se pudo probar en una máquina de la planta en esta sesión.

---

## Relaciones

- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]]
- [[Arquitectura Actual]]
- [[Deuda Técnica - Pendientes]]
