---
title: "Auditoría Técnica — Animaciones DWM, Maximizado Multi-DPI y Seguridad OWASP"
tags:
  - qa
  - auditoria
  - wpf
  - dps
  - dpi
  - owasp
  - seguridad
  - win32
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente) — Sesión Fernando
revisor: DeepInvestigator QA
estado: Subsanado
---

# Auditoría Técnica — Animaciones DWM, Maximizado Multi-DPI y Seguridad OWASP

> [!danger] REVERTIDO 2026-09-17 — el hallazgo #1 era incorrecto: `Margin="6"` NO es un antipatrón
> **No borres el `Margin="6"` de `RootBorder` en `MainWindow.xaml`.** Ya se borró una vez siguiendo esta auditoría y rompió el maximizado.
>
> La remediación del hallazgo #1 (quitar el margen y compensar con `SM_CXSIZEFRAME + SM_CXPADDEDBORDER` en `WM_GETMINMAXINFO`) se revirtió el mismo día porque:
> 1. Restar `2 × border` a `ptMaxSize` achicaba la ventana maximizada ~16px respecto de `rcWork`, dejando ver el escritorio alrededor de toda la ventana.
> 2. Ese hueco en el borde superior hacía que el cursor saliera del área cliente, y DWM dibujaba su barra de respaldo clásica de Win32 (`_ 🗖 X` + línea blanca) encima del chrome propio.
>
> **El error conceptual:** la auditoría mezcló "este número está hardcodeado" (crítica válida) con "esta compensación sobra" (falso). El margen no es decorativo — es la compensación **obligatoria** del overscan del marco nativo cuando se usa `WindowStyle="SingleBorderWindow"` + `WindowChrome`, y actúa en la capa del **contenido**. Achicar el rect de la ventana actúa en la capa de la **ventana**: no son intercambiables.
>
> **Las dos configuraciones coherentes (no mezclar):**
> - `WindowStyle="None"` → sin margen y sin compensación Win32. Era el estado de `cc3955f`, funcionaba.
> - `WindowStyle="SingleBorderWindow"` → `Margin="6"` en `RootBorder` al maximizar **+** `ptMaxPosition`/`ptMaxSize` fijados a `rcWork` sin restar nada. Estado actual; es el que conserva las animaciones nativas DWM.
>
> La regresión se produjo al tomar el `WindowStyle` de la segunda sin ninguna de las dos compensaciones.
>
> Detalle completo: [[Sesión 2026-09-17 - Reversión de regresión en maximizado de MainWindow]].

> [!abstract] Resumen
> Se auditó técnicamente la solución previa de activación de animaciones nativas de Windows (DWM), minimizado en Login, compensación de recorte en pantalla completa y asesoría de notificaciones móviles. Se identificaron 3 debilidades de diseño/arquitectura, 1 antipatrón de escalado DPI ("Margin 6px") y 3 riesgos de seguridad OWASP en la integración de webhooks. Todos los hallazgos de código fueron remediados y certificados con 498/498 pruebas aprobadas y 0 advertencias de compilación.

---

## 1. Matriz de Hallazgos y Remediaciones

