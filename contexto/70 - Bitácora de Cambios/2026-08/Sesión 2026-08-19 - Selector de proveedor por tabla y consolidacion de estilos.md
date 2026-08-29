---
title: Sesión 2026-08-19 — Selector de proveedor por tabla y consolidación de estilos
type: sesion
status: vigente
tags:
  - sesion
  - pesaje
  - modales
  - estilos
date: 2026-08-19
updated: 2026-08-19
summary: "Sesión larga, un solo hilo: el campo Proveedor del paso 1 pasó de ComboBox precargado a SelectorCatalogoModal. Eso destapó dos bugs de layout, una duplicación de…"
scope:
  - CapaUI/Formularios/Principal/Pantallas/Pesaje
  - CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales
  - CapaUI/Resources
symbols:
  - AbrirProcesoModal
  - Action<FiltroItem>
  - Auto
  - CatCell
  - CatHeader
  - CatRow
  - CatalogoConfig
  - CeldaInputTabla
  - CerrarSelectorCatalogo
  - CheckBox
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente)
---

# Sesión 2026-08-19 — Selector de proveedor por tabla y consolidación de estilos

> [!success] Resultado
> Sesión larga, un solo hilo: el campo Proveedor del paso 1 pasó de `ComboBox` precargado a `SelectorCatalogoModal`. Eso destapó dos bugs de layout, una duplicación de estilos (auditoría completa de `PesajeModalStyles.xaml`), y terminó en un rediseño completo del paso 2 — modal cuadrado, tabla de productos como `DataGrid`, y reemplazo total de `SelectorProductosModal` por el mismo selector genérico, ahora con multiselección y filtro por proveedor. La multiselección salió rota en el primer intento (agregaba 1 de N marcados) — la causa y el fix están documentados en la sección 8, son el bug más importante de la sesión.

---

## Problema / motivo

Fernando pidió que el Proveedor de "Nuevo proceso de descarga" (paso 1) se seleccione con el mismo patrón de tabla+buscador que ya usa `ProductoModal` para Proveedor/Fabricante, en vez del `ComboBox` que traía toda la lista precargada desde `PesajeView`. El motivo de fondo: un `ComboBox` cargado eager puede quedar vacío si falla la consulta, y el usuario guarda sin darse cuenta de que el proveedor quedó en `null` — el mismo tipo de bug ya documentado para `FabricanteModal` en [[Sesión 2026-08-15 - Validacion centralizada y doble clic en catalogos]].

## Cambios aplicados

### 1. Migración del campo Proveedor (ComboBox → SelectorCatalogoModal)

- `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/ProcesoDescargaModal.xaml` / `.xaml.cs` — se sacó `CmbProveedor` y se agregó `TxtProveedor` (solo lectura) + botón lupa, abriendo `SelectorCatalogoModal` con `Catalogos.Proveedores(_catalogos)`. El constructor dejó de recibir `List<ProveedorItem> proveedores` por parámetro — ahora resuelve `ICatalogoRepository` vía `App.Services`, igual que `ProductoModal`.
- `CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeView.xaml.cs` — se eliminó `CargarProveedoresAsync()` (la carga eager de los 200 proveedores) y `AbrirProcesoModal` dejó de ser `async`.
- La precarga en modo Edición usa directo `CamionPesaje.Proveedor`/`IdProveedor` (ya vienen en el modelo) — no hace falta ninguna consulta extra al abrir para editar.
- El filtro "solo proveedores activos" se preserva: `ICatalogoRepository.GetProveedoresAsync` ya filtra por `EstadoRegistro.Activo`, mismo criterio que el `ProveedorFiltros{IdEstado=1}` que se sacó.

### 2. Bug encontrado al probar: selector transparente y sin tamaño fijo

Fernando probó en la app y reportó el selector transparente (se veía el overlay oscuro de fondo en vez de un marco sólido) y que la tabla cambiaba de tamaño al filtrar en vez de quedar fija con scroll.

**Causa raíz:** `SelectorCatalogoModal` es *"chromeless"* a propósito — no tiene fondo propio, está diseñado para reemplazar el contenido de un modal que ya tiene su marco degradado y su alto congelado (patrón `FormHost`/`SelectorHost` + `RootGrid.Height` fijado en `OnLoaded`, que usa `ProductoModal`). Yo lo había alojado en cambio en el `SelectorOverlay` oscuro de `ProcesoDescargaModal`, que es el patrón correcto para `SelectorProductosModal` (ese sí pinta su propio marco degradado completo — por eso flota centrado sobre un velo).

