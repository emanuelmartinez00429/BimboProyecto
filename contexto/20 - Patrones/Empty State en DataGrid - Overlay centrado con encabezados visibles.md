---
title: "Empty State en DataGrid — Overlay centrado con encabezados visibles"
tags:
  - patron
  - ui
  - ux
  - wpf
  - datagrid
  - controls
date: 2026-09-02
lifecycle: verified
---

# Empty State en DataGrid — Overlay centrado con encabezados visibles

> [!abstract] Resumen del patrón
> Componente desacoplado (`EmptyStateOverlay`) colocado sobre el área de filas de un `DataGrid` de WPF que muestra un mensaje contextual con icono circular y tipografía atenuada cuando la consulta o filtro no devuelve registros, manteniendo intactos los encabezados de las columnas.

---

## Problema

1. **Ocultamiento por apilamiento XAML:**
   En WPF, si un `StackPanel` de estado vacío se declara en la misma celda de un `Grid` antes que un `DataGrid` con fondo blanco (`Background="White"`), el `DataGrid` se dibuja encima y tapa el mensaje, mostrando un espacio blanco vacío.
2. **Pérdida de contexto si se oculta la tabla:**
   Colapsar el `DataGrid` completo para mostrar un mensaje vacío oculta las columnas, privando al usuario de saber qué datos estructurados esperaba ver la pantalla.
3. **Mensajes genéricos poco informativos:**
   Un simple texto "Sin resultados" no indica si la tabla está vacía de origen, si la búsqueda no tuvo coincidencias o si el filtro de inactivos no tiene elementos.

---

## Solución Técnica

### 1. Control Reutilizable (`EmptyStateOverlay`)

Ubicación: `CapaUI/Core/Controls/EmptyStateOverlay.xaml` y `.xaml.cs`

- **Visual:** Marco circular suave (`Width="56" Height="56"` con borde `#E2E8F0` y fondo `#F8FAFC`), icono de documento/bandeja vectorial (`#94A3B8`), título semi-bold (`#475569`, 16px) y subtexto opcional.
- **`HeaderOffset` (default: 40px):** Margen superior aplicado al contenedor interno que compensa exactamente la altura de las cabeceras del `DataGrid`, logrando que el centro visual del mensaje coincida con el centro del cuerpo de filas disponible.
- **`IsHitTestVisible="False"`:** Permite que clicks o eventos de ratón atraviesen el overlay sin interferir con el grid o sus barras de desplazamiento.
- **`Panel.ZIndex="5"`:** Garantiza que el overlay se dibuje por encima del lienzo de filas del `DataGrid`.

### 2. Lógica Contextual en el ViewModel

El ViewModel evalúa dinámicamente la propiedad `MensajeSinResultados`:

```csharp
public bool NoResults => !IsLoading && _filteredCount == 0;

public string MensajeSinResultados
{
    get
    {
        if (IsLoading) return string.Empty;

        if (_estadoFiltro == EstadoFilter.Inactivos)
            return "No hay registros inactivos";

        if (_estadoFiltro == EstadoFilter.Activos && ActivosCount == 0 && TotalCount > 0)
            return "No hay registros activos";

        if (TieneFiltrosBusquedaActivos())
            return "No se encontraron resultados con los filtros actuales";

        return "No hay registros";
    }
}
```

Al utilizar `[NotifyPropertyChangedFor(nameof(NoResults), nameof(MensajeSinResultados))]` sobre `_isLoading`, ambas propiedades se actualizan automáticamente en cuanto finaliza cualquier petición asíncrona.

---

## Integración en XAML

```xml
<Grid>
    <!-- DataGrid normal (HeadersVisibility="Column", Background="White") -->
    <DataGrid Grid.Row="0" x:Name="DgEntidad" ... />

    <!-- Overlay en la misma fila con ZIndex superior -->
    <controls:EmptyStateOverlay Grid.Row="0"
                                Panel.ZIndex="5"
                                HeaderOffset="40"
                                Mensaje="{Binding MensajeSinResultados}"
                                EstaVacio="{Binding NoResults}"/>
</Grid>
```

---

## Relaciones

- [[Módulo Productos]]
- [[Anatomia compartida de los modales]]
- [[Panel de Filtros Fluido - Barra responsive con prioridad y equilibrado]]
