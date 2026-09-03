---
title: "Buscador y Barra de Herramientas Responsive — Protección de Ancho con AnchoMinimoAVisibilidad"
tags:
  - patron
  - wpf
  - ui
  - responsive
  - buscador
date: 2026-09-03
lifecycle: verified
---

# Buscador y Barra de Herramientas Responsive — Protección de Ancho con AnchoMinimoAVisibilidad

> [!abstract]
> Patrón estándar para evitar que el botón "Limpiar Filtros" comprima o reduzca el ancho útil del buscador controls:SuggestionSearchBox cuando la ventana se reduce o se trabaja en pantallas estrechas.

---

## El problema

En todas las pantallas de catálogo (Productos, Categorías, Fabricantes, Proveedores, Empleados, Usuarios, Bitácora, Presentaciones), la primera fila de la barra de herramientas (Toolbar) se compone de un Grid de 2 columnas:
- **Columna 0 (Width="*")**: El controls:SuggestionSearchBox (buscador con sugerencias y atajos).
- **Columna 1 (Width="Auto")**: El botón Limpiar Filtros (Style="{StaticResource ActionBtn}").

Si el botón Limpiar Filtros mantiene siempre su texto visible ("Limpiar Filtros" de ~130 px), en resoluciones pequeñas o ventanas reducidas le "come" ancho a la columna *, aplastando el buscador y haciendo que los placeholders y textos queden cortados.

---

## La solución: AnchoMinimoAVisibilidad

En CapaUI/Converters/AnchoMinimoAVisibilidadConverter.cs (registrado globalmente en App.xaml como AnchoMinimoAVisibilidad), se evalúa el ancho real (ActualWidth) del contenedor de la barra (TarjetaToolbar).

Cuando el ancho baja del umbral (760 px):
1. El TextBlock con el texto "Limpiar Filtros" se colapsa (Visibility.Collapsed).
2. El botón pasa automáticamente a modo icono (16x16 px).
3. El buscador en la columna * preserva el 100% del espacio disponible.
4. El botón conserva ToolTip="Limpiar filtros", de modo que la accesibilidad y comprensión visual nunca se pierden.

---

## Estructura XAML Canónica

`xml
<!-- TOOLBAR -->
<Border Grid.Row="2" x:Name="TarjetaToolbar" Background="White" CornerRadius="10"
        BorderBrush="#D8E1EC" BorderThickness="1" Padding="12" ClipToBounds="True">
    <Border.Effect>
        <DropShadowEffect Color="Black" BlurRadius="10" ShadowDepth="2" Opacity="0.06"/>
    </Border.Effect>
    <StackPanel>
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <!-- Buscador en columna flexible -->
            <controls:SuggestionSearchBox Grid.Column="0" Margin="0,0,10,0"
                x:Name="SearchBox"
                Query="{Binding Query, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                SuggestItems="{Binding SuggestItems}"
                HighlightIndex="{Binding HighlightIndex, Mode=TwoWay}"
                Placeholder="Buscar..."
                ItemSelected="SearchBox_ItemSelected"/>

            <!-- Botón responsivo: en ventana angosta oculta el texto y deja el icono -->
            <Button Grid.Column="1" Command="{Binding LimpiarFiltrosCommand}"
                    Background="{DynamicResource EmpresaPrimaryDarkBrush}"
                    Style="{StaticResource ActionBtn}"
                    Padding="16,0" Height="38" MinWidth="0"
                    ToolTip="Limpiar filtros">
                <StackPanel Orientation="Horizontal">
                    <Path Data="{StaticResource IconFilter}" Stroke="White" StrokeThickness="1.8"
                          StrokeLineJoin="Round" StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                          Width="16" Height="16" Stretch="Uniform" VerticalAlignment="Center"/>
                    <TextBlock Text="Limpiar Filtros" FontFamily="Segoe UI" FontSize="15.5" FontWeight="SemiBold"
                               Foreground="White" VerticalAlignment="Center" Margin="6,0,0,0"
                               Visibility="{Binding ActualWidth, ElementName=TarjetaToolbar, Converter={StaticResource AnchoMinimoAVisibilidad}, ConverterParameter=760}"/>
                </StackPanel>
            </Button>
        </Grid>
        ...
    </StackPanel>
</Border>
`

---

## Pantallas que implementan el patrón

| Pantalla | Vista | ElementName | Umbral |
|---|---|---|---|
| **Productos** | ProductosView.xaml | TarjetaToolbar | 760 |
| **Categorías** | CategoriasView.xaml | TarjetaToolbar | 760 |
| **Fabricantes** | FabricantesView.xaml | TarjetaToolbar | 760 |
| **Proveedores** | ProveedoresView.xaml | TarjetaToolbar | 760 |
| **Presentaciones** | PresentacionesView.xaml | TarjetaToolbar | 760 |
| **Empleados** | EmpleadosView.xaml | TarjetaToolbar | 760 |
| **Usuarios** | UsuariosView.xaml | TarjetaToolbar | 760 |
| **Bitácora** | BitacoraView.xaml | TarjetaToolbar | 760 |

---

## Relaciones

- [[Panel de Filtros Fluido - Barra responsive con prioridad y equilibrado]] — distribución de los combos y filtros inferiores en la toolbar
- [[Buscador Universal Bimbo]] — arquitectura del buscador general
- [[Módulo Productos]] — pantalla donde nació la necesidad del umbral
