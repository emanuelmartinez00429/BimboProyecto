---
title: "Inventario — Superficie de escalado propio (LayoutTransform global)"
tags:
  - qa
  - inventario
  - wpf
  - escalado
  - dpi
  - windowchrome
date: 2026-09-20
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Code — Sesión Fernando
estado: Vigente
---

# Inventario — Superficie de escalado propio (LayoutTransform global)

> **Fase 0** del plan de escalado por usuario. Barrido de solo lectura sobre `CapaUI` para saber
> exactamente qué toca un `LayoutTransform` aplicado al raíz del contenido de cada ventana.
> No propone implementación: lista lo que hay que remediar y lo que hay que probar.

---

## Conclusión de la Fase 0

**El riesgo está concentrado, no repartido.** La intuición de que 1.094 `Width=` y 996 `Height=`
literales harían inviable el escalado es falsa: bajo un `LayoutTransform` esas medidas escalan
**junto con** el contenido, así que no se rompen entre sí.

El problema aparece solo donde una medida dura se encuentra con un **borde que no escala**: el marco
de la ventana. Eso reduce la superficie real a **4 ventanas, 1 `WindowChrome`, 5 popups, 1 plantilla
de `ComboBox` y ~80 sombras**.

---

## 1. Ventanas (HWND propios — no heredan el transform del árbol visual)

| Ventana | Tamaño declarado | Redimensionable | Cómo responde al transform |
|:--|:--|:--|:--|
| `Formularios/Principal/MainWindow.xaml:32` | `1200×800`, `Min 960×520`, `Maximized` | `CanResize` | **OK.** Maximizada, el contenido escala dentro del área disponible. Requiere ajustar `WM_GETMINMAXINFO` |
| `Formularios/InicioSesion/LoginWindow.xaml:5` | `900×560` **fijo** | `CanMinimize` | **No escala por diseño** (siempre 1,0). Si algún día escalara, tiene el mismo problema que `RolModal` |
| `Formularios/Principal/Pantallas/Notificaciones/NotificacionDetalleWindow.xaml:5` | `Width="560"` fijo, `SizeToContent="Height"`, `MaxHeight="720"` | `CanResize` | **Parcial.** El alto se adapta solo; el **ancho fijo no**. A 1,1× el contenido excede 560 y envuelve o recorta |
| `Formularios/Principal/Pantallas/Roles/RolModal.xaml:7` | `470×285` **fijo** | `NoResize` | **Rompe.** A 1,1× no entra y no hay forma de agrandar. Decidido: migra a `UserControl` sobre overlay (Fase 5) |

> [!important] Hallazgo nuevo respecto de la investigación
> `NotificacionDetalleWindow` no estaba marcada como problema. Su `Width="560"` es fijo, así que el
> helper de la Fase 3 **no alcanza con transformar el contenido**: tiene que escalar también el
> `Width` (y el `MaxHeight`) de la ventana, o el contenido no entra a factores > 1,0.

### Corrección (2026-09-20, durante la Fase 5)

Este inventario buscó ventanas con `grep "^<Window"` sobre los `.xaml`, y por eso **se le
escaparon las ventanas creadas por código**. Hay una más:

| Ventana | Dónde | Cómo responde |
|:--|:--|:--|
| Diálogo "Cambios pendientes" | `Formularios/Principal/Pantallas/Roles/RolesView.xaml.cs:110` — `new Window { ... }` | Abre a 1.0. `SizeToContent.WidthAndHeight`, así que **no recorta**: solo se ve proporcionalmente más grande que la app |

A esto se suma una categoría entera que tampoco escala y que conviene tratar junta:
**`MessageBox.Show`**, usado en 17 archivos (25 llamadas). Es un diálogo del sistema operativo y
sigue la escala de Windows, no la de la app.

**Decisión:** ambos quedan a 1.0 a propósito. Son diálogos nativos de confirmación, legibles por
definición a la escala del sistema, y la alternativa —resolver `IEscalaService` desde un control que
no pasa por el contenedor— traería un problema peor: el servicio es `Scoped`, y pedirlo al proveedor
raíz devolvería una instancia distinta de la de la sesión, siempre en 1.0. La inconsistencia es
cosmética y acotada.

---