**Fix:** en `ProcesoDescargaModal.xaml`/`.xaml.cs`:
- El `Grid` raíz pasó a `x:Name="RootGrid"`, el `DockPanel` de contenido a `x:Name="ContenidoPrincipal"`.
- Nuevo `ContentControl x:Name="CatalogoSelectorHost"` como hermano del `DockPanel` dentro del mismo `RootGrid` — comparte el `Border` degradado de fondo, reemplaza el contenido en vez de superponerse.
- `BuscarProveedor_Click` congela `RootGrid.Height` al abrir (a diferencia de `ProductoModal`, que lo congela una sola vez para siempre porque es un formulario estático) y lo libera al cerrar (`CerrarSelectorCatalogo`) — el freeze es temporal porque el wizard sí necesita cambiar de alto entre paso 1 y paso 2, algo que `ProductoModal` no tiene que resolver.
- `SelectorProductosModal` (paso 2) quedó intacto con su overlay oscuro de siempre — es el patrón correcto para ese caso.

### 3. Duplicación de estilos: `LupaBtn`

Al agregar la lupa del nuevo campo, se detectó que `ProductoModal` ya tenía su propia copia local de `LupaBtn` (variante oscura), y que existía `LupaBtnCompartido` (variante clara, usada en `ReporteriaView`) en `CapaUI/Resources/Styles.xaml` — pero ninguna variante oscura centralizada. Se agregó `LupaBtnOscuro` al diccionario global y tanto `ProductoModal` como `ProcesoDescargaModal` la referencian ahora en vez de tener cada uno su copia.

### 4. Auditoría completa de `PesajeModalStyles.xaml`

A pedido de Fernando, se auditó el diccionario completo de Pesaje contra `CapaUI/Resources/Styles.xaml`:

- **`MIcoSearch`** — idéntico a `IconSearchShared` y sin ningún uso tras el cambio del punto 1 → **eliminado**.
- **`MCombo`** — sin ningún uso tras el cambio del punto 1 → **eliminado**.
- **`MInput`/`MCombo`/`MLabel`/`MSegBtn`** — casi-duplicados de `ModalInput`/`ModalCombo`/`EtiquetaCampo`/`ModalSegBtn`, pero con divergencia real (fuente más chica, sin aro de foco, sin borde rojo de validación). No se fusionaron a ciegas — podría ser una variante compacta deliberada del wizard, no un descuido. Quedó como [[Deuda Técnica - Pendientes|P-042]], pendiente de decisión explícita.
- Documentado todo en [[Anatomía compartida de los modales]] con una sección nueva ("Regla: antes de agregar un estilo local, buscá acá primero") para que no se repita.

### 5. Modal cuadrado (720×720) y layout con scroll acotado

Pedido aparte, sobre el mismo modal: que el marco se agrandara verticalmente hasta quedar cuadrado, que Observaciones (paso 1) se estirara para llenar el espacio ganado, y que en el paso 2 el scroll quedara acotado solo a la lista de productos (no a toda la sección).

- `ProcesoDescargaModal.xaml` — `Height="720"` fijo (antes se autoajustaba al contenido). El `ScrollViewer` único que envolvía las dos secciones se reemplazó por un `Grid` con tres filas nombradas (`RowSecCamion`, `RowSepCamion`, `RowSecProductos`): **solo una puede ser `*` a la vez** — si las dos lo fueran, el alto sobrante se repartiría 50/50 entre la sección visible y la colapsada.
- `SecCamion` pasó de `StackPanel` a `Grid` propio: la fila de `TxtObs` es `Height="*" MinHeight="56"` — se estira cuando su fila padre es `*` (paso 1 solo), y cae a su `MinHeight` cuando el padre es `Auto` (modo edición, con Productos pidiendo el `*`). Hizo falta `Height="NaN"` local en el `TextBox`: el `Setter Height="36"` del estilo `MInput` le gana a cualquier `*` del contenedor si no se pisa explícitamente.
- `SecProductos` también pasó a `Grid`: título/ayuda fijos arriba, la lista en una fila `*` con su propio scroll, y "Agregar producto" fijo abajo — nunca se desplaza aunque haya muchos productos.
- `ConfigurarFilas(camionEstrella, productosEstrella)` en el code-behind alterna cuál fila es `*` según el paso (wizard) o dice que Productos se lleve todo el sobrante (edición, con Camión a su alto natural).
- El freeze temporal de `RootGrid.Height` (sección 2) quedó como no-operación inofensiva: con el modal ya fijo en 720, `RootGrid.ActualHeight` siempre es ~720, congelarlo no cambia nada — se dejó igual por si el freeze vuelve a hacer falta en otro contexto.

