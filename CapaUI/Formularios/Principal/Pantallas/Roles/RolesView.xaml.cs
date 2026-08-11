using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Roles;

public partial class RolesView : System.Windows.Controls.UserControl
{
    // Pinceles congelados a nivel de clase: los avisos se crean y destruyen a
    // repetición, no tiene sentido alojar uno nuevo por cada uno.
    private static readonly Brush FondoAviso = Congelar("#1A1F2E");
    private static readonly Brush TildeAviso = Congelar("#4ADE80");

    private RolesViewModel? _vm;
    private bool _shimmerActivo;
    private readonly List<DispatcherTimer> _temporizadores = new();

    public RolesView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) return;

        _vm = App.Services.GetRequiredService<RolesViewModel>();
        _vm.Toast += MostrarAviso;
        DataContext = _vm;

        await _vm.CargarAsync();
    }

    /// <summary>
    /// La navegación descarta la instancia de la vista, así que acá se corta
    /// todo lo que podría sobrevivirla: el evento del ViewModel, los timers de
    /// los avisos y la referencia del DataContext.
    /// </summary>
    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        DetenerShimmer();

        foreach (var temporizador in _temporizadores)
            temporizador.Stop();
        _temporizadores.Clear();
        ToastHost.Children.Clear();

        if (_vm is null) return;
        _vm.Toast -= MostrarAviso;
        _vm.Dispose();
        _vm = null;
        DataContext = null;
    }

    // ── Shimmer del esqueleto ──────────────────────────────────────────────

    /// <summary>
    /// WPF NO detiene las animaciones de un elemento colapsado: el reloj sigue
    /// tickeando e invalidando aunque no se dibuje nada, y un RepeatBehavior.Forever
    /// sin frenar mantiene viva la referencia al elemento. Por eso el brillo se
    /// enciende y se apaga siguiendo la visibilidad real del esqueleto.
    /// </summary>
    private void Esqueleto_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            IniciarShimmer();
        else
            DetenerShimmer();
    }

    private void IniciarShimmer()
    {
        if (_shimmerActivo) return;
        _shimmerActivo = true;

        // 1000 ms = el mismo piso que RolesViewModel.DuracionMinimaEsqueletoMs,
        // así en la carga rápida se alcanza a ver un barrido entero.
        var barrido = new DoubleAnimation(-1, 1, TimeSpan.FromMilliseconds(1000))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };

        // Sin easing: es un loop continuo — un EaseInOut haría que el brillo
        // "frene" en los bordes y se notaría el corte.
        // 20 fps en vez de los ~60 por defecto: en un barrido lento y difuso se
        // ve idéntico y el TimeManager hace un tercio del trabajo.
        Timeline.SetDesiredFrameRate(barrido, 20);

        ShimmerTransform.BeginAnimation(TranslateTransform.XProperty, barrido);
    }

    private void DetenerShimmer()
    {
        if (!_shimmerActivo) return;
        _shimmerActivo = false;

        // null quita la animación de la propiedad y libera el AnimationClock.
        ShimmerTransform.BeginAnimation(TranslateTransform.XProperty, null);
        ShimmerTransform.X = -1;
    }

    private void MostrarAviso(string mensaje)
    {
        var contenido = new StackPanel { Orientation = Orientation.Horizontal };
        contenido.Children.Add(new System.Windows.Shapes.Path
        {
            Data = (Geometry)FindResource("GeoCheck"),
            Stroke = TildeAviso,
            StrokeThickness = 2.4,
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        });
        contenido.Children.Add(new TextBlock
        {
            Text = mensaje,
            Foreground = Brushes.White,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var burbuja = new Border
        {
            Background = FondoAviso,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 9, 18, 9),
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0,
            Child = contenido,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.4, Color = Colors.Black },
        };

        var desplazamiento = new TranslateTransform(0, 8);
        burbuja.RenderTransform = desplazamiento;
        ToastHost.Children.Add(burbuja);

        burbuja.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        desplazamiento.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });

        var temporizador = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
        _temporizadores.Add(temporizador);
        temporizador.Tick += (_, _) =>
        {
            temporizador.Stop();
            _temporizadores.Remove(temporizador);

            var salida = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
            salida.Completed += (_, _) => ToastHost.Children.Remove(burbuja);
            burbuja.BeginAnimation(OpacityProperty, salida);
        };
        temporizador.Start();
    }

    private static Brush Congelar(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
