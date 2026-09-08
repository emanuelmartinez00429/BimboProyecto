---
title: "WPF — StackPanel y columnas Auto no ceden espacio, no se achican de verdad"
tags:
  - wpf
  - layout
  - referencia
  - trampa
date: 2026-08-14
lifecycle: verified
---

# WPF — `StackPanel` y columnas `Auto` no ceden espacio, no se achican de verdad

> [!danger] Se repitió 4 veces en una sola sesión (2026-08-14)
> Título del header de Productos, buscador de la barra superior, botones de ventana (min/max/close), píldora de búsqueda — los cuatro se "cortaban" en vez de achicarse, con distintos síntomas visuales pero la **misma** causa raíz. Si algo en este proyecto "se ve cortado" en vez de reducirse con la ventana, sospechar de esto primero.

## Los tres mecanismos, cada uno con su síntoma

### 1. `StackPanel` con `Orientation="Horizontal"` mide a sus hijos con ancho infinito

Un `StackPanel` horizontal, al medir cada hijo, le pasa `availableSize.Width = Infinity` (porque en teoría puede "seguir apilando" sin límite). El hijo nunca se entera de que hay menos espacio del que pide, así que reporta su tamaño natural — y si ese tamaño es mayor al que el `StackPanel` termina recibiendo de SU padre, el contenido se recorta contra el borde exterior (de la ventana, de una `Border`, de lo que sea) en vez de reducirse.

**Síntoma:** texto cortado a la mitad de una palabra/letra, sin ningún `TextTrimming` en juego — porque el `TextBlock` ni siquiera llegó a saber que tenía que recortar.

**Fix:** cambiar el `StackPanel` por un `Grid` con columnas `Auto`/`*`, o un `DockPanel`. Ninguno de los dos mide a sus hijos con ancho infinito — cada uno recibe el espacio real.

```xml
<!-- ❌ el TextBlock nunca sabe que tiene menos de 400px -->
<StackPanel Orientation="Horizontal">
    <Border Width="38".../>
    <TextBlock Text="Título largo que puede no entrar"/>
</StackPanel>

<!-- ✅ la columna "*" le da un ancho real; TextTrimming ya puede actuar -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>
    <Border Grid.Column="0" Width="38".../>
    <TextBlock Grid.Column="1" Text="Título largo que puede no entrar"
               TextTrimming="CharacterEllipsis"/>
</Grid>
```

### 2. `Width="Auto"` + `MaxWidth` en una columna de `Grid` no es lo mismo que "se achica hasta caber"

Una columna `Auto` **siempre** toma el ancho natural/deseado de su contenido. `MaxWidth` en esa columna solo baja el **techo** — nunca agrega un **piso adaptable** que reaccione a cuánto espacio hay de verdad disponible. Si el contenido "quiere" 350px y solo hay 200 disponibles, la columna `Auto` igual reclama sus 350 (o el `MaxWidth` si es menor), y como ninguna otra columna `Auto` de un `Grid` le presta ese faltante a otra (así funciona el algoritmo de `Grid`: las `Auto` son todas igual de rígidas, sin importar el orden), el sobrante se dibuja fuera del borde real del contenedor.

**Síntoma:** un control con forma reconocible (una píldora redondeada, un `Border`) que aparenta tener ancho de sobra, pero su contenido/borde se corta abruptamente contra el borde de la ventana — no se ve un recorte suave, se ve como si la ventana "tapara" el resto.

**Fix:** usar `Width="*"` en la columna (con `MaxWidth` si hace falta un techo) — **no** `Width="Auto"`. Solo las columnas `*` participan del algoritmo de compresión real de `Grid` (piden hasta su `MaxWidth`, pero ceden de verdad cuando hay menos). El elemento adentro necesita `HorizontalAlignment="Stretch"` (no `"Left"`) para que realmente ocupe lo que la columna ya calculó — con `Stretch` + `MaxWidth` puesto en el ELEMENTO en vez de en la columna, WPF centra el elemento en el sobrante en vez de alinearlo, que es un bug relacionado pero distinto (ver nota abajo).

```xml
<!-- ❌ Auto + MaxWidth: techo, no piso -->
<ColumnDefinition Width="Auto" MaxWidth="480"/>
<Border HorizontalAlignment="Left" .../>

<!-- ✅ "*" + MaxWidth: participa de la compresión real -->
<ColumnDefinition Width="*" MaxWidth="480"/>
<Border HorizontalAlignment="Stretch" .../>
```

> [!tip] `MaxWidth` en el elemento vs. en la columna — no son intercambiables
> Si el `MaxWidth` va en el **elemento** (no en la `ColumnDefinition`) y el elemento es `HorizontalAlignment="Stretch"` dentro de una columna `*` que le da MÁS espacio del que su `MaxWidth` permite, WPF **centra** el elemento en ese espacio sobrante en vez de alinearlo al borde que uno esperaría. Poner el `MaxWidth` en la `ColumnDefinition` en vez de en el elemento evita esto — la columna queda genuinamente del tamaño del `MaxWidth` (o menos), y `Stretch` la llena entera sin sobrante que centrar.

