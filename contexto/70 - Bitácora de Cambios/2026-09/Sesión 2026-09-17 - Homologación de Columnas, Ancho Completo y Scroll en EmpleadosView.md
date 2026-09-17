---
title: "Sesión 2026-09-17 — Homologación de Columnas, Ancho Completo y Scroll en EmpleadosView"
tags:
  - sesion
  - wpf
  - ui
  - empleados
  - datagrid
  - estilos
  - convencion
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente) — Sesión Fernando
---

# Sesión 2026-09-17 — Homologación de Columnas, Ancho Completo y Scroll en EmpleadosView

> [!success] Resultado
> Se homologó la vista `EmpleadosView.xaml` al estándar del sistema y a la distribución de `UsuariosView`, alineando todas las columnas de datos y cabeceras a la izquierda, retirando el estilo local redundante de `DataGridColumnHeader`, asignando dimensionamiento proporcional `Width="*"` (`MinWidth="220"`) a `EMPLEADO` y `CORREO` para aprovechar el 100% del ancho de pantalla sin dejar espacio vacío a la derecha, preservando `#` y `ESTADO` centrados, habilitando scroll lateral y sincronizándolo con `Shift + Rueda del mouse` vía `ScrollHorizontalConShift`, y añadiendo pruebas de caja blanca en `EmpleadosWhiteBoxTests.cs`.

---

## 1. Contexto y Causa Raíz

1. **Alineación inconsistente**: `EmpleadosView.xaml` contaba con un `<Style TargetType="DataGridColumnHeader">` local en sus recursos que forzaba el centrado (`HorizontalAlignment="Center"`) de todas las cabeceras, mientras que la columna `TELÉFONO` tenía celdas centradas (`HorizontalAlignment="Center"`), rompiendo la alineación a la izquierda estándar del resto de los catálogos.
2. **Espacio en blanco desaprovechado**: Con anchos fijos o `Width="Auto"` en todas las columnas, la grilla se acumulaba en el lateral izquierdo dejando un amplio bloque en blanco hacia la derecha en pantallas panorámicas.
3. **Scroll lateral y atajo de rueda**: No existía desplazamiento horizontal activo (`ScrollViewer.HorizontalScrollBarVisibility="Disabled"`), lo que provocaba truncamiento por elipsis cuando la ventana se achicaba, y carecía de captura para `Shift + Rueda del mouse`.

---

## 2. Cambios Técnicos

1. **`CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadosView.xaml`:**
   - **Eliminación del override local de cabeceras:** Se removió el bloque `<Style TargetType="DataGridColumnHeader">` local (líneas 85–105) para heredar el estilo centralizado de `Styles.xaml` (`HorizontalContentAlignment="Left"` con `Padding="10,0"`).
   - **Columna `#`:** Se asignó `HeaderStyle="{StaticResource HeaderCentrado}"` y `CellStyle="{StaticResource CeldaCentrada}"` con ancho fijo `Width="40"`.
   - **Columna `EMPLEADO`:** Configurada con `Width="*"` y `MinWidth="220"`, `CellStyle="{StaticResource CeldaIzquierda}"`, `TextTrimming="CharacterEllipsis"` y padding `8,0`. Absorbe la mitad del espacio elástico sobrante.
   - **Columna `IDENTIDAD`:** Configurada con `Width="Auto" MinWidth="150"`, `CellStyle="{StaticResource CeldaIzquierda}"` y padding `8,0`.
   - **Columna `TELÉFONO`:** Configurada con `Width="Auto" MinWidth="130"`, `CellStyle="{StaticResource CeldaIzquierda}"`, `HorizontalAlignment="Left"` y padding `8,0`.
   - **Columna `CORREO`:** Configurada con `Width="*"` y `MinWidth="220"`, `CellStyle="{StaticResource CeldaIzquierda}"` y padding `8,0`. Absorbe la otra mitad del espacio elástico sobrante hasta el borde derecho.
   - **Columna `ESTADO`:** Configurada con `Width="70"` y `MinWidth="70"`, `HeaderStyle="{StaticResource HeaderCentrado}"` y `CellStyle="{StaticResource CeldaCentrada}"`.
   - **Propiedades del DataGrid:** Se agregaron `RowBackground="White"`, `RenderOptions.ClearTypeHint="Enabled"`, `EnableRowVirtualization="True"`, `EnableColumnVirtualization="True"`, `ScrollViewer.CanContentScroll="True"`, `ScrollViewer.VerticalScrollBarVisibility="Auto"` y `ScrollViewer.HorizontalScrollBarVisibility="Auto"` con `ModernScrollBarAny`.

2. **`CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadosView.xaml.cs`:**
   - En `UserControl_Loaded`, se invocó `ScrollHorizontalConShift.Habilitar(DgEmpleados);` para canalizar el evento `PreviewMouseWheel` con la tecla `Shift` pulsada hacia el desplazamiento horizontal.

3. **`BimboProyecto.Tests/Empleados/EmpleadosWhiteBoxTests.cs`:**
   - Nueva suite de pruebas automatizadas que valida por regex:
     - Ausencia de override local de cabecera.
     - `CeldaIzquierda` en `EMPLEADO`, `IDENTIDAD`, `TELÉFONO` y `CORREO`.
     - `Width="*"` y `MinWidth="220"` en `EMPLEADO` y `CORREO`.
     - Centrado estricto de `#` y `ESTADO`.
     - Activación de `ScrollViewer.HorizontalScrollBarVisibility="Auto"` y `CanContentScroll="True"`.

---

## 3. Verificación y Calidad

- **Compilación de C# y XAML:** Limpia con 0 errores CS/MC.
- **Pruebas unitarias de regresión:**
  ```powershell
  dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
  ```
  Resultado: **526 de 526 pruebas aprobadas (100%)**.
