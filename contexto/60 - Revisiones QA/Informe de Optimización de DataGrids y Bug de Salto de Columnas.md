# Informe Técnico de Auditoría Crítica y Optimización de DataGrids

**Proyecto:** BimboProyecto (.NET 8 / WPF)  
**Fecha:** 2026-09-08  
**Autor:** Especialista en Rendimiento y Optimización de Software  
**Alcance:** Auditoría integral de las 13 instancias de `DataGrid` en la solución, diagnóstico exhaustivo del bug de salto/desplazamiento dinámico de columnas durante el scroll en `ProductosView.xaml` y plan de remediación técnica.

---

## 1. Diagnóstico del Bug: ¿Por qué se "corren todas las columnas de la nada" al hacer scroll?

### El Síntoma Reportado
> *"La tabla renderiza al ancho de lo que se ve pero si mientras voy haciendo scroll lo de abajo abarca más espacio se corren todas las columnas de la nada, pasa en productos seguido."*

### Causa Raíz Matemática (`MeasureOverride` vs Virtualización de UI)
En [`ProductosView.xaml:L473-L618`](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml#L473-L618), **14 de las 15 columnas están configuradas con `Width="Auto"`** (solo la columna `#` tiene un ancho fijo de 40 px).

```text
[Carga Inicial]
Viewport muestra Filas 1 a 12 (ej. nombres cortos como "Harina Suave", 120 px).
↳ Columna PRODUCTO se ajusta a su MinWidth = 160 px.
↳ Las 12 columnas siguientes se colocan ordenadamente a partir de esa posición.

[Durante el Scroll Vertical]
El usuario baja la barra de scroll y entra al viewport la Fila 28:
"MEJORADOR DE MASA ESPECIAL PARA PANIFICACIÓN INDUSTRIAL ULTRA RESISTENTE..."
↳ Como la columna tiene Width="Auto", WPF ejecuta el pase de medición:
  MeasureOverride(new Size(double.PositiveInfinity, 36))
↳ El TextBlock mide su texto completo y reporta que desea 390 px.
↳ ¡TextTrimming="CharacterEllipsis" NO SE ACTIVA! 
  (Porque para infinitos píxeles disponibles, el texto cabe entero).

[El Salto / Corrimiento Brusco]
↳ La columna PRODUCTO se ensancha dinámicamente de 160 px a 390 px (+230 px).
↳ El StackPanel horizontal del DataGrid EMPUJA físicamente 230 px hacia la derecha 
  a TODAS las 12 columnas subsiguientes (Fabricante, Proveedor, País, Contenido, 
  Presentación, Categoría, Precio, Creado, Actualizado, Estado, etc.).
↳ Al continuar bajando, si otra columna en "Auto" (como PROVEEDOR) encuentra un texto
  más largo, vuelve a saltar y a empujar a las demás hacia la derecha.
```

### ¿Por qué `TextTrimming="CharacterEllipsis"` no lo evitó?
En [`ProductosView.xaml:L493`](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml#L493), el `TextBlock` tiene asignado `TextTrimming="CharacterEllipsis"`.  
Sin embargo, en el motor de renderizado de WPF, **`CharacterEllipsis` solo trunca texto cuando el contenedor padre le impone una cota finita (`availableWidth < textWidth`)**.  
Cuando una columna de `DataGrid` tiene `Width="Auto"`, el DataGrid le pregunta a la celda cuánto espacio desea medir dándole **ancho infinito**. El `TextBlock` responde con la longitud total del texto y jamás se recorta con puntos suspensivos, provocando el ensanchamiento en vivo de la columna.

---

## 2. ¿Por qué `ProductosView` se siente lenta con solo 50 ítems?

El backend pagina a 50 registros (`PageSize = 50`), por lo que no es un problema de volumen de base de datos. La lentitud proviene de **5 frenos simultáneos en el hilo de interfaz**:

1. **Re-rasterización continua en GPU por `DropShadowEffect`**:
   En [`ProductosView.xaml:L411-L414`](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml#L411-L414), el recuadro blanco que envuelve a la tabla tiene un `DropShadowEffect` (`BlurRadius="14"`). Como la tabla es hija directa de ese `Border`, cualquier movimiento del scroll invalida la textura fuera de pantalla de DirectX, obligando al compositor gráfico a re-ejecutar el shader de desenfoque gaussiano de toda la tarjeta en cada cuadro.
2. **Scroll sub-píxel (`VirtualizingPanel.ScrollUnit="Pixel"`)**:
   En [`ProductosView.xaml:L468`](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml#L468), el scroll está configurado en `Pixel`. Esto fuerza a WPF a calcular matrices de transformación fraccionarias y recortes sub-píxel continuamente, en lugar de avanzar de fila en fila.
3. **Ausencia de `DataGrid.RowHeight="36"`**:
   Al no declarar la altura fija a nivel de control, el `VirtualizingStackPanel` no puede usar una fórmula matemática directa (`Ítems × Altura`) para calcular el desplazamiento y debe inspeccionar dinámicamente cada fila para medirla.
4. **15 Shaders de celda en la columna `ESTADO`**:
   En [`ProductosView.xaml:L636`](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml#L636), el circulito verde de cada producto activo contiene un `DropShadowEffect` individual (`BlurRadius="4"`). En 15 filas visibles, la GPU tiene que procesar 15 efectos de celda + 1 efecto global en cada micro-movimiento.
5. **17 Triggers inútiles en la columna `#`**:
   [`Styles.xaml:L1230`](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Resources/Styles.xaml#L1230) hace que el número de fila herede de `TextoCeldaConFallback`, evaluando 17 comparaciones de cadenas (`"Sin proveedor"`, `"Sin RTN"`, etc.) por cada número que aparece en pantalla.

---

## 3. Matriz de Auditoría en las 13 Instancias de DataGrid de la Solución

| Archivo / Pantalla | Control DataGrid | Sombra Contenedor (`DropShadowEffect` sobre Scroll) | `ScrollUnit` | `RowHeight` en Grid | Estrategia de Columnas | Riesgo de Salto de Columnas |
|---|---|---|---|---|---|---|
| **`ProductosView.xaml`** | `DgProductos` | **CRÍTICO** (L411, Blur 14 en Border) | `Pixel` (L468) | *Vacío* | 1 Fija (`#`), 14 en `Auto` | **EXTREMO** |
| **`ProveedoresView.xaml`** | `DgProveedores` | **CRÍTICO** (L145, Blur 14 en Border) | `Pixel` (L175) | *Vacío* | 2 Fijas, 6 en `Auto`, 1 en `*` | **MEDIO-ALTO** |
| **`FabricantesView.xaml`** | `DgFabricantes` | **CRÍTICO** (L153, Blur 14 en Border) | `Pixel` (L182) | *Vacío* | 2 Fijas, 5 en `Auto`, 1 en `*` | **MEDIO-ALTO** |
| **`CategoriasView.xaml`** | `DgCategorias` | **CRÍTICO** (L130, Blur 14 en Border) | `Pixel` (L156) | *Vacío* | 3 Fijas, 2 en `Auto`, 1 en `*` | **BAJO** (Nombre 220 fijo) |
| **`PresentacionesView.xaml`** | `DgPresentaciones` | **CRÍTICO** (L166, Blur 14 en Border) | `Pixel` (L192) | *Vacío* | 4 Fijas, 2 en `Auto`, 1 en `*` | **BAJO** (Nombre 220 fijo) |
| **`UsuariosView.xaml`** | `DgUsuarios` | **CRÍTICO** (L271, Blur 14 en Border) | `Pixel` (L323) | *Vacío* | 6 Fijas (220, 200, 130, 160, 70, 40) | **NULO** (0 columnas en Auto) |
| **`EmpleadosView.xaml`** | `DgEmpleados` | **CRÍTICO** (L266, Blur 14 en Border) | `Pixel` (L312) | *Vacío* | 6 Fijas (220, 150, 140, 220, 70, 40) | **NULO** (0 columnas en Auto) |
| **`ContactosProveedoresView.xaml`** | `DgProveedores` / `DgContactos` | **CRÍTICO** (L226, Blur 14 en Border) | `Pixel` | *Vacío* | Master: `*` y fijas. Detalle: `*` y fijas | **NULO** |
| **`ContactosFabricantesView.xaml`** | `DgFabricantes` / `DgContactos` | **CRÍTICO** (L226, Blur 14 en Border) | `Pixel` | *Vacío* | Master: `*` y fijas. Detalle: `*` y fijas | **NULO** |
| **`BitacoraView.xaml`** | `DgBitacora` | **CRÍTICO** (L302, Blur 14 en Border) | `Pixel` (L348) | *Vacío* | 6 Fijas, 1 en `*` (`DETALLE`) | **NULO** |
| **`ReporteriaView.xaml`** | `DgPreview` | **CRÍTICO** (L192, Blur 14 en Border) | `Pixel` (L284) | *Vacío* | 100% dinámicas (`AutoGenerateColumns`) | **ALTO** en reportes con texto largo |
| **`PesajeView.xaml` (Movimiento)** | `DgProductos` | **CRÍTICO** (L789, Blur 12 en Border) | `Pixel` (L838) | **46** (L827) | `CÓDIGO` Auto (min 85), `PRODUCTO` `*` | **NULO** |
| **`PesajeView.xaml` (Entradas)** | `DgEntradas` | **OPTIMIZADO** (Desacoplado) | **`Item`** | **40** | Fijas + Proporcionales `*` | **NULO** (Resuelto) |
| **`SelectorCatalogoModal.xaml`** | `Dg` | **OPTIMIZADO** (Sin sombra en Border) | **`Item`** | *Vacío* | 4 Fijas (42, 200, 240, 120) | **NULO** (Resuelto) |

---

## 4. Residuos Detectados en `PesajeView.xaml` (Columna Izquierda)

1. **`LstCamiones` (L475-L476)**: El `Border` contenedor sigue teniendo `DropShadowEffect` (`BlurRadius="12"`) sobre la lista con scroll, y cada tarjeta interna de camión ([L526](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml#L526)) dibuja una sombra individual.
2. **`DgProductos` (L788-L789)**: El `Border` de la grilla de productos de la carga sigue teniendo `DropShadowEffect` y `ScrollUnit="Pixel"` ([L838](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml#L838)).
3. **Botones Quitar en Camión y Producto ([L745](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml#L745) y [L940](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml#L940))**: Siguen usando `BotonQuitarFila` con `<Path>` inline y búsqueda hacia arriba (`RelativeSource AncestorType=Button`) en vez del estilo global prefabricado `BasureroCelda`.

---

## 5. Plan de Acción y Solución Estandarizada

### Fase 1: Solución Inmediata en `ProductosView.xaml`
1. **Fijar anchos estables (erradicar el salto de columnas)**:
   - Asignar anchos explícitos fijos y proporcionales `*`:
     - `#`: 40 px fijo.
     - `CÓDIGO`: 95 px (MinWidth="90").
     - `PESO TEÓRICO`, `TARA`, `PRECIO / KG`: 110 px fijos.
     - `PRODUCTO`: `Width="2.2*"` (MinWidth="180").
     - `FABRICANTE`, `PROVEEDOR`: `Width="1.3*"` (MinWidth="120").
     - `PAÍS`: 120 px, `CONTENIDO`: 105 px, `PRESENTACIÓN`: 130 px, `CATEGORÍA`: 140 px.
     - `CREADO`, `ACTUALIZADO`: 135 px fijos.
     - `ESTADO`: 70 px fijo.
2. **Desacoplar la sombra en `Border` hermano estático**:
   - Poner el `DropShadowEffect` en un `Border` detrás del Grid para que la GPU nunca lo re-renderice al scrollear.
3. **Configurar `VirtualizingPanel.ScrollUnit="Item"` y `RowHeight="36"`**.
4. **Remover el `DropShadowEffect` individual en la celda de `ESTADO`**: Reemplazarlo por un círculo plano `#10B981`.

### Fase 2: Estandarización Global de Catálogos
1. En [`Styles.xaml:L1230`](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Resources/Styles.xaml#L1230), eliminar la herencia de `TextoCeldaConFallback` en `TextoNumeroFila` para eliminar los 17 triggers de string en todas las vistas.
2. Aplicar el desacople de sombra de contenedor y `ScrollUnit="Item"` en `ProveedoresView`, `FabricantesView`, `CategoriasView`, `PresentacionesView`, `UsuariosView`, `EmpleadosView` y `BitacoraView`.

### Fase 3: Limpieza de Residuos en `PesajeView.xaml`
1. Desacoplar la sombra en `LstCamiones` y `DgProductos`.
2. Migrar los botones de quitar camión y quitar producto al estilo prefabricado `BasureroCelda`.
