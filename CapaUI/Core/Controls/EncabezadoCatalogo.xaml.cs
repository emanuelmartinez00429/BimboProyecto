using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CapaUI.Converters;

namespace CapaUI.Core.Controls
{
    /// <summary>
    /// Encabezado estándar de página de catálogo: ícono con sombra + miga de pan + título
    /// a la izquierda, y hasta 3 pastillas de conteo (Total/Activos/Inactivos) a la derecha
    /// que colapsan su etiqueta de texto por debajo de <see cref="UmbralAncho"/>.
    /// </summary>
    public partial class EncabezadoCatalogo : UserControl
    {
        public static readonly DependencyProperty IconoProperty =
            DependencyProperty.Register(nameof(Icono), typeof(Geometry), typeof(EncabezadoCatalogo),
                new PropertyMetadata(null, OnIconoChanged));

        public Geometry? Icono
        {
            get => (Geometry?)GetValue(IconoProperty);
            set => SetValue(IconoProperty, value);
        }

        /// <summary>
        /// Igual semántica que <see cref="EmptyStateOverlay.IconoEsRelleno"/>: <c>false</c>
        /// (default) deja el ícono en Stroke (íconos de línea viejos: IconBox, IconTruck,
        /// IconFactory, IconTag, IconHistory); <c>true</c> mueve Stroke a Fill (íconos
        /// vectorizados de Material Symbols: IcoUsuarios, IcoInventario, etc.).
        /// </summary>
        public static readonly DependencyProperty IconoEsRellenoProperty =
            DependencyProperty.Register(nameof(IconoEsRelleno), typeof(bool), typeof(EncabezadoCatalogo),
                new PropertyMetadata(false, OnIconoChanged));

        public bool IconoEsRelleno
        {
            get => (bool)GetValue(IconoEsRellenoProperty);
            set => SetValue(IconoEsRellenoProperty, value);
        }

        public static readonly DependencyProperty ModuloPadreProperty =
            DependencyProperty.Register(nameof(ModuloPadre), typeof(string), typeof(EncabezadoCatalogo),
                new PropertyMetadata(string.Empty));

        public string ModuloPadre
        {
            get => (string)GetValue(ModuloPadreProperty);
            set => SetValue(ModuloPadreProperty, value);
        }

        public static readonly DependencyProperty SubmoduloProperty =
            DependencyProperty.Register(nameof(Submodulo), typeof(string), typeof(EncabezadoCatalogo),
                new PropertyMetadata(string.Empty));

        public string Submodulo
        {
            get => (string)GetValue(SubmoduloProperty);
            set => SetValue(SubmoduloProperty, value);
        }

        public static readonly DependencyProperty TituloProperty =
            DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(EncabezadoCatalogo),
                new PropertyMetadata(string.Empty));

        public string Titulo
        {
            get => (string)GetValue(TituloProperty);
            set => SetValue(TituloProperty, value);
        }

        // Pastillas: int? — null = esa pastilla no se renderiza (cubre Bitácora: solo Total).
        public static readonly DependencyProperty TotalProperty =
            DependencyProperty.Register(nameof(Total), typeof(int?), typeof(EncabezadoCatalogo),
                new PropertyMetadata(null, OnTotalChanged));

        public int? Total
        {
            get => (int?)GetValue(TotalProperty);
            set => SetValue(TotalProperty, value);
        }

        public static readonly DependencyProperty ActivosProperty =
            DependencyProperty.Register(nameof(Activos), typeof(int?), typeof(EncabezadoCatalogo),
                new PropertyMetadata(null, OnActivosChanged));

        public int? Activos
        {
            get => (int?)GetValue(ActivosProperty);
            set => SetValue(ActivosProperty, value);
        }

        public static readonly DependencyProperty InactivosProperty =
            DependencyProperty.Register(nameof(Inactivos), typeof(int?), typeof(EncabezadoCatalogo),
                new PropertyMetadata(null, OnInactivosChanged));

        public int? Inactivos
        {
            get => (int?)GetValue(InactivosProperty);
            set => SetValue(InactivosProperty, value);
        }

        /// <summary>
        /// Ancho (px) del control por debajo del cual las etiquetas "TOTAL"/"ACTIVOS"/
        /// "INACTIVOS" se ocultan, dejando solo el punto de color y el número. Ver
        /// <see cref="ActualizarVisibilidadEtiquetas"/> — no se puede resolver con un
        /// converter por-pastilla en XAML porque <c>Binding.ConverterParameter</c> no es
        /// una DependencyProperty (no admite bindearse a este valor).
        /// </summary>
        public static readonly DependencyProperty UmbralAnchoProperty =
            DependencyProperty.Register(nameof(UmbralAncho), typeof(double), typeof(EncabezadoCatalogo),
                new PropertyMetadata(690.0, OnUmbralAnchoChanged));

        public double UmbralAncho
        {
            get => (double)GetValue(UmbralAnchoProperty);
            set => SetValue(UmbralAnchoProperty, value);
        }

        public EncabezadoCatalogo()
        {
            InitializeComponent();
            SizeChanged += (_, _) => ActualizarVisibilidadEtiquetas();
            Loaded += (_, _) => ActualizarVisibilidadEtiquetas();
        }

        private static void OnIconoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Se dispara tanto al cambiar Icono como al cambiar IconoEsRelleno.
            if (d is not EncabezadoCatalogo c) return;
            if (c.FindName("PathIcono") is not Path path) return;

            if (c.Icono is Geometry g)
                path.Data = g;

            if (c.IconoEsRelleno)
            {
                // Ícono de relleno sólido (Material Symbols): mover el color de Stroke a
                // Fill y quitar el contorno, igual que EmptyStateOverlay.OnIconoChanged.
                path.Fill = path.Stroke;
                path.Stroke = null;
            }
            // else: ícono de línea (default) — se deja tal cual viene definido en el XAML.
        }

        private static void OnTotalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            AlternarPastilla(d, "PillTotal", e.NewValue);

        private static void OnActivosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            AlternarPastilla(d, "PillActivos", e.NewValue);

        private static void OnInactivosChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            AlternarPastilla(d, "PillInactivos", e.NewValue);

        private static void AlternarPastilla(DependencyObject d, string nombrePastilla, object? nuevoValor)
        {
            if (d is not EncabezadoCatalogo c) return;
            if (c.FindName(nombrePastilla) is not FrameworkElement pastilla) return;
            pastilla.Visibility = nuevoValor is int ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OnUmbralAnchoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EncabezadoCatalogo c) c.ActualizarVisibilidadEtiquetas();
        }

        private void ActualizarVisibilidadEtiquetas()
        {
            var visibilidad = (Visibility)AnchoMinimoAVisibilidadConverter.Instancia.Convert(
                ActualWidth, typeof(Visibility), UmbralAncho, CultureInfo.InvariantCulture);

            if (FindName("TxtEtiquetaTotal") is TextBlock t) t.Visibility = visibilidad;
            if (FindName("TxtEtiquetaActivos") is TextBlock a) a.Visibility = visibilidad;
            if (FindName("TxtEtiquetaInactivos") is TextBlock i) i.Visibility = visibilidad;
        }
    }
}
