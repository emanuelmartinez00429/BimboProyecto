---
title: "Sesión 2026-09-17 — Estandarización de encabezado en Dashboard con icono IcoDashboard"
date: 2026-09-17
tags:
  - bitacora
  - sesion
  - wpf
  - ui
  - ux
  - estilos
  - dashboard
  - reporteria
  - iconos
aliases:
  - Estandarización Header Dashboard
  - Integración IcoDashboard
---

# Sesión 2026-09-17 — Estandarización de encabezado en Dashboard con icono IcoDashboard

## Resumen

En esta sesión se homogeneizó la vista ejecutiva del Dashboard ([`DashboardView.xaml`](../../../CapaUI/Formularios/Dashboard/DashboardView.xaml)) integrando el patrón de encabezado estándar ("tarjeta de encabezado") previamente adoptado en Reportería y en los demás catálogos del sistema:
1. **Nuevo Icono Vectorial `IcoDashboard`:** Extraído del archivo oficial `Dashboard.svg` (Material Symbols *dashboard* / *space_dashboard*, `viewBox 0 -960 960 960`) ubicado en `D:\Proyectos\Iconos\Dashboard.svg`, registrado con `FillRule="Nonzero"` y `po:Freeze="True"` en [`Styles.xaml`](../../../CapaUI/Resources/Styles.xaml).
2. **Reutilización de Estructura de Encabezado de Reportería:** Se reemplazó el antiguo bloque plano de dos filas (breadcrumb de texto suelto en Fila 0 y títulos desarticulados en Fila 1) por el contenedor unificado `EncabezadoPagina` en la Fila 0, incorporando:
   - Tarjeta / insignia visual de 38×38 px con esquinas redondeadas (`CornerRadius="8"`), fondo corporativo primario (`EmpresaPrimaryBrush`), sombra de elevación (`EmpresaPrimaryColor`, Blur 8, Opacity 0.3) y el icono vectorial blanco `IcoDashboard` centrado (20×20 px).
   - Miga de pan estandarizada (`Reportería / Dashboard`) y título principal con tipografía corporativa de 21 px negrita (`#1A1F2E`).
   - Pastilla de fecha actual localizada (`DateLabel`) con fondo `#EEF2F8`, borde `#D8E1EC` y punto corporativo primario de 6×6 px, homologada con las pastillas informativas de Reportería.
   - Selector de período interactivo (`Hoy ▾`) y botón de actualización conectado a `RefreshCommand` migrado al estilo universal `ActionBtn` (`Height="38"`).
   - Barra de progreso indeterminada de carga y banner de error alojados ordenadamente al pie del encabezado.
3. **Homogeneización de Entorno y Fondo:**
   - Se unificó el fondo del `UserControl` a `#EAF1F8` (consistente con Reportería, Productos, Proveedores, Pesajes y Bitácora).
   - Se añadieron `UseLayoutRounding="True"`, `TextOptions.TextRenderingMode="Auto"` y `TextOptions.TextFormattingMode="Display"`.
   - Se importó `Styles.xaml` en `MergedDictionaries` cumpliendo la directriz ADR-028 / P-057 para soporte nativo en el diseñador de Visual Studio.

---

## Intervenciones Realizadas

### 1. Registro de `IcoDashboard` en [`Styles.xaml`](../../../CapaUI/Resources/Styles.xaml)
- Se incorporó la geometría `IcoDashboard` con directiva de congelamiento inmutable `po:Freeze="True"` y `FillRule="Nonzero"`:
  ```xaml
  <!-- IcoDashboard — fuente: Dashboard.svg (Material Icons "dashboard", viewBox 0 -960 960 960). -->
  <PathGeometry x:Key="IcoDashboard" FillRule="Nonzero"
                Figures="M540-600v-200h260v200H540ZM160-480v-320h260v320H160Zm380 320v-320h260v320H540Zm-380 0v-200h260v200H160Zm40-360h180v-240H200v240Zm380 320h180v-240H580v240Zm0-440h180v-120H580v120ZM200-200h180v-120H200v120Zm180-320Zm200-120Zm0 200ZM380-320Z"
                po:Freeze="True"/>
  ```

### 2. Refactorización de Encabezado en [`DashboardView.xaml`](../../../CapaUI/Formularios/Dashboard/DashboardView.xaml)
- Reorganización de la cuadrícula raíz a 4 filas (`Row 0: Encabezado`, `Row 1: Separador 14px`, `Row 2: KPIs`, `Row 3: Chart+Lista`), manteniendo intactos los índices de las secciones inferiores.
- Enlace del botón de refresco a `ActionBtn` eliminando eventos y bindings ad-hoc de ratón sobre `Border`, mejorando accesibilidad por teclado y coherencia visual con el resto de botones principales de la solución.

---

## Verificación

- **Compilación .NET:** `dotnet build CapaUI/CapaUI.csproj -c Debug` concluyó con **0 errores y 0 advertencias**.
- **Suite de Pruebas:** `dotnet test BimboProyecto.sln` ejecutó exitosamente las **525 pruebas unitarias (0 fallidas, 0 omitidas)**.
- **Validación XAML:** Se verificó que los bindings no introducen contratos `TwoWay` accidentales en propiedades de solo lectura.