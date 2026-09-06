# Sesión 2026-09-06 — Cierre Integral de Deuda Técnica P-031 (G11) y P-042

## Contexto y Motivación
Esta sesión resolvió de forma definitiva dos ítems de deuda técnica pendientes en la aplicación:
1. **P-042**: Divergencia de estilos y ausencia de aros de foco / alineación de validación en `PesajeModalStyles.xaml` (`MInput` y `MSegBtn`), conservando su dimensionamiento compacto indispensable para los modales de Pesaje.
2. **P-031 (Hallazgo G11)**: El último hallazgo pendiente de P-031. Eliminación de recursos duplicados (iconos vectoriales, botones de acción, botones de segmentación y estilos de scrollbar) dispersos y re-parseados en 8 pantallas de catálogo y 2 de contactos, centralizándolos con inmutabilidad (`po:Freeze="True"`) en `Styles.xaml`.

---

## 1. Resolución P-042: Aros de Foco y Validación en Pesaje

### Diagnóstico y Decisión Técnica
- **Dimensionamiento:** Se ratificó mantener el tamaño compacto de `MInput` (`Height="36"`, `FontSize="13.5"`) y `MSegBtn` (`FontSize="13"`). La alta densidad de datos de pesaje (pesaje inicial, tara, bruto, neto, observaciones) en pantallas reducidas hace inviable el tamaño general (44px / 38px) sin desbordar el layout.
- **Interacción y Accesibilidad:**
  - `MInput` carecía del aro verde característico `#34D399` en foco (solo cambiaba tenuemente a borde blanco) y su trigger de error usaba `#FCA5A5` (1.5px). Se actualizó para usar aro de foco verde (`BorderBrush="#34D399"`, `BorderThickness="2"`) y trigger de error `#EF4444` (`BorderThickness="2"`), unificando el lenguaje visual con `ModalInput` y `CeldaInput`.
  - `MSegBtn` no contaba con trigger de foco para navegación accesible por teclado. Se le agregaron `BorderBrush="Transparent"`, `BorderThickness="2"` y trigger `IsFocused="True"` asignando `BorderBrush="#34D399"`, permitiendo recorrer las opciones de tipo de pesaje con la tecla `Tab` con feedback visible.

**Archivo modificado:**
- `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/PesajeModalStyles.xaml`

---

## 2. Resolución P-031 (G11): Centralización Global y Freeze de Recursos

### Causa del Freno de Rendimiento
Cada una de las 8 vistas de catálogo (`Productos`, `Categorias`, `Fabricantes`, `Proveedores`, `Presentaciones`, `Empleados`, `Usuarios`, `Bitacora`) y las 2 vistas secundarias de contactos redeclaraban en su `<UserControl.Resources>` copias idénticas de geometrías de iconos vectoriales SVG (`IconFilter`, `IconCheck`, `IconPlus`, `IconPencil`, `IconTrash`, `IconBox`, `IconUser`, etc.) y estilos de controles (`ActionBtn`, `SegmentBtn`, `ModernScrollBar`, `ModernScrollViewer`).

Esto provocaba:
1. Re-parseo redundante de decenas de árboles XAML en cada instanciación y navegación.
2. Múltiples instancias independientes de `Geometry` y `Brush` en memoria, imposibles de congelar en hilos secundarios.

### Solución Aplicada
1. **`Styles.xaml`**:
   - Se importó el namespace `xmlns:po="http://schemas.microsoft.com/winfx/2006/xaml/presentation/options"`.
   - Se aplicó `po:Freeze="True"` a los pinceles estáticos de la paleta institucional (`BrandDarkBrush`, `BrandLightBrush`, `AccentBrush`, etc.).
   - Se definieron e inmutabilizaron con `po:Freeze="True"` los 16 iconos vectoriales compartidos de la suite (`IconSearchShared`, `IconFilter`, `IconCheck`, `IconPlus`, `IconPencil`, `IconBox`, `IconTag`, `IconFactory`, `IconTruck`, `IconUser`, `IconLogout`, `IconToggle`, `IconHistory`, `IconTrash`, `IconArrowLeft`, `IconPerson`).
   - Se promovieron `ActionBtn`, `SegmentBtn`, `ModernScrollBar` y `ModernScrollViewer` como estilos globales accesibles por `DynamicResource` o `StaticResource`.

2. **Deduplicación en Vistas (reducción de casi 1,000 líneas redundantes)**:
   - `CategoriasView.xaml`: `<UserControl.Resources>` removido al 100%.
   - `FabricantesView.xaml`: `<UserControl.Resources>` removido al 100%.
   - `ProveedoresView.xaml`: `<UserControl.Resources>` removido al 100%.
   - `PresentacionesView.xaml`: `<UserControl.Resources>` removido al 100%.
   - `BitacoraView.xaml`: Iconos, `ActionBtn` y `ModernScrollBar` eliminados; se conservaron únicamente sus estilos específicos (`BitacoraRowStyle`, cabecera y selectores de fecha).
   - `ProductosView.xaml`: Iconos, `ActionBtn`, `SegmentBtn` y plantillas de scrollbar eliminados; se conservaron sus estilos específicos de filtros compuestos (`CeldaFiltro`, `EtiquetaFiltro`, `ComboFiltroBox`, `ProductCellTextStyle`).
   - `EmpleadosView.xaml` y `UsuariosView.xaml`: Iconos, `ActionBtn`, `SegmentBtn` y scrollbars eliminados; se conservaron únicamente sus estilos de fila específicos (`EmpleadoRowStyle`, `UserRowStyle`).
   - `ContactosFabricantesView.xaml` y `ContactosProveedoresView.xaml`: Iconos y `ActionBtn` eliminados; conservan `GhostBtn` e `IconBtn` locales.

---

## Verificación y Calidad

1. **Compilación completa:**
   ```bash
   dotnet build BimboProyecto.sln --no-incremental
   ```
   Resultado: `0 Advertencia(s), 0 Errores`.

2. **Pruebas Unitarias e Integración:**
   ```bash
   dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj
   ```
   Resultado: `Total: 286, Superado: 286, Con error: 0 (100% exitosas)`.

---

## Archivos Afectados
- `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/PesajeModalStyles.xaml`
- `CapaUI/Resources/Styles.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriasView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricantesView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedoresView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionesView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Bitacora/BitacoraView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Empleados/EmpleadosView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/ContactosFabricantes/ContactosFabricantesView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/ContactosProveedores/ContactosProveedoresView.xaml`
- `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`

---

## Relaciones
- [[Deuda Técnica - Pendientes]] — P-031 (11/11 hallazgos cerrados) y P-042 (cerrado)
- [[Anatomía compartida de los modales]]
