---
title: "Sesión 2026-09-08 — Salto de columnas al scrollear y sombras sobre listas virtualizadas"
tags:
  - sesion
  - wpf
  - datagrid
  - layout
  - rendimiento
  - virtualizacion
date: 2026-09-08
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude (agente)
revisor: pendiente — Fase 1 validada por Fernando en runtime; Fases 2 y 3 sin probar todavía
---

# Sesión 2026-09-08 — Salto de columnas al scrollear y sombras sobre listas virtualizadas

> [!success] Resultado
> Se corrigió el bug de **"se corren todas las columnas de la nada"** al hacer scroll
> vertical en las grillas de catálogo (visible sobre todo en Productos) y se desacopló
> la sombra del contenedor de cada tabla para que la GPU no la re-renderice por frame.
> 13 vistas auditadas, **9 modificadas** (`ProductosView` + 7 catálogos + `PesajeView`).
> Build de `CapaUI`: **0 errores**. Ejecución del plan
> [[Informe de Optimización de DataGrids y Bug de Salto de Columnas]].

---

## 1. El bug: columnas `Width="Auto"` + virtualización de UI

En un `DataGrid` de WPF, una columna `Width="Auto"` (`DataGridLength.Auto`) se mide
contra **ancho infinito** y toma el `DesiredSize` del contenido de las celdas
**realizadas**. Con `VirtualizingPanel.IsVirtualizing="True"`, al scrollear se
realizan filas nuevas: si una trae un texto más largo que todo lo visto hasta ese
momento, la columna se **ensancha en vivo** y empuja a la derecha a todas las
columnas siguientes. Como la medición es con ancho infinito, `TextTrimming` nunca
llega a activarse.

Es el mismo mecanismo de measure/arrange que
[[WPF - StackPanel y columnas Auto no ceden espacio, no se achican de verdad]],
en su variante "durante el scroll" en vez de "al redimensionar la ventana".

**Fix (estrategia A2):** eliminar `Width="Auto"` en las columnas de datos. Cada
columna pasa a `Width="N*"` con su `MinWidth` — las estrella se reparten el ancho de
forma **puramente proporcional**, sin mirar el contenido, así que realizar una fila
nueva ya no cambia ningún ancho. Las columnas de fecha (`CREADO`/`ACTUALIZADO`) van a
ancho fijo en px (contenido de longitud constante). La columna `#` sigue fija en 40.
Cuando la suma de mínimos supera el viewport aparece la barra horizontal — estable,
sin salto (patrón ya usado en Pesaje, ver
[[Sesión 2026-08-21 - Ajustes de layout y scroll lateral en Pesajes y consolidación global de estilos]]).

---

## 2. Los otros frenos que se atacaron en la misma pasada

Aplican el checklist de [[WPF - Rendimiento de Efectos y Niveles de Renderizado]]:

1. **`DropShadowEffect` sobre el contenedor con scroll.** El `Border` que envuelve
   cada `DataGrid`/`ListBox` llevaba la sombra (`BlurRadius="14"`, o `12` en Pesaje).
   Cualquier cambio del contenido interno al scrollear invalidaba la textura
   intermedia y re-corría el shader de desenfoque de toda la tarjeta por frame.
   **Fix:** mover la sombra a un `Border` hermano **estático** detrás del contenido
   (se rasteriza una vez). El contenido queda en un `Grid`/`Border` sin `Effect`.
2. **`VirtualizingPanel.ScrollUnit="Pixel"` → `"Item"`** en todas las grillas
   tocadas. El alto de fila ya estaba fijo (`ProductRowStyle` → `Height="36"` en
   `Styles.xaml`, y `RowHeight` explícito en las de Pesaje), así que el extent de la
   virtualización es determinista. Contrapartida aceptada: la rueda avanza fila a
   fila.
3. **Sombra por celda en la columna `ESTADO`** (`BlurRadius="4"` en el check de
   activo): eliminada. Se conserva el check cuadrado — solo se borró `<Border.Effect>`.
4. **`TextoNumeroFila` dejó de heredar de `TextoCeldaConFallback`**
   (`Styles.xaml`). El número de fila arrastraba 18 `Trigger` de igualdad de `Text`
   ("Sin proveedor", "Sin RTN", …) que nunca podían dispararse. Limpieza, no
   rendimiento medible.

---

## 3. Fase 1 — `ProductosView.xaml` (validada en runtime por Fernando)

- **15 columnas** pasadas a estrella + `MinWidth` (suma de mínimos ≈ 1520 px):

  | Columna | Width | MinWidth | | Columna | Width | MinWidth |
  |---|---|---|---|---|---|---|
  | `#` | `40` fijo | — | | PRESENTACIÓN | `1.1*` | 105 |
  | CÓDIGO | `0.7*` | 90 | | CATEGORÍA | `1*` | 105 |
  | PRODUCTO | `2.4*` | 170 | | PRECIO / KG | `0.9*` | 100 |
  | PESO TEÓRICO | `0.85*` | 100 | | CREADO | `1.1*` | 125 |
  | TARA | `0.8*` | 90 | | ACTUALIZADO | `1.1*` | 125 |
  | FABRICANTE | `1.3*` | 110 | | ESTADO | `0.55*` | 70 |
  | PROVEEDOR | `1.3*` | 110 | | CONTENIDO | `0.8*` | 90 |
  | PAÍS | `0.85*` | 90 | | | | |