| # | Área | Clasificación | Causa Raíz Identificada | Remediación Implementada |
|---|---|---|---|---|
| ~~**1**~~ | ~~**Recorte al maximizar**~~ | ⛔ **REVERTIDO — diagnóstico incorrecto** (ver callout arriba) | ~~Se utilizó `<Setter Property="Margin" Value="6"/>` en un `DataTrigger` de XAML. El overscan de Win32 es físico (`SM_CXSIZEFRAME + SM_CXPADDEDBORDER`) y varía según la escala del monitor (100%: 8 DIPs, 150%: 5.33 DIPs, 200%: 4 DIPs). El margen estático de 6 cortaba en 100% y dejaba franjas vacías en pantallas de alta densidad.~~ **Lo único válido de esta crítica es que el `6` está hardcodeado; que la compensación sobre, no.** | ~~Eliminado el margen en XAML. Se interceptó `WM_GETMINMAXINFO` en `MainWindow.xaml.cs` calculando dinámicamente el marco con `GetSystemMetricsForDpi` y ajustando `ptMaxPosition` y `ptMaxSize` para encajar exactamente en `rcWork`.~~ **Revertido el 2026-09-17: rompía el maximizado.** El estado vigente es `Margin="6"` + `rcWork` sin restas. |
| **2** | **Minimizado en Login** | 🔴 Inconsistencia Crítica | `LoginWindow` tenía `ResizeMode="CanMinimize"`, pero mantenía `WindowStyle="None"`, `AllowsTransparency="True"` y `WindowState = Minimized;`. La ventana sufría degradación por capas de software (`WS_EX_LAYERED`) y carecía de animaciones suaves DWM. | Migrada a `WindowStyle="SingleBorderWindow"` con `<WindowChrome.WindowChrome>`, `SystemCommands.MinimizeWindow(this)`, retiro de `AllowsTransparency="True"` y registro de `DwmSetWindowAttribute`. |
| **3** | **Glifo de Maximizar/Restaurar** | 🟡 Defecto Visual | En `MainWindow.xaml`, `BtnMaximizar` mantenía estático el glifo `&#xE922;` (ChromeMaximize) incluso estando la ventana maximizada. | Implementado un `Style` con `DataTrigger` sobre `WindowState` que conmuta entre `&#xE922;` / `"Maximizar"` y `&#xE923;` / `"Restaurar"`. |
| **4** | **Acoplamiento MVVM** | 🟡 Violación de Capas | `MainWindow.xaml.cs` invocaba `ConfiguracionEmpresaViewModel.CargarBitmapCongelado()`, alojando lógica de renderizado de bajo nivel en un ViewModel de dominio. | Creado `CapaUI.Core.Helpers.BitmapHelper` centralizando la decodificación y congelamiento de mapas de bits. `ConfiguracionEmpresaViewModel` delega a este helper. |
| **5** | **Notificaciones Móviles** | 🔴 Riesgo OWASP | Canales abiertos de `ntfy.sh` exponen información confidencial en internet sin cifrado ni autenticación (OWASP A01/A02). Webhooks en `PreToolUse` causan DoS por saturación de tasa (API4:2023) y concatenación de scripts expone a inyección de comandos (CWE-78). | Recomendado el canal nativo de Windows (Phone Link / Enlace Móvil) con notificaciones Toast locales cifradas punto a punto sin servidores de terceros. |

---

## 2. Auditoría Detallada de Seguridad OWASP (Hooks y Notificaciones)

### 2.1. Fuga de Información Confidencial (OWASP A01 / ASVS V8)
* **Diagnóstico:** Los topics de `ntfy.sh` públicos no tienen control de acceso. Enviar detalles de detención de agentes o herramientas (que incluyen esquemas de base de datos, consultas SQL con datos de Bimbo o credenciales) expone información propietaria a cualquiera que conozca el topic.
* **Directriz Segura:** Usar **Enlace Móvil (Phone Link)** de Windows. Todo el tráfico viaja cifrado localmente punto a punto (Bluetooth o red de área local autenticada) entre la PC y el dispositivo móvil del desarrollador, sin transmitir datos a la nube.

### 2.2. Inyección de Comandos en Sistema Operativo (OWASP A03 / CWE-78)
* **Diagnóstico:** Concatenar entradas de herramientas en comandos directos de PowerShell dentro de `hooks.json` (ej. `powershell -Command "Invoke-RestMethod ... -Body '$TOOL_INPUT'"`) permite escapar la cadena mediante comillas o caracteres especiales del prompt.
* **Directriz Segura:** Invocar scripts con parámetros formales posicionales tipados, desinfectando o abstrayendo el payload a mensajes de estado fijos (ej. `"Antigravity requiere atención"`).

### 2.3. Denegación de Servicio por Agotamiento de Recursos (OWASP API4:2023)
* **Diagnóstico:** Poner un webhook en `PreToolUse` genera ráfagas de docenas de peticiones HTTP en segundos durante las operaciones de lectura del agente, bloqueando la IP por límite de tasa (HTTP 429) y pudiendo congelar el agente si el hook es síncrono.
* **Directriz Segura:** Enganchar únicamente el evento `Stop`.

---

## 3. Certificación de Calidad

- **Build de la Solución:** `dotnet build BimboProyecto.sln /p:TreatWarningsAsErrors=true` $\rightarrow$ **0 errores, 0 advertencias**.
- **Pruebas Automatizadas:** `dotnet test BimboProyecto.Tests` $\rightarrow$ **498 de 498 pruebas aprobadas (100%)**.
