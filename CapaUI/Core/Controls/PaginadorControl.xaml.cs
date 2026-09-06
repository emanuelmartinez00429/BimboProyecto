using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapaUI.Core.Controls;

/// <summary>
/// Control de paginación reutilizable para vistas de catálogo y tablas de datos.
/// Centraliza la navegación, cálculo de elipsis con <see cref="Paginacion"/>
/// y protección de rango (clamping) sobre la página actual.
/// </summary>
public partial class PaginadorControl : UserControl
{
    public static readonly DependencyProperty PageProperty =
        DependencyProperty.Register(
            nameof(Page),
            typeof(int),
            typeof(PaginadorControl),
            new FrameworkPropertyMetadata(
                1,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPageChanged,
                CoercePage));

    public static readonly DependencyProperty TotalPagesProperty =
        DependencyProperty.Register(
            nameof(TotalPages),
            typeof(int),
            typeof(PaginadorControl),
            new FrameworkPropertyMetadata(1, OnTotalPagesChanged));

    public static readonly DependencyProperty PageInfoProperty =
        DependencyProperty.Register(
            nameof(PageInfo),
            typeof(string),
            typeof(PaginadorControl),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(PaginadorControl),
            new FrameworkPropertyMetadata(false, OnIsLoadingChanged));

    public int Page
    {
        get => (int)GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public int TotalPages
    {
        get => (int)GetValue(TotalPagesProperty);
        set => SetValue(TotalPagesProperty, value);
    }

    public string PageInfo
    {
        get => (string)GetValue(PageInfoProperty);
        set => SetValue(PageInfoProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public PaginadorControl()
    {
        InitializeComponent();
        Loaded += (s, e) => Refrescar();
    }

    private static object CoercePage(DependencyObject d, object baseValue)
    {
        if (d is PaginadorControl control && baseValue is int pg)
        {
            int max = Math.Max(1, control.TotalPages);
            if (pg < 1) return 1;
            if (pg > max) return max;
            return pg;
        }
        return baseValue;
    }

    private static void OnPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PaginadorControl control)
        {
            control.Refrescar();
        }
    }

    private static void OnTotalPagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PaginadorControl control)
        {
            control.CoerceValue(PageProperty);
            control.Refrescar();
        }
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PaginadorControl control)
        {
            control.ActualizarHabilitacion();
        }
    }

    private void Refrescar()
    {
        if (PaginacionPanel == null) return;

        PaginacionPanel.Children.Clear();

        int total = Math.Max(1, TotalPages);
        int current = Page;

        foreach (var p in Paginacion.Calcular(current, total))
        {
            if (p == Paginacion.Elipsis)
            {
                PaginacionPanel.Children.Add(new TextBlock
                {
                    Text = "\u2026",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"))
                });
            }
            else
            {
                var btn = new Button
                {
                    Content = p.ToString(),
                    Margin = new Thickness(2, 0, 2, 0),
                    Style = (Style)(p == current
                        ? TryFindResource("ActivePageBtn")
                        : TryFindResource("PageBtn")),
                    Tag = p,
                    IsEnabled = !IsLoading
                };
                btn.Click += (s, ev) =>
                {
                    if (IsLoading) return;
                    if (s is Button b && b.Tag is int targetPage)
                    {
                        Page = targetPage;
                    }
                };
                PaginacionPanel.Children.Add(btn);
            }
        }

        ActualizarHabilitacion();
    }

    private void ActualizarHabilitacion()
    {
        if (BtnPrimera == null || BtnAnterior == null || BtnSiguiente == null || BtnUltima == null || PaginacionPanel == null)
            return;

        int total = Math.Max(1, TotalPages);
        bool canPrev = !IsLoading && Page > 1;
        bool canNext = !IsLoading && Page < total;

        BtnPrimera.IsEnabled = canPrev;
        BtnAnterior.IsEnabled = canPrev;
        BtnSiguiente.IsEnabled = canNext;
        BtnUltima.IsEnabled = canNext;

        foreach (UIElement child in PaginacionPanel.Children)
        {
            if (child is Button btn)
            {
                btn.IsEnabled = !IsLoading;
            }
        }
    }

    private void BtnPrimera_Click(object sender, RoutedEventArgs e)
    {
        if (IsLoading) return;
        Page = 1;
    }

    private void BtnAnterior_Click(object sender, RoutedEventArgs e)
    {
        if (IsLoading) return;
        Page = Math.Max(1, Page - 1);
    }

    private void BtnSiguiente_Click(object sender, RoutedEventArgs e)
    {
        if (IsLoading) return;
        Page = Math.Min(Math.Max(1, TotalPages), Page + 1);
    }

    private void BtnUltima_Click(object sender, RoutedEventArgs e)
    {
        if (IsLoading) return;
        Page = Math.Max(1, TotalPages);
    }
}
