---
title: "Sesión 2026-09-17 — Reversión de regresión en maximizado de MainWindow"
tags:
  - sesion
  - wpf
  - windowchrome
  - win32
  - regression-fix
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente) — Sesión Fernando
---

# Sesión 2026-09-17 — Reversión de regresión en maximizado de MainWindow

> [!success] Resumen
> Se corrigió la regresión visual ocurrida en `MainWindow` al maximizar: se eliminó la resta de bordes del sistema (`SM_CXSIZEFRAME` / `SM_CXPADDEDBORDER`) en `WM_GETMINMAXINFO` que dejaba una separación de 8px hacia el escritorio y activaba la barra de título clásica de Win32, y se restauró el `DataTrigger` con `Margin="6"` sobre `RootBorder` al maximizar para evitar el recorte visual de elementos por overscan de DWM.

---

## 1. Diagnóstico de la Causa Raíz

En el commit `a050f636` se intentó reemplazar el margen XAML de `RootBorder` calculando dinámicamente el marco en `WM_GETMINMAXINFO`:
```csharp
mmi.ptMaxPosition.X  = (work.Left - full.Left) + borderX;
mmi.ptMaxPosition.Y  = (work.Top  - full.Top)  + borderY;
mmi.ptMaxSize.X      = (work.Right  - work.Left) - (2 * borderX);
mmi.ptMaxSize.Y      = (work.Bottom - work.Top)  - (2 * borderY);
```

**Efectos no deseados:**
1. Al restar `2 * borderX` y `2 * borderY` a `ptMaxSize` y sumar `borderX` y `borderY` a `ptMaxPosition`, la ventana maximizada se achicaba entre 14 y 16 píxeles respecto al área visible de la pantalla (`rcWork`) y se desplazaba 8 píxeles hacia abajo y hacia la derecha.
2. Esto generaba un espacio/borde transparente de unos 8px alrededor de toda la ventana maximizada a través del cual se veía el escritorio o las ventanas detrás.
3. Al mover el cursor hacia el borde superior de la pantalla, el puntero salía del área cliente de la ventana e ingresaba a la zona del marco/pantalla superior, disparando el dibujo de respaldo de Windows DWM (línea horizontal blanca y botones clásicos de Win32: `_`, `🗖`, `X`).

---

## 2. Corrección Aplicada

1. **`MainWindow.xaml.cs` (`WndProc`):**
   - Se restauró `ptMaxPosition` y `ptMaxSize` para cubrir el 100% del área de trabajo del monitor (`rcWork`), garantizando 0 píxeles de holgura contra el monitor:
     ```csharp
     mmi.ptMaxPosition.X  = Math.Abs(work.Left - full.Left);
     mmi.ptMaxPosition.Y  = Math.Abs(work.Top  - full.Top);
     mmi.ptMaxSize.X      = Math.Abs(work.Right  - work.Left);
     mmi.ptMaxSize.Y      = Math.Abs(work.Bottom - work.Top);
     ```
   - Se removieron los P/Invokes y constantes innecesarias (`GetSystemMetrics`, `GetSystemMetricsForDpi`, `SM_CXSIZEFRAME`, `SM_CYSIZEFRAME`, `SM_CXPADDEDBORDER`).

2. **`MainWindow.xaml` (`RootBorder`):**
   - Se restauró el `DataTrigger` sobre `RootBorder` para aplicar `Margin="6"` exclusivamente cuando `WindowState == Maximized`:
     ```xml
     <Border.Style>
         <Style TargetType="Border">
             <Setter Property="Margin" Value="0"/>
             <Style.Triggers>
                 <DataTrigger Binding="{Binding WindowState, RelativeSource={RelativeSource AncestorType=Window}}" Value="Maximized">
                     <Setter Property="Margin" Value="6"/>
                 </DataTrigger>
             </Style.Triggers>
         </Style>
     </Border.Style>
     ```
   - Esto compensa limpiamente el overscan nativo de DWM en ventanas `SingleBorderWindow` + `WindowChrome`, asegurando que la barra superior, la barra de búsqueda y los iconos mantengan sus márgenes de respiración sin recortes.

---

## 3. Pruebas y Validación

- **Build:** `dotnet build CapaUI\CapaUI.csproj` $\rightarrow$ **0 errores, 0 advertencias**.
- **Tests:** `dotnet test BimboProyecto.Tests\BimboProyecto.Tests.csproj` $\rightarrow$ **499 de 499 pruebas aprobadas (100%)**.
