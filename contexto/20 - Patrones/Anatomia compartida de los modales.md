---
title: "Anatomía compartida de los modales"
tags:
  - patron
  - wpf
  - modales
  - estilos
date: 2026-08-15
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
| `TablaCatalogoFila` / `Celda` / `Header` | Tabla blanca (fila/celda/header) sobre modal oscuro — alternas, hover, selección | `SelectorCatalogoModal` |
| `CeldaInput` | `TextBox` **dentro de una fila** de tabla editable (34px, compacto) | `RegistroCamionesModal`, `ProductosCargaModal` |
| `BasureroCelda` | Botón de quitar la fila. Tiene estado deshabilitado: hay filas que no se pueden quitar | ídem |
| `CabeceraColTabla` | Rótulo de columna sobre la banda del color de empresa | ídem |
| `BtnAgregarFilaTabla` | Botón de agregar fila al pie de la tarjeta | ídem |

---

## La estructura de un campo

**Etiqueta arriba, nunca a la par.** La forma vieja (un `Grid` de dos columnas con el rótulo alineado a la derecha) perdía 120–150 px de ancho en una columna de texto.

```xml
<StackPanel Style="{StaticResource CampoModal}">
    <TextBlock Text="Nombre" Style="{StaticResource EtiquetaCampo}"/>
    <TextBox x:Name="TxtNombre" Style="{StaticResource ModalInput}" TabIndex="10"/>
</StackPanel>
```

> [!tip] Sin `MaxLength` en XAML
> No se declara `MaxLength` manualmente en el control XAML. La propiedad `MaxLength` es asignada automáticamente en tiempo de ejecución por `ValidadorFormulario.Segun(ReglasXxx.Campo)` a través de `TopePreventivo(m)`. Esto mantiene el XAML limpio y evita desincronizaciones con el esquema de base de datos (ver [[Validacion de formularios]] y [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]]).

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

## El marcador («placeholder») lo dibuja la plantilla, no la pantalla

**No superpongas un `TextBlock` gris sobre un `TextBox` para el texto de ayuda.** `ModalInput`, `CeldaInput` y `MInput` ya lo traen; la pantalla solo declara el texto:

```xml
<TextBox Style="{StaticResource CeldaInput}"
         controls:Placeholder.Texto="Sin observaciones"/>
```

El motivo es concreto (sesión 2026-09-07): cuando cada pantalla se superponía su propio `TextBlock`, tenía que **adivinar el margen** para que cayera donde arranca el texto real — que es `BorderThickness + Padding` del estilo, y encima cambia 1px al enfocar porque el borde pasa de 1 a 2. Los siete que había estaban en `24px` (`CamionModal`) y `12px` (modales de tabla) contra un origen real de 11px: todos corridos, y cada uno distinto. Dentro de la plantilla el marcador usa el **mismo** `{TemplateBinding Padding}` que el `PART_ContentHost`, así que cae alineado por construcción y hereda `FontFamily`/`FontSize` del input.

Se **oculta al enfocar** (vacío *y* sin foco), no solo al escribir: así el cursor nunca queda encima del texto gris.

> [!tip] Si querés correr el marcador, movés el `Padding` del estilo
> Ese `Padding` posiciona el texto real **y** el marcador a la vez, así que siguen alineados. Ojo con el alcance: `CeldaInput` lo comparten las tablas de `RegistroCamionesModal` y `ProductosCargaModal`.

Si el campo además lleva una capa de recorte con «…» al perder el foco, acordate de ocultarla cuando el texto está vacío — si no, tapa el marcador.

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

## Que el modal se vea en el diseñador de Visual Studio

**Estado: `ProductosCargaModal` es el piloto (2026-09-09). Quedan 16 modales y 12 vistas sin previsualizarse** → [[Deuda Técnica - Pendientes|P-057]].

El porqué está en [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]] y la decisión en [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]]. Acá va solo la receta.

Un modal se previsualiza cuando cumple las cuatro:

1. **Tiene un constructor público sin parámetros.** Es el único que el diseñador sabe llamar.
2. **Ese constructor no toca DI ni la base.** Si el modal necesita un repositorio, lo recibe **por constructor** desde la vista que lo abre — no lo va a buscar a `App.Services` (AGENTS.md, regla 9). Así el camino de diseño queda limpio solo, sin un `DesignerProperties.GetIsInDesignMode` que lo tape.
3. **Mergea los diccionarios que usa** en su `UserControl.Resources`:

   ```xml
   <ResourceDictionary.MergedDictionaries>
       <ResourceDictionary Source="pack://application:,,,/CapaUI;component/Resources/Styles.xaml"/>
       <ResourceDictionary Source="pack://application:,,,/CapaUI;component/Formularios/.../PesajeModalStyles.xaml"/>
   </ResourceDictionary.MergedDictionaries>
   ```

   > [!warning] Esto **no** contradice la regla del final de esta nota
   > Mergear el diccionario compartido ≠ redefinir estilos. Sigue prohibido escribir un `<Style x:Key="CeldaInput">` propio. Lo que se agrega es la línea que *carga* `Styles.xaml`, para que el control no dependa de que `App.xaml` ya lo haya hecho.

   > [!danger] No "completar" el merge con los colores `Empresa*`
   > Va a dar la tentación, porque en el lienzo el marco sale sin color. **No lo hagas.** Un diccionario mergeado en el control le gana a `Application.Resources`, que es donde `EmpresaThemeService` escribe el tema — el modal quedaría con el azul por defecto ignorando el color de la empresa, en runtime y sin error. Ver [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]].