### 3. Ningún elemento de `Grid`/`StackPanel` tiene "prioridad" por posición — `DockPanel` sí

En un `Grid`, todas las columnas `Auto` son igual de rígidas sin importar en qué orden aparecen — la última no cede ante la primera, ni viceversa. Si el contenido total (todas las `Auto` sumadas) supera el ancho real disponible, lo que se sale del borde es lo que **quede más a la derecha en el arreglo final**, no necesariamente lo menos importante.

**Síntoma:** en una barra con varios grupos de controles (marca, acciones, controles de ventana), los elementos del extremo — típicamente los botones de minimizar/maximizar/cerrar, por estar last-in-source-order — son los primeros en desaparecer al angostar la ventana, aunque sean justo los que **nunca** deberían ocultarse.

**Fix:** `DockPanel`. A diferencia de `Grid`, cada hijo `Dock="Right"` (o `"Left"`) reclama su porción del borde correspondiente **en el orden en que se declara**, contra el rectángulo que quedaba libre hasta ese momento — el primero en la lista tiene prioridad garantizada sobre los que vienen después. Poner lo que nunca debe ocultarse como el **primer** `Dock="Right"` (justo después de lo que va `Dock="Left"`), y lo que sí puede ceder espacio (con su propia lógica de `Visibility` condicional) después, en orden de importancia decreciente. El último hijo, sin `Dock`, es el que llena lo que sobra (`LastChildFill="True"`) — el equivalente a la columna `"*"` de siempre.

```xml
<DockPanel LastChildFill="True">
    <Border DockPanel.Dock="Left" .../>                 <!-- marca -->
    <StackPanel DockPanel.Dock="Right">...</StackPanel>  <!-- SIEMPRE visible: prioridad 1 -->
    <StackPanel DockPanel.Dock="Right">...</StackPanel>  <!-- puede ocultarse: prioridad 2 -->
    <Grid>...</Grid>                                     <!-- llena el resto -->
</DockPanel>
```

---

## Variante en `DataGrid`: columnas `Auto` que saltan al scrollear

Una columna de `DataGridTemplateColumn`/`DataGridTextColumn` con `Width="Auto"`
(`DataGridLength.Auto`) se mide contra **ancho infinito** y toma el `DesiredSize` de
las celdas **realizadas** en ese momento. Con virtualización de UI
(`VirtualizingPanel.IsVirtualizing="True"`), al scrollear se realizan filas nuevas: si
una trae texto más largo que todo lo visto hasta ahí, la columna se **ensancha en
vivo** y empuja a la derecha a las columnas siguientes. `TextTrimming` no salva nada
porque la medición fue con ancho infinito.

**Síntoma:** "se corren todas las columnas de la nada" mientras se hace scroll
vertical — no al redimensionar la ventana.

**Fix:** ninguna columna de datos en `Auto`. Cada una a `Width="N*"` con su `MinWidth`
(las estrella se reparten el ancho de forma proporcional, sin mirar el contenido); las
de longitud constante (fechas) a px fijos. Cuando la suma de mínimos supera el
viewport aparece la barra horizontal — estable, sin salto. Aplicado a las 9 grillas en
[[Sesión 2026-09-08 - Salto de columnas al scrollear y sombras sobre listas virtualizadas]].

---

## Cómo diagnosticar rápido cuál de los tres es

1. ¿El texto se corta a mitad de palabra, sin ellipsis? → mecanismo 1 (`StackPanel` horizontal).
2. ¿Un control con forma reconocible se ve "tapado" bruscamente por el borde de la ventana, aunque debería tener espacio de sobra? → mecanismo 2 (`Auto` + `MaxWidth` en vez de `*` + `MaxWidth`).
3. ¿Lo que se corta es siempre lo mismo (el elemento más a la derecha/abajo del arreglo), sin importar qué tan angosta esté la ventana? → mecanismo 3 (falta un `DockPanel` con prioridad explícita).

---

## Relaciones

- [[Panel de Filtros Fluido - Barra responsive con prioridad y equilibrado]] — panel custom escrito porque ni `WrapPanel` ni `UniformGrid` resuelven esto solos para una barra de filtros
- [[WPF - Bucle de Layout por Medir en ArrangeOverride]] — otro gotcha de measure/arrange de WPF, mecanismo distinto
- [[Módulo Productos]] — header y barra de filtros donde se encontraron los casos 1 y 3
- [[Sesión 2026-09-08 - Salto de columnas al scrollear y sombras sobre listas virtualizadas]] — la variante `DataGrid` + virtualización
