using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapaUI.Core.Controls;

/// <summary>
/// Boton reutilizable de "Limpiar Filtros" con icono vectorial Material Symbols (IcoFiltro)
/// y comportamiento responsivo: colapsa la etiqueta de texto por debajo del umbral de ancho
/// especificado (por defecto 760px) manteniendo visible el icono y el ToolTip.
/// </summary>
public partial class BotonLimpiarFiltros : UserControl
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(BotonLimpiarFiltros),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter),
            typeof(object),
            typeof(BotonLimpiarFiltros),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TextoProperty =
        DependencyProperty.Register(
            nameof(Texto),
            typeof(string),
            typeof(BotonLimpiarFiltros),
            new PropertyMetadata("Limpiar Filtros"));

    public static readonly DependencyProperty ToolTipTextoProperty =
        DependencyProperty.Register(
            nameof(ToolTipTexto),
            typeof(string),
            typeof(BotonLimpiarFiltros),
            new PropertyMetadata("Limpiar filtros"));

    public static readonly DependencyProperty ReferenciaAnchoProperty =
        DependencyProperty.Register(
            nameof(ReferenciaAncho),
            typeof(FrameworkElement),
            typeof(BotonLimpiarFiltros),
            new PropertyMetadata(null, OnReferenciaAnchoChanged));

    public static readonly DependencyProperty UmbralAnchoProperty =
        DependencyProperty.Register(
            nameof(UmbralAncho),
            typeof(double),
            typeof(BotonLimpiarFiltros),
            new PropertyMetadata(760.0, OnUmbralAnchoChanged));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public string Texto
    {
        get => (string)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    public string ToolTipTexto
    {
        get => (string)GetValue(ToolTipTextoProperty);
        set => SetValue(ToolTipTextoProperty, value);
    }

    public FrameworkElement? ReferenciaAncho
    {
        get => (FrameworkElement?)GetValue(ReferenciaAnchoProperty);
        set => SetValue(ReferenciaAnchoProperty, value);
    }

    public double UmbralAncho
    {
        get => (double)GetValue(UmbralAnchoProperty);
        set => SetValue(UmbralAnchoProperty, value);
    }

    public BotonLimpiarFiltros()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResolverComandoPorDefecto();
        ActualizarVisibilidadTexto();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ReferenciaAncho != null)
            ReferenciaAncho.SizeChanged -= AlCambiarTamañoReferencia;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ResolverComandoPorDefecto();
    }

    private static void OnReferenciaAnchoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not BotonLimpiarFiltros btn) return;

        if (e.OldValue is FrameworkElement oldElem)
            oldElem.SizeChanged -= btn.AlCambiarTamañoReferencia;

        if (e.NewValue is FrameworkElement newElem)
        {
            newElem.SizeChanged += btn.AlCambiarTamañoReferencia;
            btn.ActualizarVisibilidadTexto();
        }
        else
        {
            btn.ActualizarVisibilidadTexto();
        }
    }

    private static void OnUmbralAnchoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BotonLimpiarFiltros btn)
            btn.ActualizarVisibilidadTexto();
    }

    private void AlCambiarTamañoReferencia(object sender, SizeChangedEventArgs e)
    {
        ActualizarVisibilidadTexto();
    }

    private void ActualizarVisibilidadTexto()
    {
        if (TxtEtiqueta == null) return;

        if (ReferenciaAncho == null || ReferenciaAncho.ActualWidth <= 0)
        {
            TxtEtiqueta.Visibility = Visibility.Visible;
            return;
        }

        TxtEtiqueta.Visibility = ReferenciaAncho.ActualWidth >= UmbralAncho
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Si no se asigno explicitamente Command, busca por convencion LimpiarFiltrosCommand en el DataContext.
    /// </summary>
    private void ResolverComandoPorDefecto()
    {
        if (Command != null || DataContext == null) return;

        var prop = DataContext.GetType().GetProperty("LimpiarFiltrosCommand");
        if (prop != null && typeof(ICommand).IsAssignableFrom(prop.PropertyType))
        {
            if (prop.GetValue(DataContext) is ICommand cmd)
            {
                SetCurrentValue(CommandProperty, cmd);
            }
        }
    }
}