## 2. `WindowChrome` — el hallazgo que no estaba en la investigación

`MainWindow.xaml:116` declara:

```xml
<WindowChrome CaptionHeight="56" ResizeBorderThickness="6" GlassFrameThickness="0" .../>
```

y la fila superior del layout mide exactamente lo mismo: `<RowDefinition Height="56"/>` (línea 137).

**`CaptionHeight` se expresa en coordenadas de la ventana y no se ve afectado por el
`LayoutTransform` del contenido.** A 0,8× la barra superior pasa a medir 44,8 px visuales, pero
Windows sigue tratando la franja 0–56 como área de título. Quedan **11,2 px por debajo de la barra
visible** donde un clic arrastra la ventana en vez de interactuar con lo que se ve ahí — el borde
superior del sidebar y del área de contenido.

Mitigante ya presente: hay **12 opt-ins de `WindowChrome.IsHitTestVisibleInChrome="True"`** en
`MainWindow.xaml` (líneas 172, 175, 210, 212, 218, 249, 264, 282, 378, 405, 447, 540), así que los
controles de la barra superior siguen respondiendo. Lo que **no** está cubierto es lo que queda
debajo de la barra al encogerse.

`LoginWindow.xaml:23` tiene `CaptionHeight="32"` con el mismo patrón, pero al ir siempre a 1,0 no aplica.

**Prueba obligatoria:** a 0,75× y 0,8×, hacer clic justo debajo de la barra superior y confirmar que
no arrastra la ventana.

---

## 3. Popups (5) — HWND separado, no heredan el transform

| Archivo | Línea | Nombre | Alcance |
|:--|:--|:--|:--|
| `Resources/Styles.xaml` | 648 | `PART_Popup` | **Plantilla del `ComboBox`** — un solo setter cubre los 21 usos de la app |
| `Core/Controls/SuggestionSearchBox.xaml` | 107 | `SuggestionsPopup` | Control compartido: grepear usos antes de tocar |
| `Formularios/Principal/MainWindow.xaml` | 304 | `NotifPopup` | Panel de notificaciones |
| `Formularios/Dashboard/DashboardView.xaml` | 199 | `PopupPeriodo` | Selector de período |
| `Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml` | 1248 | `ConfirmPopup` | `Placement="Center"` sobre `DgProductos` |

### `ComboBox` que cuelgan de la plantilla compartida (21)

`ProductosView` (9) · `BitacoraView` (3) · `UsuariosView` (3) · `NotificacionesView` (2) ·
`FabricantesView` (1) · `FabricanteModal` (1) · `ProductoModal` (1) · `UsuarioModal` (1)

---

## 4. ToolTips (76 `ToolTip="…"` + 1 explícito)

Concentración: `PesajeView` (11) · `MainWindow` (11) · `ReporteriaView` (9) · `ProductoModal` (5) ·
`ProductosCargaModal` (5) · `ProductosView` (4) · `PresentacionesView` (4). Un `<ToolTip>` con
`ToolTip.Style` propio en `MainWindow.xaml:221`.

> [!warning] El offset **no** debe escalar
> `Core/ToolTipPlacement.cs` sobrescribe `ToolTipService.VerticalOffset` con un desfase calculado
> contra un cursor de **32 px físicos**, deliberadamente insensible a `CursorBaseSize`. Si se escala
> el contenido del tooltip, el offset se deja intacto: el cursor sigue midiendo lo mismo.

---

## 5. Lo que NO existe (remedios de la investigación que se caen)

| Elemento | Usos | Consecuencia |
|:--|:--|:--|
| `ContextMenu` | **0** | Se cae el setter propuesto |
| `AdornerDecorator` / `AdornerLayer` | **0** | Se cae el `<AdornerDecorator>` local y la parte de adorners de la matriz |
| `Validation.ErrorTemplate` / `Validation.HasError` | **0** | No hay adorners implícitos de validación tampoco |

---

## 6. Medidas duras — el barrido que desactiva el miedo

Totales brutos: **1.094** ocurrencias de `Width="N"` y **996** de `Height="N"` (incluye
`ColumnDefinition`, `RowDefinition`, `Min*`, `Max*`).

Distribución de los literales de `Width`/`Height` sobre elementos:

