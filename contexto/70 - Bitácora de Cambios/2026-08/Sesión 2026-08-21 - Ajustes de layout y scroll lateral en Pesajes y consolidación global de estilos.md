---
title: "Sesión 2026-08-21 — Ajustes de layout y scroll lateral en Pesajes y consolidación global de estilos"
tags:
  - sesion
  - pesaje
  - layout
  - datagrid
  - estilos
  - wpf
date: 2026-08-21
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Fernando / Antigravity (agente)
---

# Sesión 2026-08-21 — Ajustes de layout y scroll lateral en Pesajes y consolidación global de estilos

> [!success] Resultado
> Se ajustó la proporción horizontal y la simetría vertical en `PesajeView`. Se solucionó el problema de corte y compresión de columnas en las tablas de productos y pesajes mediante `MinWidth` explícitos, desplazamiento horizontal nativo y `Shift + Rueda`. Se reorganizaron los botones del pie de página y se centralizaron los estilos de botones de acción (`ToolPrimary`, `ToolSuccess`, `ToolWarn`, `ToolDanger`, `ToolBtn`) en `Styles.xaml` para su reutilización global en toda la aplicación. Se resolvió la advertencia CS8826 en `RolesViewModel`.

---

## Problema / motivo

1. **Proporción y asimetría en Pesajes:** La columna izquierda (Camiones y Productos) ocupaba demasiado ancho relativo respecto a la grilla de Entradas/Pesajes, y el panel de Camiones tenía una altura fija (`Height="262"`) que rompía la simetría con el panel de Productos.
2. **Tablas comprimidas e ilegibles en anchos reducidos:** Al reducir el ancho de la ventana, el `DataGrid` de WPF comprimía las columnas de ambas tablas hasta volver ilegibles los encabezados y datos ("BRU", "TARA", "NETO", "Ag", "Ed", etc.) en lugar de activar la barra de desplazamiento horizontal.
3. **Botones de acción del pie desalineados y con colores inconsistentes:** "Cerrar todos los camiones" estaba separado en el extremo derecho, e "Imprimir reporte", "Tara extra", "Editar" y "Quitar" requerían normalización a estilos sólidos uniformes con tipografía e íconos en blanco.
4. **Reutilización y coherencia visual:** Necesidad de que los estilos sólidos de botones de acción puedan ser reutilizados con el mismo nombre y comportamiento en cualquier vista o modal del sistema.

---

## Cambios aplicados

### 1. Simetría y ajuste de dimensiones en `PesajeView.xaml`

- **Ancho del panel izquierdo:** Se redujo en un 10% la columna de Camiones y Productos (de `0.85*` a `0.765*`), asignando mayor espacio horizontal a la tabla de pesajes/entradas (`1*`).
- **Simetría vertical 1:1:** Se reemplazó la altura fija (`Height="262"`) del panel de Camiones por `Height="*"`, distribuyendo el espacio vertical 50/50 de manera equitativa entre *Camiones de Entrega* y *Movimiento de Materia Prima*.

### 2. Scroll lateral real y protección de anchos en DataGrids

**Causa raíz del problema de corte:** En WPF `DataGrid`, cuando coexisten columnas con tamaño estrella (`*`) y columnas con ancho en píxeles pero sin `MinWidth` explícito, el algoritmo de layout comprime todas las columnas fijas hacia su valor mínimo por defecto (~20px) antes de activar el `ScrollViewer` horizontal.

**Solución aplicada en `PesajeView.xaml` y `PesajeView.xaml.cs`:**
- **Movimiento de Materia Prima (`DgProductos`):**
  - Se habilitó `ScrollViewer.HorizontalScrollBarVisibility="Auto"` y `ScrollViewer.CanContentScroll="True"`.
  - Se definieron `MinWidth` en todas las columnas: `CÓDIGO: 85px`, `PRODUCTO: 180px`, `REST.: 72px`, `% RESTANTE: 120px`, `ESTADO: 104px`.
  - Se implementó `DgProductos_ScrollChanged` y `DgProductos_PreviewMouseWheel` para soportar desplazamiento horizontal con `Shift + Rueda del mouse`.
- **Entradas de Materia Prima (`DgEntradas`):**
  - Se fijaron `MinWidth` en todas las columnas: `ID: 60px`, `PRODUCTO: 160px`, `BRUTO: 96px`, `TARA: 96px`, `TARA EXTRA: 102px`, `NETO: 96px`, `BULTOS: 86px`, `FECHA/HORA: 125px`.
  - Se sincronizaron los `MinWidth` correspondientes en la fila `TotalGrid`, garantizando alineación exacta al scrollear.
- **Toolbars responsivas:** Se transformaron los contenedores de botones de `UniformGrid` a `WrapPanel` para evitar que los botones compriman sus etiquetas de texto en anchos reducidos.

### 3. Reorganización de botones del footer

- **Imprimir reporte:** Se actualizó a estilo azul sólido (`ToolPrimary`) con texto e icono en blanco.
- **Cerrar todos los camiones:** Se reubicó al margen izquierdo, colocado inmediatamente a la par del botón "Imprimir reporte" con un margen horizontal de 10px.

### 4. Botones sólidos en la sección de Entradas

- **Tara extra:** Estilo `ToolPrimary` (azul sólido de marca) con ícono y texto blanco.
- **Editar:** Estilo `ToolPrimary` (azul sólido de marca) con ícono y texto blanco.
- **Quitar:** Estilo `ToolDanger` (rojo sólido `#DC2626`) con ícono y texto blanco.

### 5. Centralización global de estilos de botones en `Resources/Styles.xaml`

Se consolidaron en el diccionario global los siguientes estilos para su uso en toda la aplicación:

| Estilo (`x:Key`) | Tipo / Color | Hover | Propósito |
| :--- | :--- | :--- | :--- |
| **`ToolPrimary`** | Sólido Azul Marca (`EmpresaPrimaryBrush`) | `EmpresaPrimaryDarkBrush` | Acciones principales (Guardar, Imprimir, Editar, Pesar) |
| **`ToolSuccess`** | Sólido Verde (`#16A34A`) | `#15803D` | Creación / Altas ("Agregar", "Nuevo") |
| **`ToolWarn`** | Sólido Ámbar (`#D9924A`) | `#C17F3A` | Acciones de cierre / riesgo controlado |
| **`ToolDanger`** | Sólido Rojo (`#DC2626`) | `#B91C1C` | Eliminación / Bajas ("Quitar", "Eliminar") |
| **`ToolBtn`** | Outline Blanco | `#EFF6FF` / Borde Azul | Acciones secundarias o neutras |

Cada estilo incluye su propio `ControlTemplate` evitando que el hover pise colores sólidos con fondos claros incompatibles.

### 6. Corrección de advertencia en `RolesViewModel.cs`

- Se normalizaron los nombres de parámetros del método parcial de `CommunityToolkit.Mvvm`:
  `partial void OnRolSeleccionadoChanged(RolItemVm? oldValue, RolItemVm? newValue)`
  eliminando la advertencia de compilación `CS8826`.

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` — **0 Errores, 0 Advertencias**.
- Validación de marcado XAML y bindings en tiempo de compilación.

---

## Relaciones

- [[Módulo Pesaje]]
- [[Conocimiento Principal]]
- [[Arquitectura Actual]]
- [[Anatomía compartida de los modales]]
- [[Scroll horizontal con Shift en DataGrid]]