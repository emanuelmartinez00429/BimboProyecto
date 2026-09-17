---
title: "Sesión 2026-09-16 — Icono vectorial IcoFiltro y componente reutilizable BotonLimpiarFiltros"
date: 2026-09-16
tags:
  - bitacora
  - sesion
  - wpf
  - ui
  - ux
  - controles
  - componentes
  - filtros
  - estilos
aliases:
  - Componente BotonLimpiarFiltros
  - Icono IcoFiltro Material Symbols
---

# Sesión 2026-09-16 — Icono vectorial IcoFiltro y componente reutilizable BotonLimpiarFiltros

## Resumen

En esta sesión se completó la modernización de la iconografía de filtros y la refactorización arquitectónica del botón "Limpiar Filtros". Se incorporó al diccionario global de recursos la geometría oficial de Material Symbols (`IcoFiltro`) a partir del archivo SVG original `D:\Proyectos\Iconos\Filtros.svg`. Asimismo, se encapsuló la lógica y presentación del botón en un nuevo control reutilizable de WPF (`BotonLimpiarFiltros`), eliminando más de 120 líneas de código duplicado a través de 8 módulos del sistema y preservando la regla responsiva de colapso de texto por debajo del umbral de 760px.

---

## Intervenciones Realizadas

### 1. Vectorización e Integración de `IcoFiltro` en [`Styles.xaml`](../../../CapaUI/Resources/Styles.xaml)
- A partir de `D:\Proyectos\Iconos\Filtros.svg` (Material Symbols *filter_alt*, coordenadas `viewBox="0 -960 960 960"`), se integró el recurso `IcoFiltro` con `FillRule="Nonzero"` y congelamiento de alto rendimiento (`po:Freeze="True"`), siguiendo la misma convención que los iconos del menú lateral (`IcoInventario`, `IcoScale`, etc.).
- Al tratarse de un glifo sólido relleno (embudo), se renderiza mediante `Fill="White"` garantizando bordes nítidos y uniformidad con la estética corporativa.

### 2. Creación del Componente Reutilizable [`BotonLimpiarFiltros`](../../../CapaUI/Core/Controls/BotonLimpiarFiltros.xaml)
Se creó el control de usuario en `CapaUI.Core.Controls`:
- **Presentación XAML (`BotonLimpiarFiltros.xaml`):**
  - Botón base con estilo `ActionBtn`, fondo corporativo `EmpresaPrimaryDarkBrush`, altura fija de `38px`, `MinWidth="0"` y `Padding="16,0"`.
  - Icono `IcoFiltro` centrado a `16×16 px`.
  - `TextBlock` ("Limpiar Filtros") con tipografía `Segoe UI` SemiBold de 15.5px.
- **Lógica en Code-Behind (`BotonLimpiarFiltros.xaml.cs`):**
  - DependencyProperties: `Command`, `CommandParameter`, `Texto`, `ToolTipTexto`, `ReferenciaAncho` y `UmbralAncho` (default 760px).
  - **Convención automática de comando:** Si no se especifica explícitamente en el XAML, resuelve automáticamente por reflexión la propiedad `LimpiarFiltrosCommand` del `DataContext`.
  - **Manejo responsivo limpio:** Se suscribe al evento `SizeChanged` de `ReferenciaAncho` y evalúa el ancho en `Loaded`, alternando la visibilidad del texto sin fugas de memoria al limpiar la suscripción en `Unloaded`.

### 3. Reemplazo y Limpieza en los 8 Módulos de Catálogo
Se sustituyó el bloque duplicado de 15 líneas por la llamada declarativa concisa `<controls:BotonLimpiarFiltros Grid.Column="1" ReferenciaAncho="{Binding ElementName=TarjetaToolbar}"/>` en:
- [`ProductosView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml)
- [`CategoriasView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriasView.xaml)
- [`FabricantesView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricantesView.xaml)
- [`ProveedoresView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedoresView.xaml)
- [`PresentacionesView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionesView.xaml)
- [`EmpleadosView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadosView.xaml)
- [`UsuariosView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml)
- [`BitacoraView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Bitacora/BitacoraView.xaml)

### 4. Actualización en [`RolesResources.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Roles/RolesResources.xaml) y [`RolesView.xaml`](../../../CapaUI/Formularios/Principal/Pantallas/Roles/RolesView.xaml)
- Se actualizó la geometría `GeoFiltros` con el nuevo path del embudo sólido.
- Se adaptó el `Path` del botón de Roles para renderizar con `Fill="{TemplateBinding Foreground}"`, conservando su comportamiento único de deshabilitado condicional (`IsEnabled="{Binding Filtrando}"`).

---

## Verificación

- **Compilación de la Solución:** `dotnet build BimboProyecto.sln` finalizado exitosamente con **0 errores y 0 advertencias**.
- **Pruebas Unitarias:** Ejecución de la suite completa en `BimboProyecto.Tests` con **435 pruebas superadas (0 fallos, 0 omitidas)**.
- **Responsividad:** Verificado el colapso del texto por debajo de 760px preservando el ToolTip accesible en todo momento.
