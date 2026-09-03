# Regla: Traducción de Conceptos a WPF e Idiomatic XAML

## Propósito
Prevenir errores de compilación y excepciones de parser XAML (`XamlParseException`) en tiempo de ejecución originados por transcribir literalmente términos procedentes de otros stacks tecnológicos (WinForms, Web, Móvil).

## Directiva Obligatoria
Al recibir peticiones con requerimientos o vocabulario funcional:

1. **Interpretar la intención técnica**:
   - Comprender qué resultado visual, de layout o de comportamiento solicita el usuario antes de escribir código.

2. **Mapear a la tecnología del proyecto (WPF / .NET 8 / XAML)**:
   - **Auto-ajuste de columnas de grillas**:
     - ❌ `DisplayedCells` (Constante de Windows Forms `DataGridViewAutoSizeColumnMode.DisplayedCells`)
     - ✅ `Width="Auto"` junto con `MinWidth="N"` (WPF `DataGridLength.Auto` que dimensiona a celdas y cabeceras sin colapsar).
   - **Alineación y Flexibilidad**:
     - ❌ `align-items`, `justify-content` (CSS)
     - ✅ `HorizontalAlignment`, `VerticalAlignment`, `DockPanel`, `StackPanel` o `Grid` (WPF).
   - **Visibilidad / Ocultamiento**:
     - ❌ `display: none`, `visible: false`
     - ✅ `Visibility="Collapsed"` o `Visibility="Hidden"` (WPF).
   - **Tipografía y Colores**:
     - ✅ Usar siempre los estilos y pinceles centralizados en `Styles.xaml` y recursos dinámicos corporativos (`EmpresaPrimaryBrush`).

3. **Validar contra el Type System y el Schema de XAML**:
   - Nunca inventar o asumir cadenas en propiedades XAML (`Width`, `Padding`, `Margin`, etc.) sin comprobar que el convertidor de tipos del framework (`DataGridLengthConverter`, `ThicknessConverter`, etc.) las soporta oficialmente.