### 6. Reemplazo completo de `SelectorProductosModal` por `SelectorCatalogoModal`

Al ver el paso 2 más de cerca ("en vez de usar esta tabla rara del paso dos podemos usar la que usamos en el paso 1 y con los demás modales"), se decidió retirar `SelectorProductosModal` (el picker de productos, autocontenido, con su propio `DataGrid` de columnas en píxeles fijos) y usar el mismo `SelectorCatalogoModal` que ya elige el Proveedor — mismo patrón "chromeless" de la sección 2.

- `SelectorProductosModal.xaml` / `.xaml.cs` — **borrados**. Quedaron sin ningún consumidor.
- `ProcesoDescargaModal.xaml` — se sacó el overlay oscuro `SelectorOverlay`/`SelectorHost` (ya sin uso); `AgregarProducto_Click` ahora abre `SelectorCatalogoModal` a través del mismo `AbrirSelectorCatalogo(cfg, alSeleccionar)` que usa el Proveedor.
- Como consecuencia directa se perdieron dos comportamientos del picker viejo que hubo que reconstruir en el genérico (secciones 7 y 8): el filtro "Solo <Proveedor>" y el atenuado visual de productos ya agregados.
- `SelectorProductosModal` quedaba **más ancho** (`Width="820"`) que el marco de 720 que ahora lo aloja, y su `MaxWidth`/`MaxHeight` estaban atados al `Border` ancestro más cercano — que al estar anidado dentro de `ProcesoDescargaModal` resolvía al overlay del propio modal (no al overlay de nivel app), y el clamp resultante rompía su layout interno (buscador y toggle superpuestos). El reemplazo por `SelectorCatalogoModal`, que se estira para llenar exactamente su host, resolvió esto de raíz — no hizo falta arreglar el binding roto porque el control entero se retiró.

### 7. Productos acotados por proveedor: puente en 2 pasos

`SelectorProductosModal` acotaba la búsqueda al proveedor elegido; para no perder esa utilidad al migrar, se extendió el catálogo genérico de Productos con el mismo filtro.

- `productos` no tiene columna `id_proveedor` propia — el vínculo es `producto → fabricante → proveedor`. `PickerProductoRepository.FabricantesDeProveedor` (Pesaje) ya resolvía esto con un puente en 2 pasos: primero los ids de fabricante del proveedor, después `id_fabricante IN (...)`. Se replicó el mismo patrón (comentado a propósito como copia deliberada, para no acoplar los dos repositorios) en `CatalogoRepository.GetProductosAsync`.
- `ICatalogoRepository.GetProductosAsync` y `Catalogos.Productos(r, idProveedor, permiteMultiple, estaYaElegido)` ganaron los parámetros nuevos — mismo patrón de encadenamiento que `Catalogos.Fabricantes(r, idProveedor)`.
- Se perdió el toggle "Todo el catálogo" que tenía el picker viejo: ahora el acotamiento es fijo, sin escape hatch — igual que `Fabricantes` tampoco lo tiene.

### 8. Multiselección en `SelectorCatalogoModal` — se agregó rota, se diagnosticó y se corrigió

Para poder agregar varios productos de una sola apertura del selector ("un solo click seleccionar varios y traerlo de un solo"), se le agregó a `SelectorCatalogoModal` (componente compartido, no solo Pesaje):

- `CatalogoConfig` ganó `PermiteMultiple`, `EstaYaElegido` (predicado `Func<int?, bool>` para atenuar filas ya elegidas en otro lado) y `DescripcionPrimero` (Código antes que Nombre, solo Productos).
- Primera versión: cada fila (`FilaCatalogo`, clase privada anidada) tenía su propio `bool Marcado`, y `Confirmar()` los leía enumerando `Dg.ItemsSource` en el momento de confirmar.