4. **Ningún `{StaticResource}` en un atributo del elemento raíz.** Es el que rompía todo. En los modales aparece una sola vez, en el converter de `MaxWidth`/`MaxHeight`:

   ```diff
   - Converter={StaticResource RestarMargen}, ConverterParameter=48
   + Converter={x:Static conv:RestarMargenConverter.Instancia}, ConverterParameter=48, FallbackValue=880
   ```

   con `xmlns:conv="clr-namespace:CapaUI.Converters"`. El `FallbackValue` es lo que le da tamaño real al lienzo: en el diseñador no hay `Border` ancestro y el `Binding` no produce valor.

**Qué esperar del lienzo:** estructura, estilos compartidos, layout y datos de muestra, **pero sin los colores de empresa** — el marco degradado sale gris. Es correcto y no hay que arreglarlo (ver el aviso de arriba): esas claves viven en `Application.Resources` porque el tema las reescribe ahí, y `{DynamicResource}` degrada sin excepción justamente para esto. El lienzo sirve para maquetar, no para aprobar colores.

Opcional, pero es lo que hace útil la vista previa: **sembrar datos de muestra en el constructor de diseño**, para ver la tabla con sus filas, el zigzag y los contadores en vez de una tarjeta vacía. No hay atajo `d:` para esto: las filas se pueblan desde código y `FilaProducto` es una clase anidada, así que `d:DesignInstance` no la alcanza cómodamente.

### Plan de réplica

Barrido completo del ensamblado (2026-09-09): instanciar cada `UserControl` con `Application.Resources` vacío y ver qué pasa. **Modales y vistas son dos problemas distintos** — no se replican igual.

**Los modales: el bloqueo es el paso 1, no el 4.**

De los 18 modales, **17 no tienen constructor sin parámetros**, así que el diseñador ni siquiera llega a instanciarlos: el paso 4 no importa hasta que exista ese ctor. Los únicos que se previsualizan hoy son `ProductosCargaModal` (el piloto) y `FormatoReporteModal` (que ya se previsualizaba: no usa `MaxWidth` con converter).

Todos comparten el mismo `MaxWidth`/`MaxHeight` en el raíz, así que el paso 4 es idéntico en todos: reemplazar `{StaticResource RestarMargen}` por `{x:Static conv:RestarMargenConverter.Instancia}`, sumar el `xmlns:conv` y un `FallbackValue` con el `Width`/`Height` propio de cada uno.

| Modal | Falta paso 1 (ctor) | Falta paso 2 (DI en el `.ctor`) | Falta 3 y 4 |
|---|---|---|---|
| `RegistroCamionesModal`, `CamionModal` | sí | sí — `ICatalogoRepository` | sí |
| `ProductoModal`, `FabricanteModal` | sí | sí — `ICatalogoRepository` | sí |
| `PesajeModal`, `TaraExtraTotalModal`, `ReporteModal` | sí | no | sí |
| `CategoriaModal`, `PresentacionModal`, `ProveedorModal`, `UsuarioModal`, `EmpleadoModal`, `ContactoFabricanteModal`, `ContactoProveedorModal` | sí | no | sí |
| `SelectorCatalogoModal`, `ConfiguracionEmpresaModal` | sí | no | sí |

**Las vistas: solo les falta el paso 3.**

Las `*View.xaml` ya tienen ctor sin parámetros y **no** usan el converter en el raíz, así que el paso 4 no aplica. Fallan todas en contenido interno, por falta del merge:

| Vista | Falla en | Alcanza con mergear `Styles.xaml` |
|---|---|---|
| `CategoriasView`, `FabricantesView`, `PresentacionesView`, `ProductosView`, `ProveedoresView`, `UsuariosView`, `EmpleadosView`, `ContactosFabricantesView`, `ContactosProveedoresView`, `BitacoraView` | `IconTag`, `IconBox`, `IconTruck`, `IconFactory`, `IconUser`, `IconHistorial` | ✅ sí |
| `PesajeView` | `CeldaCentrada` | ✅ sí |
| `ReporteriaView`, `UniversalSearchView` | `BoolToVisibility` | ❌ **no** — ese converter está en `App.xaml`, no en `Styles.xaml` |

Ya se previsualizan sin tocar nada: `DashboardView`, `NotificacionesView`, `RolesView`.

Los dos casos con ❌ son el argumento a favor de mover los **converters** (no los colores) de `App.xaml` a `Styles.xaml` — ahí sí, porque nadie los reescribe en runtime. La alternativa es `{x:Static}` también en esos dos usos.

Cuando estén los 18 modales, `RestarMargen` se puede sacar de `App.xaml`.

### Cómo verificar sin abrir Visual Studio

Instanciar el control con una `Application` de `Resources` vacío reproduce la condición exacta del diseñador y devuelve la `XamlParseException` con número de línea — que es más de lo que muestra la ventana Salida, donde solo aparece el `TaskCanceledException` del subrogado.

---

## Relaciones

- [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]] — por qué el lienzo quedaba en blanco
- [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] — la decisión y las alternativas descartadas
- [[TextoResponsivo]] — el helper de recorte y ToolTip
- [[Dialogo de confirmacion reusable]] — la advertencia al inactivar, que usa estos mismos estilos
- [[Módulo Productos]] — `ProductoModal` es la referencia de layout multi-columna
- [[WPF - StackPanel y columnas Auto no ceden espacio, no se achican de verdad]]
- [[Deuda Técnica - Pendientes]] — P-042, familia paralela de estilos en Pesaje
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] — origen de `LupaBtnOscuro` y de esta auditoría
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — reglas de dominio y derivación automática de `MaxLength`
- [[Validacion de formularios]] — patrón de validación en modales
- [[Sesión 2026-09-02 - Validación de longitud máxima en campos de texto]] — sesión de limpieza de `MaxLength` en XAML y alineación de dominio

