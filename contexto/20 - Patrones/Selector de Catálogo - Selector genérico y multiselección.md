---
title: "Selector de Catálogo — Selector genérico y multiselección"
tags:
  - patron
date: 2026-08-19
lifecycle: verified
---

# Selector de Catálogo — Selector genérico y multiselección

> [!abstract]
> `SelectorCatalogoModal` + `CatalogoConfig`/`Catalogos` es el selector "elegir un registro de una tabla" que usa todo el proyecto — Proveedor, Fabricante, Producto, Presentación, Tara, Categoría, Unidad, País. Usalo para cualquier campo nuevo de tipo "buscar y elegir de una lista", en vez de un `ComboBox` cargado eager o un picker a medida.

## Cuándo usarlo

- Un campo necesita que el usuario elija **un** registro (o varios) de una tabla que puede crecer — proveedores, fabricantes, productos, cualquier catálogo chico o mediano.
- Ya existe una consulta paginada/buscable en `ICatalogoRepository`, o se puede agregar una siguiendo el mismo patrón (`GetXxxAsync(termino, page, size, ct)` → `Result<PagedResult<FiltroItem>>`).
- **No** lo uses para un picker con lógica de negocio propia y compleja (ej. el viejo `SelectorProductosModal`, que hacía scoping por proveedor a mano) — extendé `CatalogoConfig` en cambio; ver "Cómo se implementa" abajo. Reconstruir un picker a medida duplica trabajo que el genérico ya resuelve (paginación, búsqueda, caché, teclado, ya-elegido, multiselección).

## Cómo se implementa

```csharp
// 1. El repositorio expone la consulta uniforme (CapaAplicacion4/Common/Catalogos/ICatalogoRepository.cs)
Task<Result<PagedResult<FiltroItem>>> GetProductosAsync(
    string termino, int page, int size, int? idProveedor = null, CancellationToken ct = default);

// 2. Una factory por catálogo (CapaUI/Core/Catalogos/CatalogoConfig.cs)
public static CatalogoConfig Productos(
    ICatalogoRepository r, int? idProveedor = null,
    bool permiteMultiple = false, Func<int?, bool>? estaYaElegido = null) => new(
    idProveedor is null ? "productos" : $"productos:{idProveedor}",
    "Seleccionar producto", "Buscar por ID, código o nombre...",
    (t, p, s, ct) => r.GetProductosAsync(t, p, s, idProveedor, ct),
    TituloDescripcion: "Código", DescripcionPrimero: true,
    PermiteMultiple: permiteMultiple, EstaYaElegido: estaYaElegido);

// 3. El modal que lo hospeda lo abre por el mismo patrón que ProductoModal/ProcesoDescargaModal
private void AbrirSelectorCatalogo(CatalogoConfig cfg, Action<FiltroItem> alSeleccionar)
{
    var selector = new SelectorCatalogoModal(cfg);
    selector.Cerrado += CerrarSelectorCatalogo;
    selector.Seleccionado += item => alSeleccionar(item);   // el selector cierra solo, ver abajo
    // ... hospedar en un ContentControl que comparta el marco del modal padre (es "chromeless")
}
```

### `CatalogoConfig` — todos los flags

| Parámetro | Default | Para qué |
|---|---|---|
| `MostrarDescripcion` | `true` | Oculta la columna de `Descripcion` si el catálogo no la necesita (ej. Tara). |
| `TituloDescripcion` | `"Descripción"` | `FiltroItem.Descripcion` es genérico — cada catálogo lo reusa para algo distinto (RTN en Proveedores, código en Productos, abreviatura en Unidades). El título de columna tiene que decir qué es de verdad. |
| `MostrarEstado` | `true` | Oculta ESTADO si el catálogo no tiene columna de estado (Tara, País). |
| `DescripcionPrimero` | `false` | Muestra Descripción antes que Nombre — Productos lo usa porque el código es lo que se escanea primero. |
| `PermiteMultiple` | `false` | Cambia el círculo de selección única por un `CheckBox` independiente por fila. Clickear varias las va sumando; "Seleccionar" las trae todas de una, invocando `Seleccionado` una vez por ítem marcado. |
| `EstaYaElegido` | `null` | `Func<int?, bool>` — las filas cuyo Id matchea salen atenuadas, sin checkbox, `IsHitTestVisible="False"`. Para "no dejes elegir de nuevo lo que ya está en otro lado" (ej. productos ya agregados a la carga). |

