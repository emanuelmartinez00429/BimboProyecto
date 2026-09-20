---
title: "Sesión 2026-09-19 — Corrección de layout en Movimiento de Materia Prima y sincronización de fila TOTAL en Pesajes"
tags:
  - sesion
  - pesaje
  - wpf
  - ui
  - layout
date: 2026-09-19
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente) con Fernando
---

# Sesión 2026-09-19 — Corrección de layout en Movimiento de Materia Prima y sincronización de fila TOTAL en Pesajes

> [!success] Resultado
> Se corrigió el bug de layout en el panel de **Movimiento de Materia Prima** donde la barra de scroll horizontal aparecía pegada a la primera fila de producto y el overlay de carga salía pegado arriba. Además, se completó la sincronización de la fila **TOTAL** en Entradas mediante `ScrollViewer` acoplado (evitando recorte por layout clip de WPF) y se estandarizó la alineación a la izquierda de los encabezados de tabla en Pesajes.

---

## 1. Problema de layout en Movimiento de Materia Prima y Camiones

### Causa raíz
En `PesajeView.xaml`, el `Grid` interno de **Movimiento de Materia Prima** contenía 4 definiciones de fila (`Auto`, `Auto`, `*`, `Auto`):
```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>  <!-- Fila 0: Encabezado -->
    <RowDefinition Height="Auto"/>  <!-- Fila 1: Tabla y Overlays -->
    <RowDefinition Height="*"/>     <!-- Fila 2: Vacía -->
    <RowDefinition Height="Auto"/>  <!-- Fila 3: Botonera -->
</Grid.RowDefinitions>
```

Al estar asignado el contenedor interactivo a la **Fila 1 con `Height="Auto"`**:
1. **Barra de scroll pegada al item:** El `DataGrid` (`DgProductos`) colapsaba su altura a la cantidad exacta de filas renderizadas. Con un solo producto, medía apenas ~86 px (encabezado + 1 fila) y ubicaba su barra de desplazamiento horizontal inmediatamente debajo de esa fila, dejando todo el espacio sobrante hacia la botonera como un hueco en blanco.
2. **Overlay de carga pegado arriba:** `CargandoMovimiento` ("Actualizando productos...") quedaba restringido al alto colapsado de esa fila `Auto`, mostrándose pegado arriba en lugar de centrado en la tabla.
3. **Estado vacío descentrado:** `MovEmpty` ("Selecciona un camión...") sufría el mismo problema de centrado vertical.

### Solución aplicada
- Se redujo `Grid.RowDefinitions` a 3 filas simétricas:
  - `Row 0` (`Auto`): Encabezado de 42 px.
  - `Row 1` (`*`): Contenedor interactivo (`DgProductos`, `MovEmpty`, `CargandoMovimiento`), ocupando todo el alto disponible.
  - `Row 2` (`Auto`): `WrapPanel` con los botones de acción (`+ Agregar`, `Editar`, `Cerrar producto`).
- Se normalizó el panel de **Camiones de Entrega** a la misma estructura consistente de 3 filas (eliminando la fila `Auto` sobrante y el `Grid.RowSpan="2"` de `DgCamiones`).

---

## 2. Sincronización de la fila TOTAL en Entradas (`TotalScroll`)

### Causa raíz
La fila de totales vive fuera del `ScrollViewer` de `DgEntradas`. Anteriormente utilizaba un `TranslateTransform` en X para sincronizar su desplazamiento con el scroll horizontal de la grilla. No obstante, WPF aplica un recorte de layout (`layout clip`) a cualquier elemento cuyo contenido exceda el ancho del viewport asignado; dado que el recorte se traslada junto con la transformación visual, al hacer scroll horizontal hacia la derecha las columnas finales (NETO, BULTOS y el conteo de pesajes) quedaban recortadas e invisibles.

### Solución aplicada
- Se encapsuló `TotalGrid` dentro de un `ScrollViewer` nativo (`TotalScroll`) con:
  - `HorizontalScrollBarVisibility="Hidden"`
  - `VerticalScrollBarVisibility="Disabled"`
  - `Focusable="False"` e `IsHitTestVisible="False"`
- En `DgEntradas_ScrollChanged`: se sincronizan primero los anchos de columna (`SincronizarColumnasTotales()`) y se aplica el `HorizontalOffset` directamente a `TotalScroll.ScrollToHorizontalOffset(...)`.
- Se añadió el manejador `TotalScroll_ScrollChanged` para reaplicar el offset si el extent de la fila crece un pase de layout después de que la grilla ya scrolleó.

---

## 3. Alineación a la izquierda en encabezados de tabla (`PgHeader`)

- Se ajustó el estilo `PgHeader` en `PesajeView.xaml`:
  - `HorizontalContentAlignment="Left"`
  - `HorizontalAlignment="Left"` en el `TextBlock` del `ControlTemplate` (con padding `8,0`).
- Armoniza visualmente con el estándar del resto de pantallas del sistema (Usuarios, Proveedores, Productos).

---

## Archivos modificados

- **`CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml`:**
  - Estilo `PgHeader`: alineación a la izquierda.
  - Panel Camiones: `Grid.RowDefinitions` limpio de 3 filas y botonera en `Row="2"`.
  - Panel Movimiento: `Grid.RowDefinitions` limpio de 3 filas y botonera en `Row="2"`.
  - Fila TOTAL: reemplazo de `TranslateTransform` por `ScrollViewer TotalScroll`.
- **`CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml.cs`:**
  - Sincronización horizontal bidireccional en `DgEntradas_ScrollChanged` y nuevo handler `TotalScroll_ScrollChanged`.

---

## Verificación

- **Compilación:** `dotnet build CapaUI/CapaUI.csproj /t:Compile` → 0 errores, 0 advertencias.
- **Tests unitarios:** `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` → 595/595 pruebas superadas (0 fallos).
- **Prueba visual:** Verificada por el usuario.
