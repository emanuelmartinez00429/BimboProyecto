using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CapaUI.Core.Controls;

/// <summary>
/// Control de superposición de carga con indicador circular vectorial animado (spinner).
/// Encapsula el ciclo de vida de la animación y la visibilidad de forma reactiva,
/// evitando duplicación de Storyboards imperativos en el code-behind de los formularios.
/// </summary>
public partial class LoadingOverlay : UserControl
{
    private bool _isSpinning;

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LoadingOverlay),
            new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty MensajeProperty =
        DependencyProperty.Register(nameof(Mensaje), typeof(string), typeof(LoadingOverlay),
            new PropertyMetadata("Cargando..."));

    public static readonly DependencyProperty SpinnerBrushProperty =
        DependencyProperty.Register(nameof(SpinnerBrush), typeof(Brush), typeof(LoadingOverlay),
            new PropertyMetadata(null, OnSpinnerBrushChanged));

    public static readonly DependencyProperty TamanoProperty =
        DependencyProperty.Register(nameof(Tamano), typeof(double), typeof(LoadingOverlay),
            new PropertyMetadata(22.0));

    public static readonly DependencyProperty GrosorProperty =
        DependencyProperty.Register(nameof(Grosor), typeof(double), typeof(LoadingOverlay),
            new PropertyMetadata(2.5));

    public static readonly DependencyProperty HeaderOffsetProperty =
        DependencyProperty.Register(nameof(HeaderOffset), typeof(double), typeof(LoadingOverlay),
            new PropertyMetadata(0.0, OnHeaderOffsetChanged));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string Mensaje
    {
        get => (string)GetValue(MensajeProperty);
        set => SetValue(MensajeProperty, value);
    }

    public Brush? SpinnerBrush
    {
        get => (Brush?)GetValue(SpinnerBrushProperty);
        set => SetValue(SpinnerBrushProperty, value);
    }

    public double Tamano
    {
        get => (double)GetValue(TamanoProperty);
        set => SetValue(TamanoProperty, value);
    }

    public double Grosor
    {
        get => (double)GetValue(GrosorProperty);
        set => SetValue(GrosorProperty, value);
    }

    public double HeaderOffset
    {
        get => (double)GetValue(HeaderOffsetProperty);
        set => SetValue(HeaderOffsetProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
        ActualizarOffset();
        Loaded += (s, e) =>
        {
            if (IsLoading) IniciarAnimacion();
        };
        Unloaded += (s, e) => DetenerAnimacion();
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LoadingOverlay control || e.NewValue is not bool isLoading) return;

        if (isLoading)
        {
            control.Visibility = Visibility.Visible;
            control.IniciarAnimacion();
        }
        else
        {
            control.Visibility = Visibility.Collapsed;
            control.DetenerAnimacion();
        }
    }

    private static void OnSpinnerBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay control && e.NewValue is Brush brush)
        {
            if (control.FindName("SpinnerPath") is System.Windows.Shapes.Path spinner)
            {
                spinner.Stroke = brush;
            }
        }
    }

    private static void OnHeaderOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay control)
            control.ActualizarOffset();
    }

    private void ActualizarOffset()
    {
        Margin = new Thickness(Margin.Left, HeaderOffset, Margin.Right, Margin.Bottom);
        if (FindName("ContenedorPanel") is FrameworkElement panel)
            panel.Margin = new Thickness(0);
    }

    private RotateTransform? GetRotateTransform()
    {
        if (FindName("SpinnerRotate") is RotateTransform rotate)
            return rotate;

        if (FindName("SpinnerPath") is System.Windows.Shapes.Path path &&
            path.RenderTransform is RotateTransform pathRotate)
            return pathRotate;

        return null;
    }

    private void IniciarAnimacion()
    {
        if (_isSpinning) return;

        var rotate = GetRotateTransform();
        if (rotate == null) return;

        var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
        _isSpinning = true;
    }

    private void DetenerAnimacion()
    {
        if (!_isSpinning) return;

        var rotate = GetRotateTransform();
        rotate?.BeginAnimation(RotateTransform.AngleProperty, null);
        _isSpinning = false;
    }
}