- Sombra `Blur 14` desacoplada a `Border` hermano; contenido en `Grid` + `Border`
  sin `Effect`.
- `ScrollUnit="Item"`; sombra de celda `ESTADO` eliminada.

---

## 4. Fase 2 — 7 catálogos

Todas: sombra de contenedor desacoplada + `ScrollUnit="Item"` + (donde aplica) sombra
de celda `ESTADO` eliminada.

| Vista | Cambios de columna |
|---|---|
| **Fabricantes** | NOMBRE `1.6*`/150 · DESCRIPCIÓN `2*`/220 · PROVEEDOR `1.5*`/150 · PAÍS `0.9*`/100 · CREADO + ACTUALIZADO `Auto`→`150` fijo |
| **Proveedores** | NOMBRE `1.5*`/150 · RTN `0.9*`/120 · TELÉFONO `0.9*`/110 · CORREO `1.4*`/170 · DIRECCIÓN `1.8*`/220 · CREADO + ACTUALIZADO `Auto`→`150` fijo |
| **Categorías** | CREADO + ACTUALIZADO `Auto`→`150` fijo (resto ya era fijo + 1 `*`) |
| **Presentaciones** | CREADO + ACTUALIZADO `Auto`→`150` fijo |
| **Usuarios** | sin cambios de columna (ya 100 % fijas) |
| **Empleados** | sin cambios de columna (ya 100 % fijas) |
| **Bitácora** | sin cambios de columna (fijas + 1 `*`); sin columna `ESTADO` |

---

## 5. Fase 3 — `PesajeView.xaml`

- **Sombras de contenedor desacopladas** en los dos paneles de la columna izquierda
  (`LstCamiones` y `DgProductos`), con el mismo patrón que ya tenía el panel de
  Entradas: `Border` estático solo-sombra + `Grid` hermano `ClipToBounds` con el
  contenido.
- **`DgProductos`: `ScrollUnit="Pixel"` → `"Item"`** (ya tenía `RowHeight="46"`).
- **Botones "quitar" migrados a `BasureroCelda`** (estilo global): quitar camión y
  quitar producto. Se eliminó el `<Path>` inline con
  `RelativeSource AncestorType=Button` — el icono `IcoTrashFila` vive en el template.
- **`BotonQuitarFila` local eliminado** (`PesajeView.xaml` Resources): sin usos tras
  la migración.

---

## 6. Qué NO se tocó (decisión explícita)

- **Sombra por tarjeta de camión** dentro de `LstCamiones` (`DropShadowEffect
  Blur 12` por `GroupItem`). Es un efecto anidado que se re-rasteriza al scrollear,
  pero quitarlo cambia visiblemente la tarjeta (le queda solo el borde `#D7E3F5`).
  Fernando decidió dejarla como está. Queda como residuo conocido.
- **Sombra de la tarjeta del toolbar** (`BlurRadius="10"`, `Opacity 0.06`) en las
  vistas de catálogo: contenido estático, no se toca.
- **Sombra del ícono del encabezado** (`BlurRadius="8"`): fuera del área de scroll.

---

## 7. Verificación

- `dotnet build CapaUI/CapaUI.csproj -c Debug` → **`Compilación correcta. 0 Errores`**
  (compila XAML → BAML de las 9 vistas + `Styles.xaml`).
- Durante la sesión el build falló dos veces por causas ajenas: (a) copia de DLLs
  bloqueada por `CapaUI.exe` en ejecución (`MSB3021/3027`); (b) un
  `CapaUI_zcgnx0m3_wpftmp.csproj` viejo del 6-sep que MSBuild arrastraba y hacía
  fallar con un `_cts` inexistente en `SelectorCatalogoModal.xaml.cs` (el campo real
  es `_ctsVida`). Se borró el `*_wpftmp.csproj` stale y el build quedó limpio.
- **Fase 1 validada por Fernando en runtime.** Fases 2 y 3 pendientes de prueba
  visual.

---

## Relaciones

- [[Informe de Optimización de DataGrids y Bug de Salto de Columnas]] — el plan que ejecuta esta sesión
- [[WPF - StackPanel y columnas Auto no ceden espacio, no se achican de verdad]] — mismo mecanismo measure/arrange, variante al redimensionar
- [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] — checklist de `DropShadowEffect` y niveles de render
- [[Sesión 2026-08-21 - Ajustes de layout y scroll lateral en Pesajes y consolidación global de estilos]] — origen del patrón `MinWidth` + scroll lateral en DataGrid
- [[Sesión 2026-08-14 - Scroll horizontal con Shift en DataGrid]] — `ScrollHorizontalConShift` ya enganchado en Productos
- [[Plan de Mejora - Módulo Productos (Revisión QA)]] — consolidación previa de estilos de celda (`CeldaCentrada`/`CeldaIzquierda`)
- [[Arquitectura Actual]]
