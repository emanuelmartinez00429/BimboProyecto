---
title: WPF — DPI Awareness y Escalado Multi-Resolución
type: referencia
status: vigente
tags:
  - wpf
  - dpi
  - escalado
  - rendering
  - referencia
date: 2026-06-21
updated: 2026-06-21
summary: "Que la app se vea nítida en cualquier resolución (1366×768 → 4K) y escala de Windows (100%, 125%, 150%, 200%), incluso al mover la ventana entre monitores con…"
scope: []
symbols:
  - ApplicationHighDpiMode
  - LoginWindow
  - MainWindow
  - MaxHeight
  - MinHeight
  - MinWidth
  - ModalContent
  - ModalOverlay
  - Path
  - TextOptions
---

# WPF — DPI Awareness y Escalado Multi-Resolución

> [!abstract] Objetivo
> Que la app se vea nítida en cualquier resolución (1366×768 → 4K) y escala de Windows (100%, 125%, 150%, 200%), incluso al mover la ventana entre monitores con distinto DPI.

Investigación validada contra fuentes de Microsoft Learn (2026-06-21) e implementada en las 5 fases descritas abajo. Complementa a [[WPF - Texto Dentado en Fondos de Color]].

---

## Conceptos clave

WPF trabaja en **unidades independientes de dispositivo** (1 DIP = 1/96 pulgada). El factor de escala = DPI/96:

| Escala Windows | DPI | Factor |
|---|---|---|
| 100% | 96 | 1.0× |
| 125% | 120 | 1.25× |
| 150% | 144 | 1.5× |
| 200% | 192 | 2.0× |

### Niveles de DPI awareness

| Modo | Comportamiento |
|---|---|
| **System Aware** (default de WPF) | Lee el DPI del monitor primario **una sola vez al arrancar**. |
| **Per-Monitor V2** (objetivo) | Detecta el DPI de cada monitor y **re-renderiza** al cambiar de monitor o de escala. |

> [!danger] El problema de System Aware
> Cita de Microsoft Learn: *"WPF applications are by default system DPI-aware... the application will be scaled by the OS when the application is moved to a monitor with a different DPI... Scaling in the OS may result in WPF applications to appear blurry especially when the scaling is non-integral."*
>
> Es decir: en modo System Aware, al arrastrar la ventana a un monitor con distinto DPI, **Windows estira el bitmap → todo borroso**.

---

## ⚠️ La trampa: `ApplicationHighDpiMode` NO aplica a WPF

El `.csproj` tenía:
```xml
<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
```

**Esto era código muerto.** La documentación oficial de MSBuild lista `ApplicationHighDpiMode` bajo **"Windows Forms settings"** y dice que controla el argumento de `Application.SetHighDpiMode()` emitido por `ApplicationConfiguration.Initialize()` — una API de arranque **exclusiva de WinForms**. Un proyecto WPF puro (`UseWpf=true`, sin `UseWindowsForms`) nunca genera ni llama esa API, así que la propiedad no hace nada.

