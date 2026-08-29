---
title: Anatomía compartida de los modales
type: patron
status: vigente
tags:
  - patron
  - wpf
  - modales
  - estilos
date: 2026-08-15
updated: 2026-08-19
summary: "Todo modal de formulario del proyecto usa los mismos estilos, definidos una sola vez en CapaUI/Resources/Styles.xaml. Antes cada modal los redefinía localmente,…"
scope:
  - CapaUI/Resources
symbols:
  - Background
  - BasedOn
  - BorderBrush
  - BorderThickness
  - CampoModal
  - CampoModalEnGrid
  - Celda
  - ComboBox
  - ControlTemplate
  - DataGrid
lifecycle: verified
---

# Anatomía compartida de los modales

> [!abstract]
> Todo modal de formulario del proyecto usa los mismos estilos, definidos **una sola vez** en `CapaUI/Resources/Styles.xaml`. Antes cada modal los redefinía localmente, y eso los fue desincronizando. **Un modal nuevo no debe declarar ninguno de estos estilos en su `UserControl.Resources`.**

---

## Los estilos y qué resuelve cada uno

| Estilo | Para | Nota |
|---|---|---|
| `EtiquetaCampo` | El rótulo **arriba** del campo | Cambiar acá el tamaño lo cambia en todos los modales |
| `CampoModal` | El `StackPanel` que envuelve etiqueta + input | Espaciado vertical entre campos |
| `CampoModalEnGrid` | Igual, con separación lateral | Para modales de varias columnas (`ProductoModal`) — es `BasedOn`, no una copia |
| `ModalInput` | `TextBox` | Incluye `TextoResponsivo` y `PART_Recorte` |
| `ModalPassword` | `PasswordBox` | No puede heredar de `ModalInput` (ver abajo) |
| `ModalCombo` | `ComboBox` | — |
| `ModalInputAuditoria` | Campos Creado/Actualizado | Solo lectura, apagado |
| `ModalSegBtn` | Toggle segmentado Activo/Inactivo | — |
| `ModalTitulo` / `ModalContexto` | Título y cinta del encabezado | — |
| `LupaBtnCompartido` | Botón lupa de campo de catálogo, **fondo claro** | Usado en `ReporteriaView` |
| `LupaBtnOscuro` | Igual, **fondo oscuro** (modales con marco degradado) | `ProductoModal`, `ProcesoDescargaModal` |
| `TablaCatalogoFila` / `Celda` / `Header` | Tabla blanca (fila/celda/header) sobre modal oscuro — alternas, hover, selección | `SelectorCatalogoModal`, tabla de productos de `ProcesoDescargaModal` |

---

## La estructura de un campo

**Etiqueta arriba, nunca a la par.** La forma vieja (un `Grid` de dos columnas con el rótulo alineado a la derecha) perdía 120–150 px de ancho en una columna de texto.

```xml
<StackPanel Style="{StaticResource CampoModal}">
    <TextBlock Text="Nombre" Style="{StaticResource EtiquetaCampo}"/>
    <TextBox x:Name="TxtNombre" Style="{StaticResource ModalInput}" TabIndex="10" MaxLength="100"/>
</StackPanel>
```

Convención de `TabIndex`: de diez en diez por campo (10, 20, 30…), y los pares que van juntos con el siguiente número (un campo de catálogo `30` y su lupa `31`). Deja lugar para insertar un campo en el medio sin renumerar todo.

Los botones del pie cierran la secuencia (`40`/`41` o el número que siga).

> [!warning] Cuidado al convertir un `Grid` a `StackPanel`
> Si el `Grid` tenía **dos hijos en la misma celda** (el caso de `RowEmpleado` en `UsuarioModal`: un `ComboBox` y un `TextBlock` que se alternan según alta/edición), pasarlos a `StackPanel` los **apila** y se ven los dos. Hay que envolverlos en un `Grid` interno para que sigan superpuestos.

---

## Foco: solo borde, sin relleno

El indicador de foco es **un aro verde `#34D399` de 3 px**, el mismo color de la fila seleccionada en las tablas. Antes también pintaba el interior del campo de `#C2F2E0`, y se quitó: tapaba el contraste del texto que se estaba escribiendo, y hacía que el campo enfocado se leyera como un estado del dato (verde = válido/activo) en vez de "acá está el cursor".

Ese patrón estaba clonado en **cinco** lugares. Si se agrega un control nuevo con foco propio, usar solo `BorderBrush` + `BorderThickness`, nunca `Background`.

Caso especial: en `ModalSegBtn` el relleno era el *único* indicador. Ahí se reemplazó por borde, con `BorderThickness="2"` y `BorderBrush` transparente de base — así encender el color no corre el layout.

---

## `PasswordBox` no hereda de `ModalInput`

WPF **no permite compartir un `ControlTemplate` entre `TextBox` y `PasswordBox`**: el `PART_ContentHost` espera tipos distintos. Por eso existe `ModalPassword`, que replica la apariencia a mano. No lleva `PART_Recorte` ni `TextoResponsivo` — el contenido va enmascarado, no hay nada que recortar ni que mostrar en un ToolTip.

---

## Recorte con `…` y ToolTip

Dos mecanismos complementarios, ambos ya resueltos — ver [[TextoResponsivo]]:

