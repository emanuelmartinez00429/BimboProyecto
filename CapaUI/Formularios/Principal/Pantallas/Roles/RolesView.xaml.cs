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
    private Storyboard? _spinnerStory;
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
        _vm.SolicitarEdicionRol += AbrirModalRol;
        _vm.ConfirmarCambioEstado += ConfirmarCambioEstado;
        _vm.ConfirmarCambiosPendientes += ConfirmarCambiosPendientes;
        _vm.PropertyChanged += OnVmPropertyChanged;
        DataContext = _vm;

        // try/catch obligatorio: este manejador es async void, y una excepción
        // que se escape de acá no la puede atrapar nadie — sube al dispatcher y
        // tumba la aplicación entera.
        try
        {
            // Se captura la instancia ANTES del await. Si el usuario cierra la
            // pantalla mientras carga, Unloaded pone _vm = null y la continuación
            // volvería sobre una vista ya descargada. Se compara por referencia
            // para cubrir también el abrir-cerrar-abrir rápido, donde _vm no es
            // null pero es OTRO ViewModel.
            var vm = _vm;
            await vm.CargarAsync();
            if (!ReferenceEquals(_vm, vm)) return;
            if (Window.GetWindow(this)?.DataContext is MainViewModel principal &&
                principal.ConsumirRegistroNotificacionPendiente("roles") is int idRol)
                vm.AbrirDetalleRolCommand.Execute(idRol);

        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[Roles] Falló la carga inicial de la pantalla");
            if (_vm is null) return;

            // Sin esto la pantalla quedaría para siempre en "Cargando roles...".
            _vm.MostrarErrorCarga("No se pudo cargar la configuración de roles.");
        }
    }

    private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RolesViewModel.IsLoading))
            ActualizarCarga();
    }

    private void AbrirModalRol(RolItemVm? rol)
    {
        if (_vm is null) return;
        var modal = new RolModal(_vm, rol) { Owner = Window.GetWindow(this) };
        modal.ShowDialog();
    }

    private bool ConfirmarCambioEstado(RolItemVm rol)
    {
        string verbo = rol.EsActivo ? "desactivar" : "activar";
        return MessageBox.Show(
            $"¿Desea {verbo} el rol {rol.Nombre}?",
            "Confirmar cambio de estado",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private DecisionCambiosPendientes ConfirmarCambiosPendientes()
    {
        var dialogo = new Window
        {
            Title = "Cambios pendientes",
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false,
            Background = Brushes.White,
        };
        var raiz = new StackPanel { Margin = new Thickness(24), Width = 430 };
        raiz.Children.Add(new TextBlock
        {
            Text = "Hay cambios de permisos sin guardar.",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
        });
        raiz.Children.Add(new TextBlock
        {
            Text = "¿Desea guardarlos antes de volver a la lista de roles?",
            FontSize = 13,
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 20),
        });
        var botones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        raiz.Children.Add(botones);
        dialogo.Content = raiz;

        var decision = DecisionCambiosPendientes.SeguirEditando;
        AgregarBoton("Seguir editando", DecisionCambiosPendientes.SeguirEditando, false);
        AgregarBoton("Descartar", DecisionCambiosPendientes.Descartar, false);
        AgregarBoton("Guardar", DecisionCambiosPendientes.Guardar, true);
        dialogo.ShowDialog();
        return decision;

        void AgregarBoton(string texto, DecisionCambiosPendientes valor, bool predeterminado)
        {
            var boton = new Button
            {
                Content = texto,
                MinWidth = 100,
                Height = 34,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(12, 0, 12, 0),
                IsDefault = predeterminado,
                IsCancel = valor == DecisionCambiosPendientes.SeguirEditando,
            };
            boton.Click += (_, _) =>
            {
                decision = valor;
                dialogo.DialogResult = true;
            };
            botones.Children.Add(boton);
        }
    }

    // ── Estado de carga ────────────────────────────────────────────────────

    private void ActualizarCarga()
    {
        if (_vm is null) return;

        // Solo se alterna el area de tarjetas. El encabezado, el buscador, los
        // filtros y el pie son parte fija del formulario y quedan visibles — el
        // mismo criterio que Productos, Usuarios y Categorias, donde lo unico
        // que se oculta mientras carga es el DataGrid.
        if (_vm.IsLoading)
        {
            ContenidoRoles.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            IniciarSpinner();
        }
        else
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            DetenerSpinner();
            ContenidoRoles.Visibility = Visibility.Visible;
        }
    }

    // ── Spinner ────────────────────────────────────────────────────────────

    private void IniciarSpinner()
    {
        if (_spinnerStory != null) return;
        _spinnerStory = new Storyboard();
        var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
        { RepeatBehavior = RepeatBehavior.Forever };
        Storyboard.SetTarget(anim, SpinnerPath);
        Storyboard.SetTargetProperty(anim,
            new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
        _spinnerStory.Children.Add(anim);
        _spinnerStory.Begin();
    }

    private void DetenerSpinner()
    {
        if (_spinnerStory is null) return;
        _spinnerStory.Stop();
        _spinnerStory.Remove();         // desasocia el clock del elemento destino
        _spinnerStory.Children.Clear(); // corta la referencia a SpinnerPath
        _spinnerStory = null;
    }

    /// <summary>
    /// La navegación descarta la instancia de la vista, así que acá se corta
    /// todo lo que podría sobrevivirla: el evento del ViewModel, los timers de
    /// los avisos y la referencia del DataContext.
    /// </summary>
    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        DetenerSpinner();

        foreach (var temporizador in _temporizadores)
            temporizador.Stop();
        _temporizadores.Clear();
        ToastHost.Children.Clear();

        if (_vm is null) return;
        _vm.Toast -= MostrarAviso;
        _vm.SolicitarEdicionRol -= AbrirModalRol;
        _vm.ConfirmarCambioEstado -= ConfirmarCambioEstado;
        _vm.ConfirmarCambiosPendientes -= ConfirmarCambiosPendientes;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.Dispose();
        _vm = null;
        DataContext = null;
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
