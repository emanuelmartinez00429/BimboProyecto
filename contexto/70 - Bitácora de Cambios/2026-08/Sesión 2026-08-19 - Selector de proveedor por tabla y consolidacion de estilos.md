---
title: "Sesión 2026-08-19 — Selector de proveedor por tabla y consolidación de estilos"
tags:
  - sesion
  - pesaje
  - modales
  - estilos
date: 2026-08-19
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Fernando (agente)
---

# Sesión 2026-08-19 — Selector de proveedor por tabla y consolidación de estilos

> [!success] Resultado
> El campo Proveedor del paso 1 de `ProcesoDescargaModal` pasó de `ComboBox` precargado a `SelectorCatalogoModal` (tabla paginada con buscador), igual que el resto de los modales. En el camino aparecieron dos bugs de layout (transparencia y tamaño no fijo) por reusar el patrón de hosting equivocado, y una duplicación de estilos que llevó a auditar `PesajeModalStyles.xaml` completo contra el diccionario global.

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

## Verificación

- `dotnet build BimboProyecto.sln` — 0 errores `CS####` en cada iteración (los únicos errores que aparecieron fueron `MSB3027`/`MSB3021` por tener la app abierta probándola, no del código).
- `dotnet test` sobre `BimboProyecto.Tests` — 3/3 correctas (sin tests específicos de Pesaje; solo confirma que no hay regresión en el resto).
- Prueba manual de Fernando en la app: confirmó el bug de transparencia/tamaño (corregido en el punto 2) y no reportó nada más después del fix.

## Lo que NO cambió

- `SelectorProductosModal` (paso 2, selección de productos) — sin tocar, sigue con su overlay oscuro autocontenido.
- `ProveedorItem` — se mantiene como tipo interno de Pesaje; se mapea desde `FiltroItem` en el único punto de selección en vez de reemplazarlo en todo el modal.
- `PesajeViewModel.GuardarProcesoAsync` — sin cambios de firma ni de comportamiento.
- `MInput`/`MCombo`/`MLabel`/`MSegBtn` de Pesaje — **no** se fusionaron con sus equivalentes globales; ver P-042.
- `GhostBtn`/`SolidBtn`/`CloseBtn` de Pesaje — no duplican nada global (no existe una versión centralizada de estos), así que no forman parte de esta consolidación. Coinciden de nombre con estilos locales de `ContactosProveedoresView`/`ContactosFabricantesView`, pero son familias visuales distintas (oscura vs clara) sin colisión real — no se tocaron.

---

## Relaciones

- [[Módulo Pesaje]]
- [[Anatomía compartida de los modales]] — tabla de estilos globales, actualizada esta sesión
- [[Deuda Técnica - Pendientes]] — P-042
- [[Sesión 2026-08-15 - Validacion centralizada y doble clic en catalogos]] — el bug de asimetría de `ComboBox` que motivó este cambio
- [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]] — origen de `ProcesoDescargaModal`