> [!tip] Mecanismo correcto para WPF
> DPI awareness en WPF se declara vía **`app.manifest`**, no por csproj property. Y en **.NET Core 3.0+ (incluye .NET 8), basta el manifest** — WPF maneja el re-render automáticamente para contenido WPF puro, sin código nativo ni helper.
> Fuente: [WPF-Samples/PerMonitorDPI](https://github.com/microsoft/WPF-Samples/blob/main/PerMonitorDPI/readme.md)

---

## Las 5 fases implementadas (2026-06-21)

### Fase 1 — `app.manifest` con Per-Monitor V2
**Archivo nuevo:** `CapaUI/app.manifest`
```xml
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, PerMonitor</dpiAwareness>
  </windowsSettings>
</application>
```
**csproj:** se reemplazó `<ApplicationHighDpiMode>` (no-op) por `<ApplicationManifest>app.manifest</ApplicationManifest>`.

### Fase 2 — Anti-dentado global
Las 3 propiedades de rendering se extendieron de los 6 modales a: **6 Views**, `MainWindow`, `LoginWindow`. Los paneles Forgot **heredan** `TextOptions` + `UseLayoutRounding` de `LoginWindow` (son propiedades heredables).
```xml
UseLayoutRounding="True"
TextOptions.TextRenderingMode="Grayscale"
TextOptions.TextFormattingMode="Display"
```
Ver detalle de cada propiedad en [[WPF - Texto Dentado en Fondos de Color]].

### Fase 3 — Modales escalables en pantallas pequeñas
En los 6 modales:
- `MaxHeight` del Border raíz atado al `ModalOverlay` que llena la vista:
  ```xml
  MaxHeight="{Binding ActualHeight, RelativeSource={RelativeSource AncestorType=Border}}"
  ```
  (El `ModalContent` ContentControl está centrado, no estirado, por eso se ata al overlay y no al ContentControl.)
- Contenido envuelto en `<ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">`.

Resultado: si el modal supera el alto de la vista (pantalla baja + escala alta), aparece scroll y el botón Guardar siempre es alcanzable.

> [!warning] Cuidado al replicar
> 3 modales (Fabricante/Proveedor/Categoria) usan formato XAML compacto **sin línea en blanco** entre `</Border>` (degradado) y `<StackPanel>` (contenido). Los otros 3 sí la tienen. Al insertar el `<ScrollViewer>` hay que respetar ese detalle o el anclaje falla.

### Fase 4 — Ventana mínima realista
`MainWindow`: `MinWidth` 590→**960**, `MinHeight` 500→**600**. Evita que el layout sidebar+contenido colapse al achicar la ventana.

### Fase 5 — Imágenes nítidas (ya satisfecha)
Auditados todos los `<Image>`: todos ya tenían `RenderOptions.BitmapScalingMode="HighQuality"`. Los íconos de UI son `Path` vectoriales → escalan perfecto a cualquier DPI.

---

## Caso práctico — comportamiento por escenario

| Escenario | Antes (System Aware) | Después (Per-Monitor V2) |
|---|---|---|
| Monitor único 1920×1080 @ 100% | ✅ Nítido | ✅ Nítido |
| Laptop único 1920×1080 @ 150% | ⚠️ Views con "cerrucho" en texto sobre color | ✅ Nítido (Fase 2) |
| Laptop 150% + externo 100%, arrastrando | ❌ **Borroso** (bitmap stretch) | ✅ Nítido, re-render nativo (Fase 1) |
| Laptop 1366×768 @ 150% | ❌ Modal corta el botón Guardar | ✅ Modal con scroll (Fase 3) |
| Monitor 4K @ 200% | ⚠️ Logos PNG suaves si faltara HighQuality | ✅ Nítido (Fase 5 + íconos vectoriales) |

---

## Matriz de prueba recomendada

Probar: **100% / 125% / 150% / 200%** × **1366×768 / 1920×1080 / 3840×2160** × **monitor único + doble con DPI mixto**. La validación clave es arrastrar la ventana entre dos monitores de distinto DPI y confirmar que NO se ve borroso.

---

## Relaciones

- [[WPF - Texto Dentado en Fondos de Color]] — detalle de las 3 propiedades de rendering
- [[Arquitectura Actual]] — estado del proyecto
- [[Módulo Contactos (Drill-down)]] — modales donde empezó el trabajo de rendering

## Fuentes

- [Developing a Per-Monitor DPI-Aware WPF Application — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/hidpi/declaring-managed-apps-dpi-aware)
- [MSBuild properties for Microsoft.NET.Sdk.Desktop — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop)
- [WPF-Samples / PerMonitorDPI — GitHub (Microsoft)](https://github.com/microsoft/WPF-Samples/blob/main/PerMonitorDPI/readme.md)
- [FrameworkElement.UseLayoutRounding — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.uselayoutrounding)
