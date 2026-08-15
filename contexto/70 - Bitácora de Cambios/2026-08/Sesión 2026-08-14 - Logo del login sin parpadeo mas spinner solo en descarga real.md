---
title: "Sesión 2026-08-14 — Logo del login sin parpadeo, spinner solo cuando hay descarga real"
tags:
  - sesion
  - login
  - ui
  - wpf
  - cache
date: 2026-08-14
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Sonnet 5 (Claude Code)
---

# Sesión 2026-08-14 — Logo del login sin parpadeo, spinner solo cuando hay descarga real

> [!success] Resultado
> El logo del login ya no salta de "empacado" a "real" en cada apertura. Se pinta el cacheado al instante, sin esperar ningún viaje de red, y solo se reemplaza (con un spinner chico mientras dura) cuando de verdad hace falta descargar — primera vez en la máquina, o el logo cambió en la base. Build: `0 errores CS`.

---

## Problema reportado

Después de implementar [[Sesión 2026-08-14 - Logo de empresa dinamico en login]] (que ya cacheaba bien — se verificó con el archivo real en disco, `%APPDATA%\BimboPesaje\LogoEmpresa\logo_empresa_1.png`, 1.06 MB, persistente entre corridas), el usuario reportó: *"aparte hay dos logos, está el logo de Bimbo, el otro, y después carga el otro logo, se ve raro, quita el logo anterior y deja que este cargue"*.

No era un problema de caché (el archivo ya estaba ahí y no se re-descargaba) — era un problema de **secuencia visual**. `LoginWindow_Loaded` hacía `await RepositorioEmpresa.ObtenerAsync()` (necesario para `dominio_correo`) **antes** de aplicar el logo, así que la ventana siempre abría con el logo empacado por defecto, esperaba esa vuelta de red (ajena a la imagen), y recién ahí saltaba al logo real — cacheado o no. Ese salto, disparado por una espera de red que no tenía nada que ver con la imagen, era lo que se veía como "está descargando cada vez".

---

## Diseño

Tres cambios, en orden de prioridad (el primero elimina el 100% de los casos normales sin necesitar ningún indicador de carga):

### 1. Mostrar lo cacheado antes de cualquier `await`

`LogoEmpresaCache.ObtenerRutaCacheadaSinRed()` (nuevo) — lee la carpeta de caché directo, sin tocar la base ni la red. Como `LimpiarVersionesViejas` ya garantiza que nunca queda más de un archivo real ahí, alcanza con tomar el primero. `LoginWindow_Loaded` lo llama como la primera línea del método, antes del `try`/`await`:

```csharp
var rutaYaMostrada = LogoEmpresaCache.ObtenerRutaCacheadaSinRed();
if (rutaYaMostrada is not null) AplicarImagenLogo(rutaYaMostrada);
```

Con esto, en cualquier máquina que ya tenga algo cacheado (o sea, casi siempre), el logo correcto está puesto antes de que la ventana termine de dibujarse. Cero parpadeo, cero red de por medio.

### 2. No re-aplicar la imagen si ya es la vigente

`AplicarLogoEmpresaAsync` ahora recibe `rutaYaMostrada` y compara el nombre de archivo contra el `empresa.logo_empresa` que trae la consulta que sí hace falta (`dominio_correo`). Si coinciden, no hace nada — ni siquiera reasigna `LogoImage.Source` con el mismo bitmap.

### 3. Spinner chico solo en el resto: primera vez, o el logo cambió de verdad

Si no había nada cacheado, o el nombre cambió, ahí sí puede haber una descarga real — y solo en ese caso se muestra un spinner. **No se usó el shimmer con barrido** que documenta [[WPF - Esqueleto con Shimmer (Skeleton Loading)]] — esa nota registra que se implementó completo en Roles y Productos el 2026-08-11 y **se revirtió el mismo día** por artefactos visuales y lentitud en la máquina del usuario. Se preguntó explícitamente antes de elegir, y se optó por el patrón **ya vigente y probado** en el resto de la app: spinner giratorio, `BeginAnimation`/`Stop` desde code-behind, `Visibility` nunca bindeada en XAML (esa fue justamente la causa raíz sospechada del problema del shimmer: dos capas con visibilidad bindeada que quedaban visibles a la vez mientras el `DataContext` era null).

El spinner del logo (`LogoSpinner`/`LogoSpinnerRotate` en el XAML) es una copia a escala del que ya usa el panel "Iniciando sesión" más abajo en el mismo archivo (`SpinnerRotate`) — mismo arco, mismo `DoubleAnimation(0, 360, 1s, RepeatBehavior.Forever)`, mismo mecanismo de arranque/parada.

---

## Archivos modificados

- `CapaUI/Core/Empresa/LogoEmpresaCache.cs` — `ObtenerRutaCacheadaSinRed()` (nuevo).
- `CapaUI/Formularios/InicioSesion/LoginWindow.xaml` — el `Image` del logo pasó a vivir dentro de un `Grid` (para poder superponer el spinner), y se agregó `LogoSpinner`/`LogoSpinnerRotate` (`Collapsed` por defecto).
- `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs` — `LoginWindow_Loaded` pinta el caché primero; `AplicarLogoEmpresaAsync` ahora compara contra lo ya mostrado y solo descarga/anima si hace falta; `AplicarImagenLogo`, `IniciarLogoSpinner`, `DetenerLogoSpinner` (nuevos, extraídos).

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → **0 errores `CS*`** (falló solo la copia de DLL por `CapaUI.exe` corriendo — mismo caso recurrente del día).
- **Pendiente en runtime**: cerrar la app, recompilar, abrir el login y confirmar que el logo aparece de una sola vez, sin salto — y que solo se ve el spinner chico si se borra a mano la carpeta de caché (simula "primera vez").

## Estado en git

Sin commitear al cierre de esta nota — pendiente confirmación del usuario.

---

## Relaciones

- [[Sesión 2026-08-14 - Logo de empresa dinamico en login]] — implementación original del caché, que ya funcionaba bien; esta sesión corrige la secuencia visual, no el caché en sí
- [[WPF - Esqueleto con Shimmer (Skeleton Loading)]] — por qué NO se usó shimmer acá
- [[ADR-016 - Logo de empresa dinamico en login con cache por nombre de archivo]]