- **`TextBox`** → `TextoResponsivo.Activo`, ya puesto en `ModalInput`. En campos de solo lectura recorta el texto; en editables solo agrega el ToolTip.
- **`TextBlock`** → `TextoResponsivo.ToolTipSiRecorta`. El `…` ya lo pone `TextTrimming` nativo; esto agrega el ToolTip **solo cuando el texto no entra**, para que las celdas que sí entran no muestren un globo redundante.

En un `DataGrid` virtualizado el contenedor se recicla y el texto cambia sin disparar `Loaded` ni `SizeChanged`; por eso la variante de `TextBlock` engancha `TextProperty` con un `DependencyPropertyDescriptor`, y lo desengancha al desactivarse.

---

## Hint del atajo de guardado

El pie lleva `Ctrl+Enter para guardar`. El atajo ya funcionaba en casi todos los modales vía `controls:AtajoGuardar.Boton`, pero solo uno lo anunciaba — es decir, la función existía sin que el usuario pudiera enterarse.

---

## ⚠️ Regla: antes de agregar un estilo local, buscá acá primero

**Antes de escribir un `<Style>` o `<Geometry>` nuevo en el `UserControl.Resources` de un modal — o en un diccionario de estilos propio de un módulo —, buscá si ya existe algo equivalente en `CapaUI/Resources/Styles.xaml` (la tabla de arriba) o en este documento.** Un nombre distinto para lo mismo (`MInput` vs `ModalInput`, `LupaBtn` vs `LupaBtnCompartido`) es indistinguible de una duplicación real hasta que alguien lo audita.

Dos casos reales, sesión 2026-08-19:

1. **`LupaBtn` duplicado dos veces.** `ProductoModal` ya tenía su propia copia local de la lupa de campo de catálogo (variante oscura, para su marco degradado). Al agregarle el mismo patrón a `ProcesoDescargaModal`, se copió esa copia local en vez de darse cuenta de que ya existía `LupaBtnCompartido` (variante clara, usada en `ReporteriaView`) en el diccionario global — solo faltaba la variante oscura. Se resolvió centralizando ambas: `LupaBtnCompartido` + `LupaBtnOscuro`, las dos en `Styles.xaml`. Ver [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] y [[Selector de Catálogo - Selector genérico y multiselección]] (el selector que terminó necesitando la variante oscura).

2. **`PesajeModalStyles.xaml` es una familia paralela completa**, no solo un estilo suelto. Los modales de Pesaje (`PesajeModal`, `ProcesoDescargaModal`, `TaraExtraTotalModal`) importan `PesajeModalStyles.xaml` en vez de usar los estilos de esta tabla, con sus propios `MLabel`/`MInput`/`MCombo`/`MSegBtn` — casi-duplicados de `EtiquetaCampo`/`ModalInput`/`ModalCombo`/`ModalSegBtn`, pero **no idénticos**:
   - `MInput`/`MCombo` tienen fuente más chica (13.5 vs 16.5) y **no tienen** el aro verde de foco ni el borde rojo de `validacion:Validacion.TieneError` — los modales de Pesaje no muestran el mismo feedback de validación por campo que el resto de la app.
   - `MSegBtn` tiene el mismo problema (sin aro de foco), y encima el comentario que lo justificaba estaba **basado en una premisa falsa**: decía que `ModalSegBtn` "no es visible desde acá" porque se define local en cada modal CRUD — falso, `ModalSegBtn` está centralizado en `Styles.xaml` desde antes y sí es visible.
   - `MIcoSearch` y `MCombo` eran duplicados sin ningún uso real (`MIcoSearch` idéntico a `IconSearchShared`) y se eliminaron directamente (2026-08-19) — sin efecto visual, estaban muertos.
   - `MLabel` vs `EtiquetaCampo` difieren apenas en color (`#D9FFFFFF` vs `#E0FFFFFF`) y margen — probablemente deriva accidental, no una decisión.
   
   La fuente más chica de `MInput`/`MCombo` **sí puede ser intencional** (el wizard de `ProcesoDescargaModal` es más denso: tarjetas de producto con varios campos chicos por fila) — fusionarlo a ciegas con `ModalInput` podría romper ese layout. Por eso no se fusionó todavía: queda como [[Deuda Técnica - Pendientes|P-042]], pendiente de una decisión explícita (¿son 16.5px y sin validación por campo un gap real a corregir, o una variante "compacta" deliberada que hay que nombrar y documentar como tal, igual que `LupaBtnOscuro`?).

**Antes de crear un diccionario de estilos nuevo para un módulo nuevo:** primero preguntate si lo que necesitás ya está en `Styles.xaml`, y si hace falta una variante (como `LupaBtnOscuro`), agregala ahí con un nombre que distinga la variante — no dupliques el archivo entero "por las dudas".

---

## Relaciones

- [[TextoResponsivo]] — el helper de recorte y ToolTip
- [[Dialogo de confirmacion reusable]] — la advertencia al inactivar, que usa estos mismos estilos
- [[Módulo Productos]] — `ProductoModal` es la referencia de layout multi-columna
- [[WPF - StackPanel y columnas Auto no ceden espacio, no se achican de verdad]]
- [[Deuda Técnica - Pendientes]] — P-042, familia paralela de estilos en Pesaje
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] — origen de `LupaBtnOscuro` y de esta auditoría
