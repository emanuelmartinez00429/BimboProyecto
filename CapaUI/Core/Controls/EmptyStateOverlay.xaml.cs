using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapaUI.Core.Controls
{
    public partial class EmptyStateOverlay : UserControl
    {
        /// <summary>
        /// Icono del estado vacío. Tiene default (una hoja) para que las pantallas que no
        /// lo pasan sigan viéndose igual, pero cada tabla puede dar el suyo: en Pesaje el
        /// vacío invita a pesar, y una balanza lo dice mejor que un documento.
        /// </summary>
        public static readonly DependencyProperty IconoProperty =
            DependencyProperty.Register(nameof(Icono), typeof(Geometry), typeof(EmptyStateOverlay),
                new PropertyMetadata(null, OnIconoChanged));

        public Geometry? Icono
        {
            get => (Geometry?)GetValue(IconoProperty);
            set => SetValue(IconoProperty, value);
        }

        /// <summary>
        /// Indica si el ícono asignado a <see cref="Icono"/> es de relleno sólido (Fill)
        /// en lugar de línea (Stroke). Default <c>false</c>.<br/>
        /// Cuando es <c>true</c>, <see cref="OnIconoChanged"/> mueve el color actual de
        /// <c>Stroke</c> a <c>Fill</c> y quita el contorno — necesario para geometrías como
        /// <c>IcoScale</c> que se diseñaron para pintarse con Fill.<br/>
        /// Cuando es <c>false</c> (default) el ícono se renderiza tal cual, con Stroke,
        /// sin alterar Fill — correcto para todos los íconos de línea del proyecto
        /// (IconTruck, IconBox, IconUser, etc.). Pon <c>True</c> solo al pasar un ícono
        /// de relleno.
        /// </summary>
        public static readonly DependencyProperty IconoEsRellenoProperty =
            DependencyProperty.Register(nameof(IconoEsRelleno), typeof(bool), typeof(EmptyStateOverlay),
                new PropertyMetadata(false, OnIconoChanged));

        public bool IconoEsRelleno
        {
            get => (bool)GetValue(IconoEsRellenoProperty);
            set => SetValue(IconoEsRellenoProperty, value);
        }

        public static readonly DependencyProperty MensajeProperty =
            DependencyProperty.Register(nameof(Mensaje), typeof(string), typeof(EmptyStateOverlay),
                new PropertyMetadata("No hay registros"));

        public static readonly DependencyProperty SubmensajeProperty =
            DependencyProperty.Register(nameof(Submensaje), typeof(string), typeof(EmptyStateOverlay),
                new PropertyMetadata(string.Empty, OnSubmensajeChanged));

        public static readonly DependencyProperty EstaVacioProperty =
            DependencyProperty.Register(nameof(EstaVacio), typeof(bool), typeof(EmptyStateOverlay),
                new PropertyMetadata(false, OnEstaVacioChanged));

        public static readonly DependencyProperty HeaderOffsetProperty =
            DependencyProperty.Register(nameof(HeaderOffset), typeof(double), typeof(EmptyStateOverlay),
                new PropertyMetadata(40.0, OnHeaderOffsetChanged));

        public string Mensaje
        {
            get => (string)GetValue(MensajeProperty);
            set => SetValue(MensajeProperty, value);
        }

        public string Submensaje
        {
            get => (string)GetValue(SubmensajeProperty);
            set => SetValue(SubmensajeProperty, value);
        }

        public bool EstaVacio
        {
            get => (bool)GetValue(EstaVacioProperty);
            set => SetValue(EstaVacioProperty, value);
        }

        public double HeaderOffset
        {
            get => (double)GetValue(HeaderOffsetProperty);
            set => SetValue(HeaderOffsetProperty, value);
        }

        public EmptyStateOverlay()
        {
            InitializeComponent();
            ActualizarOffset();
        }

        private static void OnIconoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // El callback se dispara tanto cuando cambia Icono como cuando cambia IconoEsRelleno.
            if (d is not EmptyStateOverlay c) return;

            // Si no hay geometría asignada todavía, solo actualizamos Data si corresponde
            // (puede llegar null en la inicialización del DP de IconoEsRelleno).
            if (c.Icono is Geometry g)
                c.IconoEstado.Data = g;

            if (c.IconoEsRelleno)
            {
                // Ícono de relleno sólido (ej. IcoScale): mover el color de Stroke a Fill
                // y quitar el contorno, para que la geometría se pinte como shape sólida.
                c.IconoEstado.Fill = c.IconoEstado.Stroke;
                c.IconoEstado.Stroke = null;
            }
            // else: ícono de línea (default) — se deja Fill y Stroke tal como están definidos
            // en el XAML (Fill=null, Stroke="#94A3B8"). Así IconTruck, IconBox, IconUser, etc.
            // siguen renderizando correctamente en cualquier vista que use EmptyStateOverlay.
        }

        private static void OnEstaVacioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EmptyStateOverlay control && e.NewValue is bool estaVacio)
            {
                control.Visibility = estaVacio ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnSubmensajeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EmptyStateOverlay control)
            {
                var texto = e.NewValue as string;
                control.TxtSubmensaje.Visibility = string.IsNullOrWhiteSpace(texto) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static void OnHeaderOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EmptyStateOverlay control)
            {
                control.ActualizarOffset();
            }
        }

        private void ActualizarOffset()
        {
            ContenedorPrincipal.Margin = new Thickness(0, HeaderOffset, 0, 0);
        }
    }
}