### Contrato de eventos — importante si vas a tocar el host

```csharp
public event Action? Cerrado;
public event Action<FiltroItem>? Seleccionado;
```

`Seleccionado` se dispara **una vez por ítem elegido** (1 en modo simple, N en modo múltiple) — la firma pública no cambia a lista para no tocar todos los consumidores. **El que decide cuándo cerrar es el selector, no el host**: `Confirmar()` emite todo lo que corresponda y recién después dispara `Cerrado`. El handler de `Seleccionado` en el host **no debe cerrar el selector** — si lo hace, con selección múltiple el control queda `Dispose()`-ado a mitad de la emisión de los N ítems restantes (ver el bug real documentado en [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]], sección 8). El patrón correcto:

```csharp
selector.Cerrado += CerrarSelector;                 // el único que cierra
selector.Seleccionado += item => alSeleccionar(item); // solo agrega, no cierra
```

### Hosting — "chromeless"

`SelectorCatalogoModal` no pinta fondo ni tiene alto propio: hereda el marco degradado del modal que lo aloja, reemplazando su contenido (no se superpone). Ver [[Anatomía compartida de los modales]] para el patrón completo de hosting (`RootGrid`/`ContenidoPrincipal`/`CatalogoSelectorHost`, freeze temporal de altura). Un consumidor que necesite un picker **autocontenido** (con su propio marco, flotando sobre un velo oscuro) es un patrón distinto — ver `SelectorProductosModal` como referencia histórica antes de que se retirara (borrado en la misma sesión que agregó `PermiteMultiple`, precisamente porque duplicaba lo que el genérico ya resolvía).

## Dónde está en el proyecto

- `CapaUI/Core/Controls/SelectorCatalogoModal.xaml(.cs)` — el control.
- `CapaUI/Core/Catalogos/CatalogoConfig.cs` — `CatalogoConfig` + la fábrica `Catalogos` (un miembro por catálogo).
- `CapaAplicacion4/Common/Catalogos/ICatalogoRepository.cs` / `CapaDatos/Repositories/Catalogos/CatalogoRepository.cs` — la consulta uniforme.
- Consumidores: `ProductoModal` (Presentación, Fabricante, Categoría, País, Proveedor), `ProcesoDescargaModal` (Proveedor con `EstaYaElegido`=false siempre, Producto con `PermiteMultiple` + `EstaYaElegido` + acotado por proveedor), `ReporteriaView` (Producto/Proveedor/Categoría, modo simple).

## Anti-patrones / qué evitar

- **No** copiar el `DataGridRow`/`DataGridCell`/`DataGridColumnHeader` de este selector a mano para otra tabla — están promovidos a `Styles.xaml` como `TablaCatalogoFila`/`TablaCatalogoCelda`/`TablaCatalogoHeader`, globales, pensados para cualquier tabla blanca sobre fondo oscuro (ver [[Anatomía compartida de los modales]]).
- **No** leer la selección desde `Dg.ItemsSource` / `Dg.SelectedItem` en un handler propio si estás extendiendo el selector — en modo memoria es una vista filtrada (no todo lo cargado), en modo servidor son instancias que se recrean por página. El acumulador (`_marcados`, indexado por Id) es la única fuente de verdad que sobrevive a eso.
- **No** cerrar el selector desde el handler de `Seleccionado` en un host nuevo — ver "Contrato de eventos" arriba.
- **No** construir un picker a medida (estilo `SelectorProductosModal`) para una necesidad que `CatalogoConfig` ya cubre con un flag — antes de agregar lógica de negocio a un modal nuevo, revisá si `PermiteMultiple`/`EstaYaElegido`/`DescripcionPrimero` ya resuelven el caso.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Anatomía compartida de los modales]] — patrón de hosting "chromeless" y los estilos de tabla compartidos
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] — origen de `PermiteMultiple`/`EstaYaElegido`/`DescripcionPrimero`, y el bug de multiselección diagnosticado y corregido
- [[Deuda Técnica - Pendientes]] — P-044 (multiselección no responde a teclado)
- [[Módulo Productos]] — `ProductoModal`, el consumidor original del selector en modo simple