**Bug:** se tildaban 3 productos y "Seleccionar" agregaba **uno solo** — justo el resaltado. Causa raíz real (diagnosticada con un agente dedicado a refutar hipótesis antes de tocar código, no supuesta): el estado "marcado" vivía en objetos `FilaCatalogo` que se destruyen y recrean en cada refiltrado (`ICollectionView.Filter` en modo memoria) y en cada página (modo servidor) — `Confirmar()` leía la vista *actual*, no lo que el usuario había tildado antes. Cuando el conteo de marcados visibles caía a 0, el código caía a un *fallback* de selección simple que agregaba la fila resaltada — y esa fila coincidía con la última tildada porque clickear un `CheckBox` dentro de una `DataGridCell` también dispara la selección de su fila (el handler de `MouseLeftButtonDown` de `DataGridCell` se registra con `handledEventsToo: true`).

Segundo bug, latente (no se veía en el repro porque el `foreach` de emisión ya estaba materializado con `.ToList()`, pero mordía apenas se corrigiera el primero): los tres consumidores del evento `Seleccionado` (`ProcesoDescargaModal`, `ProductoModal`, `ReporteriaView`) cerraban el selector **dentro** del handler de la primera invocación — con selección múltiple eso dispone el control a mitad de una emisión de N ítems.

**Fix:**
- `SelectorCatalogoModal` ahora tiene `Dictionary<int, FiltroItem> _marcados`, dueño único de la verdad — sobrevive a refiltrados, cambios de página y al repintado por revalidación de caché (`CrearFila` siembra el tilde desde el diccionario).
- `Marcado_Changed` lee el estado directo del `CheckBox` (no de la fila) para no depender del orden del write-back del binding.
- `Confirmar()` emite todo lo marcado y **recién después** dispara `Cerrado?.Invoke()` — el selector decide cuándo cerrar, no el host. Los tres hosts dejaron de cerrar dentro del handler de `Seleccionado`.
- Doble clic y Enter quedan deshabilitados como atajo de "elegir solo esta" **en modo múltiple**: antes descartaban en silencio lo que el usuario venía tildando.
- Contador ("N marcados") + botón "Limpiar marcas" en el pie — necesarios porque las marcas ahora sobreviven a la búsqueda y a la paginación, así que puede haber marcados invisibles (filtrados fuera de la vista actual) si no se avisa.
- `EstaYaElegido` (productos que ya están en la carga): fila atenuada, sin checkbox, `IsHitTestVisible="False"` — no se puede ni tocar. Se re-evalúa en `Confirmar()` por si el estado cambió desde que se creó la fila, no se confía en el snapshot.
- Pendiente, documentado y no bloqueante: en modo múltiple no se puede marcar con teclado (`Space` no llega al `CheckBox` desde el foco de la fila). Ver [[Deuda Técnica - Pendientes|P-044]].

### 9. Fixes menores encontrados probando en vivo

- **"DESCRIPCIÓN" mostraba el RTN (Proveedores) o el código (Productos).** `FiltroItem.Descripcion` es un campo genérico que cada catálogo reusa para algo distinto — se agregó `TituloDescripcion` a `CatalogoConfig` para que el header diga qué es de verdad.
- **El cursor de Observaciones se centraba en vez de ir arriba.** El `ControlTemplate` de `MInput` tenía `VerticalAlignment="Center"` fijo en el `PART_ContentHost`, ignorando `VerticalContentAlignment` sin importar qué se le pusiera — se ató con `TemplateBinding`. **`ModalInput`/`InputBox` (los globales) tienen el mismo bug, sin corregir** — ver [[Deuda Técnica - Pendientes|P-043]].
- **El pie del selector desbordaba el marco.** El hint más largo del modo múltiple ("Marcá varios y tocá Seleccionar...") no se envolvía dentro de un `DockPanel LastChildFill="False"` y empujaba los botones fuera del modal — se cambió a `Grid` con columnas `*`/`Auto` + `TextWrapping="Wrap"`.
- **Tabla de productos rediseñada como `DataGrid` compacto**, reciclando los mismos estilos que la tabla del selector — promovidos `CatRow`/`CatCell`/`CatHeader` (antes locales a `SelectorCatalogoModal.xaml`) a `Styles.xaml` global como `TablaCatalogoFila`/`TablaCatalogoCelda`/`TablaCatalogoHeader`. Columnas Peso/Bultos editables con placeholder ("0.00"/"0", vía nuevo `VacioAVisibilidadConverter`) y un botón de tacho de basura en vez de una pastilla de texto "Quitar". El placeholder tuvo un desajuste de 1px con el cursor real (`Margin` del `TextBlock` vs. `Margin+BorderThickness+Padding` del `TextBox`) — corregido en una vuelta aparte.
- **Filas alternas invisibles en las columnas editables.** El estilo `MInput` (fondo blanco opaco, borde blanco translúcido pensado para el modal oscuro) tapaba la franja alterna de la fila. Nuevo estilo `CeldaInputTabla`, local a `ProcesoDescargaModal.xaml`, pensado para fondo claro: fondo transparente, borde gris sólido.

