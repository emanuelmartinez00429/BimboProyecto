---
title: "Sesión 2026-09-17 — Alineación de Columnas a la Izquierda en UsuariosView"
tags:
  - sesion
  - wpf
  - ui
  - usuarios
  - datagrid
  - estilos
  - convencion
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente) — Sesión Fernando
---

# Sesión 2026-09-17 — Alineación de Columnas a la Izquierda en UsuariosView

> [!success] Resultado
> Se homologó la vista `UsuariosView.xaml` al estándar de diseño del sistema, corrigiendo la alineación de todas las columnas de datos y metadatos hacia la izquierda, retirando el estilo local redundante de `DataGridColumnHeader` para heredar el estilo canónico centralizado en `Styles.xaml`, preservando `#` y `ESTADO` centrados, y agregando pruebas de caja blanca automáticas en `UsuariosWhiteBoxTests.cs`.

---

## 1. Contexto y Causa Raíz

En sesiones anteriores, la vista de usuarios (`UsuariosView.xaml`) había quedado configurada con `HeaderDerecho` y `CeldaDerecha` en todas sus columnas de datos, además de poseer un `<Style TargetType="DataGridColumnHeader">` local en sus `UserControl.Resources` que forzaba el centrado (`HorizontalContentAlignment="Center"` y `HorizontalAlignment="Center"`).

Esto rompía la uniformidad del sistema respecto a las demás pantallas (`CategoriasView`, `ProveedoresView`, `FabricantesView`, `PresentacionesView`, `ProductosView`), las cuales consumen el estilo global de `Styles.xaml` con encabezados y celdas de texto alineados a la izquierda.

---

## 2. Cambios Técnicos

1. **`CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml`:**
   - **Eliminación de override local de cabecera:** Se eliminó el `<Style TargetType="DataGridColumnHeader">` local (líneas 85–105) para heredar el estilo canónico de `Styles.xaml` (`HorizontalContentAlignment="Left"` con `Padding="10,0"`).
   - **Estilo de celda base en `DgUsuarios`:** Se cambió `CellStyle="{StaticResource CeldaCentrada}"` por `CellStyle="{StaticResource CeldaIzquierda}"` en la definición principal del `DataGrid`.
   - **Columna `#`:** Se retiró `HeaderDerecho` y `CeldaDerecha`. Se asignó `CellStyle="{StaticResource CeldaCentrada}"` para centrar el número de fila.
   - **Columnas `EMPLEADO` y `EMAIL`:** Se retiraron `HeaderDerecho` y `CeldaDerecha`. Se asignó `CellStyle="{StaticResource CeldaIzquierda}"`, y los `TextBlock` se alinearon con `HorizontalAlignment="Left"` y `Padding="8,0"`. Ambas columnas comparten el dimensionamiento flexible `Width="*"` con `MinWidth="220"` para equilibrar el espacio horizontal disponible de manera homogénea.
   - **Columna `ROL`:** Se retiraron `HeaderDerecho` y `CeldaDerecha`. Se asignó `CellStyle="{StaticResource CeldaIzquierda}"`, y el `Border` tipo chip se alineó con `HorizontalAlignment="Left"` y `Margin="8,0"`.
   - **Columna `ÚLTIMO ACCESO`:** Se retiraron `HeaderDerecho` y `CeldaDerecha`. Se asignó `CellStyle="{StaticResource CeldaIzquierda}"`, y el `TextBlock` se alineó con `HorizontalAlignment="Left"` y `Padding="8,0"`.
   - **Columnas `CREADO` y `ACTUALIZADO`:** Se retiraron `HeaderDerecho` y `CeldaDerecha`. Se asignó `CellStyle="{StaticResource CeldaIzquierda}"`, y los `TextBlock` se alinearon con `HorizontalAlignment="Left"` y `Padding="10,0"`.
   - **Columna `ESTADO`:** Se configuró con `HeaderStyle="{StaticResource HeaderCentrado}"` y `CellStyle="{StaticResource CeldaCentrada}"` para mantener el checkbox centrado con su encabezado.

2. **`BimboProyecto.Tests/Usuarios/UsuariosWhiteBoxTests.cs`:**
   - Nueva suite de pruebas automatizadas que verifica por regex la ausencia de `HeaderDerecho` y `CeldaDerecha`, la inexistencia de override local de `DataGridColumnHeader`, el uso de `CeldaIzquierda` en columnas de datos y el centrado estricto de `#` y `ESTADO`.

---

## 3. Verificación de Compilación y Calidad

- **Build:** `dotnet build BimboProyecto.sln /p:TreatWarningsAsErrors=true` $\rightarrow$ **0 errores, 0 advertencias**.
- **Tests:** `dotnet test BimboProyecto.Tests\BimboProyecto.Tests.csproj` $\rightarrow$ **499 de 499 pruebas aprobadas (100%)**.
