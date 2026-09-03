---
title: "Columnas de Auditoría Temporal en DataGrid — Estandarización Creado y Actualizado"
tags:
  - patron
  - ui
  - wpf
  - datagrid
  - auditoria
  - xaml
date: 2026-09-02
---

# Columnas de Auditoría Temporal en DataGrid — Estandarización Creado y Actualizado

Patrón estándar para la exposición de fechas y horas de auditoría (`created_at` y `updated_at`) en tablas de gestión y catálogos en WPF dentro de Bimbo Honduras.

---

## Principios del Patrón

1. **Ubicación consistente:**
   Las columnas `CREADO` y `ACTUALIZADO` se ubican de manera obligatoria de forma contigua, inmediatamente a la izquierda de la columna `ESTADO` (o antes de las columnas de acciones si no hay columna de estado).

2. **Alineación consistente a la derecha (Encabezado y Celdas):**
   - **Encabezado:** Debe usar obligatoriamente `HeaderStyle="{StaticResource HeaderDerecho}"` (`HorizontalAlignment="Right"` y `Padding="10,0"`) centralizado en `Styles.xaml`, para que el título de la columna quede alineado verticalmente al píxel con la información temporal.
   - **Celdas:** Alineación a la derecha (`HorizontalAlignment="Right"`, `CellStyle="{StaticResource CeldaDerecha}"` y `Padding="10,0"`), respetando las buenas prácticas de diseño de interfaz para datos temporales y numéricos.

3. **Dimensionamiento dinámico y seguro:**
   - Usar siempre `Width="Auto"` y `MinWidth="130"`.
   - `Width="Auto"` ajusta dinámicamente al contenido y la cabecera.
   - `MinWidth="130"` evita que la columna colapse a un ancho ilegible cuando la tabla esté vacía o durante la carga inicial.
   - *Nunca* usar cadenas como `SizeToDisplayedCells` (eso es de Windows Forms y causa `XamlParseException`).

4. **Conversión y Formateo:**
   - En la capa de datos/mapeo (`CapaDatos`), aplicar `.ToLocalTime()` al DateTime UTC proveniente de PostgreSQL (`timestamp with time zone`) para ajustarlo a la zona horaria de Honduras (`UTC-6`).
   - En XAML, aplicar `StringFormat={}{0:dd/MM/yyyy HH:mm}` y `TargetNullValue='—'`.

---

## Implementación de Referencia en XAML

```xml
<!-- CREADO -->
<DataGridTemplateColumn Header="CREADO"
                        Width="Auto"
                        MinWidth="130"
                        HeaderStyle="{StaticResource HeaderDerecho}"
                        CellStyle="{StaticResource CeldaDerecha}">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding CreatedAt, StringFormat={}{0:dd/MM/yyyy HH:mm}, TargetNullValue='—'}"
                       FontFamily="Segoe UI" FontSize="14" Foreground="#64748B"
                       HorizontalAlignment="Right" VerticalAlignment="Center"
                       Padding="10,0"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>

<!-- ACTUALIZADO -->
<DataGridTemplateColumn Header="ACTUALIZADO"
                        Width="Auto"
                        MinWidth="130"
                        HeaderStyle="{StaticResource HeaderDerecho}"
                        CellStyle="{StaticResource CeldaDerecha}">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding UpdatedAt, StringFormat={}{0:dd/MM/yyyy HH:mm}, TargetNullValue='—'}"
                       FontFamily="Segoe UI" FontSize="14" Foreground="#64748B"
                       HorizontalAlignment="Right" VerticalAlignment="Center"
                       Padding="10,0"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

---

## Implementación en Repositorio (`CapaDatos`)

En el mapeador de la entidad PostgREST al DTO:

```csharp
CreatedAt = entidad.createdAt?.ToLocalTime(),
UpdatedAt = entidad.updatedAt?.ToLocalTime(),
```

---

## Relaciones

- [[Conocimiento Principal]]
- [[Módulos de Catálogos Administrativos]]
- [[Módulo Productos]]
- [[Empty State en DataGrid - Overlay centrado con encabezados visibles]]
- [[Sesión 2026-09-02 - Columnas Creado y Actualizado en tablas de catálogo]]
