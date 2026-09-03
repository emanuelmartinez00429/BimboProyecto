---
title: "Columna de Número de Fila (#) en DataGrid"
tags: [patron, wpf, datagrid, tablas, ui]
date: 2026-09-03
lifecycle: verified
---

# Columna de Número de Fila (#) en DataGrid

Primera columna `#` en las tablas de lista con la **posición visual** de cada fila.
No sale de la base ni de ningún campo del DTO: se calcula en tiempo de render.

> [!warning] Es una posición, no un identificador
> El número depende del **orden, el filtro y la página** actuales. El mismo
> registro recibe otro `#` si cambia cualquiera de esas cosas. El identificador
> estable sigue siendo la columna `CÓDIGO`/`Id`.

## Piezas (todas compartidas)

| Pieza | Ubicación |
|---|---|
| `NumeroFilaConverter` (`IMultiValueConverter`) | `CapaUI/Converters/NumeroFilaConverter.cs` |
| `x:Key="NumeroFilaConverter"` | `CapaUI/Resources/Styles.xaml` (sección "Conversores Compartidos") |
| `Style x:Key="TextoNumeroFila"` (gris `#9CA3AF`, 13px, centrado) | `Styles.xaml`, tras `TextoCeldaConFallback` |
| `DataTemplate x:Key="PlantillaCeldaNumeroFila"` | `Styles.xaml`, junto al style anterior |

## Cómo funciona

El converter recibe `values = [ item, DataGridRow, Page ]`:

- **`item`** (`<Binding/>` a secas): no se usa para calcular, pero al reciclarse el
  contenedor con virtualización (`VirtualizationMode="Recycling"`) el `MultiBinding`
  se vuelve a evaluar sólo porque este valor cambió. Sin él, el `#` se queda pegado
  al hacer scroll.
- **`DataGridRow`** (`RelativeSource AncestorType=DataGridRow`): `row.GetIndex()` da
  la posición 0-based en la página. `-1` (fila desconectada) → celda vacía.
- **`Page`** (`DataContext.Page` del `DataGrid`): número de página actual del
  ViewModel. Numeración **global**: `(Page - 1) * tamañoPágina + GetIndex() + 1`.
  Si el binding no resuelve (grilla sin paginación) degrada a página 1 → `1..N`.
- **`ConverterParameter`**: tamaño de página como string; default `50` (todas las
  VMs de lista usan `PageSize = 50`). Sólo se pasa si una vista difiere.

## Uso en una vista

Primera columna dentro de `<DataGrid.Columns>`:

```xml
<DataGridTemplateColumn Header="#" Width="40" CanUserResize="False" CanUserReorder="False"
                        CellStyle="{StaticResource CeldaCentrada}"
                        CellTemplate="{StaticResource PlantillaCeldaNumeroFila}"/>
```

Requisitos de la vista (los cumplen las 10 tablas de lista): `DataGrid` con
`AutoGenerateColumns="False"`, y el `DataContext` del `DataGrid` expone una
propiedad bindable `Page` (`RealtimeAwareViewModel` / VMs de lista).

## Dónde está aplicado

Como primera columna en: Productos, Presentaciones, Proveedores, Fabricantes,
Categorías, Empleados, Usuarios, Bitácora, Contactos Fabricantes (grilla master),
Contactos Proveedores (grilla master).

**No** está en: la grilla de detalle de Contactos (`DgContactos` — usaría el `Page`
del master, que no le corresponde) ni en `RolesView` (no pagina; grilla con otra
estructura). Para agregarlo ahí haría falta una plantilla sin el binding a `Page`.

## Relaciones

- [[Empty State en DataGrid - Overlay centrado con encabezados visibles]]
- [[Columnas de Auditoria Temporal en DataGrid - Estandarizacion Creado y Actualizado]]
- [[Paginación y Búsqueda - Arquitectura Detallada]]
- [[Arquitectura Actual]]
