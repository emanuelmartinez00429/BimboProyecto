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
            if (d is not EmptyStateOverlay c || e.NewValue is not Geometry g) return;
            c.IconoEstado.Data = g;
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
