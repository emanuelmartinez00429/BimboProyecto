using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapaUI.Core.Controls;

public partial class SuggestionSearchBox : UserControl
{
    private bool _updatingText;

    // ── Evento de selección ───────────────────────────────────────────────────

    public event System.EventHandler<SuggestionItemData>? ItemSelected;

    // ── DependencyProperties ──────────────────────────────────────────────────

    public static readonly DependencyProperty QueryProperty =
        DependencyProperty.Register(nameof(Query), typeof(string), typeof(SuggestionSearchBox),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnQueryChanged));

    public string Query
    {
        get => (string)GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(SuggestionSearchBox),
            new PropertyMetadata("Buscar..."));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly DependencyProperty SuggestItemsProperty =
        DependencyProperty.Register(nameof(SuggestItems), typeof(IReadOnlyList<SuggestionItemData>),
            typeof(SuggestionSearchBox),
            new PropertyMetadata(null, OnSuggestItemsChanged));

    public IReadOnlyList<SuggestionItemData>? SuggestItems
    {
        get => (IReadOnlyList<SuggestionItemData>?)GetValue(SuggestItemsProperty);
        set => SetValue(SuggestItemsProperty, value);
    }

    public static readonly DependencyProperty HighlightIndexProperty =
        DependencyProperty.Register(nameof(HighlightIndex), typeof(int), typeof(SuggestionSearchBox),
            new FrameworkPropertyMetadata(-1,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHighlightIndexChanged));

    public int HighlightIndex
    {
        get => (int)GetValue(HighlightIndexProperty);
        set => SetValue(HighlightIndexProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public SuggestionSearchBox()
    {
        InitializeComponent();
    }

    // ── Callbacks de DP ──────────────────────────────────────────────────────

    private static void OnQueryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SuggestionSearchBox)d;
        if (ctrl._updatingText) return;
        ctrl._updatingText = true;
        var texto = (string)(e.NewValue ?? string.Empty);
        ctrl.TxtBusqueda.Text = texto;
        ctrl.BtnClearSearch.Visibility = string.IsNullOrEmpty(texto)
            ? Visibility.Collapsed : Visibility.Visible;
        ctrl._updatingText = false;
    }

    private static void OnSuggestItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl  = (SuggestionSearchBox)d;
        var items = e.NewValue as IReadOnlyList<SuggestionItemData>;

        // Siempre limpiar selección antes de cargar items nuevos.
        // Así, aunque HighlightIndex no cambie de valor (-1→-1),
        // la ListBox queda visualmente deseleccionada (fix bug #3).
        ctrl.SuggestionsList.SelectedIndex = -1;

        if (items is { Count: > 0 })
        {
            ctrl.SuggestionsList.ItemsSource = items;
            ctrl.SugCountLabel.Text =
                $"\u2191\u2193 navegar · \u21b5 seleccionar · {items.Count} coincidencias";
            ctrl.SuggestionsPopup.IsOpen = true;
            ctrl.SearchBoxBorder.CornerRadius = new CornerRadius(8, 8, 0, 0);
        }
        else
        {
            ctrl.SuggestionsPopup.IsOpen = false;
            if (!ctrl.TxtBusqueda.IsFocused)
                ctrl.SearchBoxBorder.CornerRadius = new CornerRadius(8);
        }
    }

    private static void OnHighlightIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SuggestionSearchBox)d;
        int idx  = (int)e.NewValue;
        ctrl.SuggestionsList.SelectedIndex = idx;
        if (idx >= 0 && idx < ctrl.SuggestionsList.Items.Count)
            ctrl.SuggestionsList.ScrollIntoView(ctrl.SuggestionsList.Items[idx]);
    }

    // ── Handlers del TextBox ─────────────────────────────────────────────────

    private void TxtBusqueda_GotFocus(object sender, RoutedEventArgs e)
    {
        if (SuggestionsPopup.IsOpen)
            SearchBoxBorder.CornerRadius = new CornerRadius(8, 8, 0, 0);
    }

    private void TxtBusqueda_LostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            if (!SuggestionsPopup.IsKeyboardFocusWithin)
            {
                SuggestionsPopup.IsOpen = false;
                SearchBoxBorder.CornerRadius = new CornerRadius(8);
            }
        });
    }

    private void TxtBusqueda_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingText) return;
        _updatingText = true;
        Query = TxtBusqueda.Text;
        BtnClearSearch.Visibility = string.IsNullOrEmpty(TxtBusqueda.Text)
            ? Visibility.Collapsed : Visibility.Visible;
        _updatingText = false;
    }

    private void TxtBusqueda_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var items = SuggestItems;
        if (items == null || items.Count == 0) return;

        if (e.Key == Key.Down)
        {
            // Wrap-around: del último vuelve al primero
            HighlightIndex = (HighlightIndex + 1) % items.Count;
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            // Wrap-around: del primero (o -1) va al último
            HighlightIndex = HighlightIndex <= 0
                ? items.Count - 1
                : HighlightIndex - 1;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (HighlightIndex >= 0 && HighlightIndex < items.Count)
                SeleccionarItem(items[HighlightIndex]);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CerrarPopup();
            e.Handled = true;
        }
    }

    // ── Handlers del botón limpiar ────────────────────────────────────────────

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        CerrarPopup();
    }

    // ── Handlers de items del popup ───────────────────────────────────────────

    private void SuggestionItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is SuggestionItemData data)
            SeleccionarItem(data);
    }

    private void SuggestionItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border b && b.Tag is SuggestionItemData data && SuggestItems != null)
        {
            for (int i = 0; i < SuggestItems.Count; i++)
            {
                if (ReferenceEquals(SuggestItems[i], data))
                {
                    HighlightIndex = i;
                    break;
                }
            }
        }
    }

    // ── Lógica interna ────────────────────────────────────────────────────────

    private void SeleccionarItem(SuggestionItemData data)
    {
        ItemSelected?.Invoke(this, data);
        CerrarPopup();
    }

    private void CerrarPopup()
    {
        _updatingText = true;
        Query = "";
        TxtBusqueda.Text = "";
        BtnClearSearch.Visibility = Visibility.Collapsed;
        _updatingText = false;

        SuggestionsPopup.IsOpen = false;
        SearchBoxBorder.CornerRadius = new CornerRadius(8);
    }
}