| Rango | `Width` | `Height` | Qué son |
|:--|--:|--:|:--|
| ≤ 48 | 440 | 489 | Íconos, puntos, badges, avatares — vectoriales, escalan perfecto |
| 49–200 | 141 | 23 | Columnas de grilla, campos, botones |
| > 200 | 76 | 34 | Paneles y contenedores |

La mayoría de los `> 200` son **`d:DesignWidth` / `d:DesignHeight`**, inertes en runtime.

### Medidas fijas reales > 200 (la lista corta que importa)

| Archivo:línea | Medida | Riesgo |
|:--|:--|:--|
| `Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml:477` | `DgCamiones Height="242"` | Grilla de alto fijo; escala con el contenido, verificar filas visibles |
| `Formularios/Principal/Pantallas/Pesaje/Modales/ProductosCargaModal.xaml:10` | `Height="620"` | Modal sobre overlay: `ModalLayout` ya lo acota |
| `Formularios/Principal/Pantallas/Pesaje/Modales/RegistroCamionesModal.xaml:11` | `Height="620"` | Ídem |
| `Formularios/Principal/Pantallas/Pesaje/Modales/PesajeModal.xaml:10` | `Width="980"` | El más ancho: a 1,1× son 1.078 lógicos, `MaxWidth` del overlay puede comprimirlo |
| `Formularios/Principal/Pantallas/Reporteria/ReporteriaView.xaml:588` | `820×650` | Verificar en pantalla baja |
| `Formularios/Principal/MainWindow.xaml:168` y `:604` | `BrandBlock`/sidebar `Width="256"` | Animados por `AnimateSidebarWidth`; escalan juntos |
| `Formularios/Principal/MainWindow.xaml:310` | `Width="440" MaxHeight="720"` | Panel del popup de notificaciones — **dentro de un `Popup`**, ver §3 |
| Modales de catálogo | `520` / `560` / `620` | Acotados por `ModalLayout` |

---

## 7. Sombras (`DropShadowEffect`) — ~80 en 37 archivos

Un efecto rastariza a la resolución que impone el transform. La regla 12 de `AGENTS.md`
(*Zero-Shader Layout*) existe justamente porque esto ya fue un problema.

| Pantalla | Sombras | Nota |
|:--|--:|:--|
| `PesajeView.xaml` | 6 | **Peor caso**: además 3 `DataGrid` y `RowHeight` fijo |
| `ContactosFabricantesView` / `ContactosProveedoresView` | 5 c/u | |
| `ReporteriaView` | 4 | |
| `ProductosView`, `Styles.xaml` | 3 c/u | Las de `Styles.xaml` son compartidas |
| Resto (31 archivos) | 1–2 c/u | |

### `DataGrid` con alto de fila fijo

- `Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml:486` → `RowHeight="40"`
- `Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml:655` → `RowHeight="46"`
- `Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml:863` → `RowHeight="40"`
- `Formularios/Principal/Pantallas/Roles/RolesView.xaml:394` → `MinColumnWidth="250"`

**`PesajeView` es la pantalla de referencia para medir** (Fase 4): concentra sombras, grillas y altos fijos.

---

## 8. Pruebas que este inventario agrega a la matriz

1. A 0,75× y 0,8×, clic justo debajo de la barra superior de `MainWindow`: **no** debe arrastrar la ventana.
2. `NotificacionDetalleWindow` a 1,1×: el contenido entra en los 560 px o el ancho escaló con él.
3. `PesajeModal` (980 de ancho) a 1,1× en 1366×768: no se comprime ni recorta.
4. `DgCamiones` (alto fijo 242) a 0,75× y 1,1×: filas visibles coherentes, sin media fila cortada.
5. `ConfirmPopup` de `PesajeView` (`Placement="Center"` sobre la grilla): centrado correcto con el transform puesto.

---

## Relaciones

- [[WPF - DPI Awareness y Escalado Multi-Resolución]] — las 5 fases ya implementadas (2026-06-21)
- [[Convenciones de UI (WPF) — leer antes de tocar XAML]] — reglas que atan cualquier remediación
- [[Auditoría Técnica — Animaciones DWM, Maximizado Multi-DPI y Seguridad OWASP]] — antecedente sobre `WindowChrome` y maximizado
- [[Arquitectura Actual]]