## Verificación

- `dotnet build BimboProyecto.sln` — 0 errores `CS####` en cada ronda (los únicos errores intermedios fueron `MSB3027`/`MSB3021` por tener la app abierta probándola, no del código; se repitieron varias veces a lo largo de la sesión).
- `dotnet test` sobre `BimboProyecto.Tests` — 3/3 correctas, repetido después de cada tanda de cambios (sin tests específicos de Pesaje; solo confirma que no hay regresión en el resto).
- Prueba manual de Fernando en la app, iterativa (screenshots en el chat, no archivados acá): confirmó y describió cada bug de esta nota a medida que aparecía — transparencia/tamaño del selector de Proveedor (sección 2), el layout roto del picker de productos viejo (sección 6), el header "Descripción" incorrecto, el cursor mal alineado de Observaciones, el desborde del pie del selector, la falta de indicación de "ya agregado" en la tabla de productos, y el bug de multiselección que agregaba 1 de N (sección 8) — corregido con diagnóstico dedicado antes de tocar código.

## Lo que NO cambió

> [!warning] Esta sección se corrigió a mitad de sesión
> La primera versión decía que `SelectorProductosModal` no se tocaba — eso dejó de ser cierto en la sección 6, donde se borró entero. Se deja la nota acá para que quien lea el historial de git no se confunda con una versión vieja de este archivo.

- `ProveedorItem` — se mantiene como tipo interno de Pesaje; se mapea desde `FiltroItem` en el único punto de selección en vez de reemplazarlo en todo el modal.
- `PesajeViewModel.GuardarProcesoAsync` — sin cambios de firma ni de comportamiento.
- `MInput`/`MCombo`/`MLabel`/`MSegBtn` de Pesaje — **no** se fusionaron con sus equivalentes globales; ver [[Deuda Técnica - Pendientes|P-042]].
- `GhostBtn`/`SolidBtn`/`CloseBtn` de Pesaje — no duplican nada global (no existe una versión centralizada de estos), así que no forman parte de esta consolidación. Coinciden de nombre con estilos locales de `ContactosProveedoresView`/`ContactosFabricantesView`, pero son familias visuales distintas (oscura vs clara) sin colisión real — no se tocaron.
- El evento público `Seleccionado` de `SelectorCatalogoModal` sigue siendo `Action<FiltroItem>` (un ítem por invocación) — la multiselección (sección 8) lo invoca N veces en vez de cambiar la firma a una lista. Decisión deliberada para no tocar los tres hosts más de lo necesario.
- El toggle "Todo el catálogo" que tenía el picker de productos viejo no se repuso — el acotamiento por proveedor ahora es fijo (sección 7).

---

## Relaciones

- [[Módulo Pesaje]]
- [[Anatomía compartida de los modales]] — tabla de estilos globales, actualizada esta sesión
- [[Selector de Catálogo - Selector genérico y multiselección]] — referencia del componente que terminó cargando todo el peso de esta sesión
- [[Deuda Técnica - Pendientes]] — P-042, P-043, P-044
- [[Sesión 2026-08-15 - Validacion centralizada y doble clic en catalogos]] — el bug de asimetría de `ComboBox` que motivó este cambio
- [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]] — origen de `ProcesoDescargaModal`
