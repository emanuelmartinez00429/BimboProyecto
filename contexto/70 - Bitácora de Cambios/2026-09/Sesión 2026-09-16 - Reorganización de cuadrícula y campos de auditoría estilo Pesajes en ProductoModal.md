---
title: "Sesión 2026-09-16 — Reorganización de cuadrícula y campos de auditoría estilo Pesajes en ProductoModal"
date: 2026-09-16
tags:
  - bitacora
  - sesion
  - wpf
  - ui
  - ux
  - productos
  - estilos
aliases:
  - Reorganización ProductoModal
  - Auditoría estilo Pesajes
---

# Sesión 2026-09-16 — Reorganización de cuadrícula y campos de auditoría estilo Pesajes en ProductoModal

## Resumen

A petición del usuario y tras validación visual directa, se reorganizó la cuadrícula de formulario en [`ProductoModal.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Productos/ProductoModal.xaml) eliminando la asimetría de la fila 2 y logrando una distribución simétrica de 3 columnas por fila. Además, se rediseñó el estilo global de los campos de auditoría temporal (`ModalInputAuditoria`) en [`Styles.xaml`](../../../CapaUI/Resources/Styles.xaml) replicando la apariencia visual de los campos de solo lectura del módulo de Pesajes (`MInputDisplay`), erradicando la falsa sensación de que los campos estaban trabados o congelados.

## Intervenciones Realizadas

1. **Reorganización en Cuadrícula (`ProductoModal.xaml`):**
   - **Fila 2 (Grid.Row="2"):** Se reubicó el bloque de `País importado` (`TxtPais` y botón lupa `BtnBuscarPais`) a la columna 2 (`Grid.Column="2"`), llenando el espacio que anteriormente se encontraba desocupado a la par de `Contenido` y `Peso teórico`. Sus `TabIndex` se ajustaron a `100` y `101`.
   - **Fila 3 (Grid.Row="3"):** Se reubicó el selector segmentado de `Estado` (`RbActivo` y `RbInactivo`) a la columna 2 (`Grid.Column="2"`), a la par de `Tara (kg)` y `Precio por kg`. Sus `TabIndex` se ajustaron a `130` y `131`.
   - **Fila 4 (Grid.Row="4"):** Al liberarse la columna 2, los campos de auditoría `Creado` (Col 0) y `Actualizado` (Col 1) se complementaron con una tarjeta informativa del sistema en la columna 2 con el icono vectorial `IconHistory` (`#34D399`) y el texto aclaratorio: *"Auditoría automática — Las fechas de registro y modificación son generadas por el servidor y no se pueden alterar."*

2. **Estandarización de Estilo Display de Auditoría (`Styles.xaml`):**
   - Se actualizó el estilo global `ModalInputAuditoria` para adoptar el patrón de diseño de `MInputDisplay` de Pesajes:
     - Fondo traslúcido `#1AFFFFFF` con borde `#26FFFFFF` y bordes redondeados (`CornerRadius="6"`).
     - Viñeta vertical / barrita verde de acento a la izquierda (`Width="3"`, `Margin="2"`, `CornerRadius="2"`, color `#34D399`).
     - Tipografía menta `#6EE7B7`, fuente `Segoe UI`, tamaño `15`, `FontWeight="SemiBold"` con `Padding="17,0,10,0"`.
     - `Cursor="Arrow"`, `Focusable="False"`, `IsReadOnlyCaretVisible="False"` y `ToolTip` informativo base para eliminar cualquier indicio de campo interactivo o cursor de texto parpadeante (`I-Beam`).

## Verificación

- **Compilación de XAML y C#:** `dotnet build CapaUI/CapaUI.csproj /t:Compile` finalizó con 0 errores y 0 advertencias.
- **Pruebas Unitarias:** `dotnet test` superó las 435 pruebas unitarias sin fallos (100% de cobertura funcional).
- **Aprobación de Usuario:** Validado visual y funcionalmente en la interfaz en ejecución.
