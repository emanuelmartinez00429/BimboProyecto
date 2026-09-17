---
title: "Sesión 2026-09-17 — Remediación Multi-DPI en WindowChrome, animación en Login y BitmapHelper"
tags:
  - sesion
  - wpf
  - dpi
  - win32
  - chrome
  - dwm
  - owasp
  - arquitectura
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente) — Sesión Fernando
revisor: DeepInvestigator QA
---

# Sesión 2026-09-17 — Remediación Multi-DPI en WindowChrome, animación en Login y BitmapHelper

> [!danger] PARCIALMENTE REVERTIDO 2026-09-17 — el punto 1 rompió el maximizado
> El cambio de `MainWindow` descrito en §1.1 (retirar `Margin="6"` de `RootBorder` y compensar con `GetSystemMetricsForDpi` en `WM_GETMINMAXINFO`) **se revirtió el mismo día**: restar el marco a `ptMaxSize` achicaba la ventana maximizada ~16px y dejaba ver el escritorio alrededor; el hueco superior además disparaba la barra clásica de Win32 (`_ 🗖 X`) sobre el chrome propio.
>
> El `Margin="6"` no era un antipatrón prescindible: es la compensación obligatoria del overscan del marco nativo bajo `WindowStyle="SingleBorderWindow"` + `WindowChrome`, aplicada en la capa del contenido. Estado vigente: `Margin="6"` al maximizar **+** `ptMaxPosition`/`ptMaxSize` fijados a `rcWork` sin restas.
>
> **Sigue vigente** el resto de la sesión: migración de `LoginWindow`, extracción de `BitmapHelper` y las directrices OWASP.
>
> Ver [[Sesión 2026-09-17 - Reversión de regresión en maximizado de MainWindow]] y el callout en [[Auditoría Técnica — Animaciones DWM, Maximizado Multi-DPI y Seguridad OWASP]].

> [!success] Resultado
> A partir de la auditoría técnica exhaustiva de los cambios recientes, se erradicó el antipatrón de `Margin="6"` mediante cálculo dinámico Win32 en `WM_GETMINMAXINFO` adaptado a Per-Monitor V2, se conmutó el icono y tooltip de maximizar/restaurar, se migró `LoginWindow` a `WindowChrome` eliminando `AllowsTransparency="True"` y habilitando animaciones nativas DWM, se extrajo `BitmapHelper` para desacoplar el ViewModel de renderizado gráfico de bajo nivel, y se emitieron directrices de seguridad OWASP para notificaciones móviles.

---

## 1. Cambios Técnicos

1. **`MainWindow.xaml` y `MainWindow.xaml.cs`:**
   - Retiro de `<Setter Property="Margin" Value="6"/>` en `RootBorder`.
   - Intercepción de `WM_GETMINMAXINFO` con `GetSystemMetricsForDpi` (`SM_CXSIZEFRAME` + `SM_CXPADDEDBORDER`) para ajustar `ptMaxPosition` y `ptMaxSize` con precisión física sobre `rcWork`.
   - Conmutación dinámica del glifo `&#xE922;` (Maximizar) y `&#xE923;` (Restaurar) con ToolTip adaptativo en `BtnMaximizar`.
2. **`LoginWindow.xaml` y `LoginWindow.xaml.cs`:**
   - Retiro de `AllowsTransparency="True"`, `WindowStyle="None"` y `Background="Transparent"`.
   - Adición de `WindowStyle="SingleBorderWindow"` y `WindowChrome` con `CaptionHeight="32"`.
   - Conexión de `BtnMinimize_Click` a `SystemCommands.MinimizeWindow(this)`.
   - Registro de atributos DWM (`DWMWA_WINDOW_CORNER_PREFERENCE`) en `SourceInitialized`.
3. **`BitmapHelper.cs` y `ConfiguracionEmpresaViewModel.cs`:**
   - Creación de `CapaUI.Core.Helpers.BitmapHelper` centralizando `CargarBitmapCongelado`.
   - Delegación en el ViewModel manteniendo 100% retrocompatibilidad.

---

## 2. Verificación de Compilación y Calidad

- **Build:** `dotnet build BimboProyecto.sln /p:TreatWarningsAsErrors=true` $\rightarrow$ **0 errores, 0 advertencias**.
- **Tests:** `dotnet test BimboProyecto.Tests` $\rightarrow$ **498 de 498 pruebas aprobadas (100%)**.
