---
title: "Sesión 2026-06-21 — Soporte Multi-Resolución y DPI Per-Monitor V2"
tags:
  - sesion
  - wpf
  - dpi
  - escalado
  - rendering
date: 2026-06-21
branch: feat/fase6-IntegracionWpf/MenuPrincipal
---

# Sesión 2026-06-21 — Soporte Multi-Resolución y DPI Per-Monitor V2

## Resumen

Investigación (Microsoft Learn) + implementación de 5 fases para que la app se vea nítida en cualquier resolución y escala de Windows, incluso entre monitores con distinto DPI. Build final: **0 errores**.

Documento de referencia: [[WPF - DPI Awareness y Escalado Multi-Resolución]].

---

## Hallazgo clave

`CapaUI.csproj` tenía `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>`, que parecía configurar DPI pero **es no-op en WPF** — es una propiedad de WinForms (controla `Application.SetHighDpiMode` vía `ApplicationConfiguration.Initialize()`, que WPF nunca genera). La app corría en el default de WPF: **System Aware** → borrosa al cambiar de monitor.

---

## Archivos creados

| Archivo | Descripción |
|---|---|
| `CapaUI/app.manifest` | Declara `dpiAwareness = PerMonitorV2, PerMonitor` + `supportedOS` Win10/11 |

## Archivos modificados

| Archivo | Cambio | Fase |
|---|---|---|
| `CapaUI/CapaUI.csproj` | `<ApplicationHighDpiMode>` (no-op) → `<ApplicationManifest>app.manifest</ApplicationManifest>` | 1 |
| 6 Views (Productos, Proveedores, Fabricantes, Categorías, ContactosFabricantes, ContactosProveedores) | + `UseLayoutRounding` + `TextRenderingMode=Grayscale` + `TextFormattingMode=Display` | 2 |
| `MainWindow.xaml` | + 3 props rendering; `MinWidth` 590→960, `MinHeight` 500→600 | 2 + 4 |
| `LoginWindow.xaml` | + 3 props rendering (los paneles Forgot las heredan) | 2 |
| 6 Modales | `MaxHeight` atado al overlay + contenido en `ScrollViewer` | 3 |
| `FabricanteModal.xaml`, `CategoriaModal.xaml` | Fix inconsistencia de fuente inline (13.5→15) heredada de sesión previa | — |

---

## Decisiones técnicas

1. **Manifest, no csproj.** DPI awareness en WPF se declara vía `app.manifest`. En .NET 8 basta el manifest — WPF re-renderiza solo (sin helper nativo) porque la app es WPF puro (sin `HwndHost`/`WindowsFormsHost` tras eliminar BimboPesaje).
2. **`PerMonitorV2, PerMonitor`** como valor: V2 en Win10 1703+, fallback a PerMonitor en versiones anteriores.
3. **`MaxHeight` atado al `ModalOverlay`**, no al `ModalContent`: el ContentControl está centrado (sizea a contenido), el overlay llena la vista. Se usa `RelativeSource AncestorType=Border` que resuelve al overlay.
4. **Props de rendering heredables:** `TextOptions.*` y `UseLayoutRounding` se heredan, por eso ponerlas en `LoginWindow`/`MainWindow` cubre a los hijos (paneles Forgot, SuggestionSearchBox, etc.).

---

## Bug corregido durante la sesión

3 modales en formato XAML compacto (Fabricante/Proveedor/Categoria) no tienen línea en blanco entre `</Border>` y `<StackPanel>`. El anclaje de apertura del `ScrollViewer` falló y los dejó con `</ScrollViewer>` huérfano. Se detectó (antes de compilar) y se corrigió con el anclaje sin línea en blanco.

---

## Verificación

`dotnet build CapaUI.csproj` → **0 errores**, 41 advertencias (CS8618/CS8603 nullable preexistentes en CapaDatos, no relacionadas).

Pendiente: prueba visual en matriz de resoluciones/escalas (ver [[WPF - DPI Awareness y Escalado Multi-Resolución]]).

---

## Relaciones

- [[WPF - DPI Awareness y Escalado Multi-Resolución]] — referencia técnica completa
- [[WPF - Texto Dentado en Fondos de Color]] — origen del trabajo de rendering
- [[Arquitectura Actual]]
