using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using WpfKey         = System.Windows.Input.KeyEventArgs;
using WpfMouse       = System.Windows.Input.MouseEventArgs;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;
using Key            = System.Windows.Input.Key;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public class SuggestionItemData
    {
        public string     Codigo { get; set; } = "";
        public string     Nombre { get; set; } = "";
        public string     Meta   { get; set; } = "";
        public bool       Activo { get; set; }
        public ProductoDto Source { get; set; } = null!;
    }

    public partial class ProductosView : System.Windows.Controls.UserControl
    {
        private ProductosViewModel _vm = null!;
        private bool _suppressFilterChange = false;

        public event Action? SalirSolicitado;

        public ProductosView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _vm = App.Services.GetRequiredService<ProductosViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.SolicitarSalir   += () => SalirSolicitado?.Invoke();
            _vm.PropertyChanged  += (s, ev) =>
            {
                if (ev.PropertyName == nameof(ProductosViewModel.PageRows))
                    RefrescarPaginacion();
                if (ev.PropertyName == nameof(ProductosViewModel.IsLoading))
                    LoadingText.Visibility = _vm.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                if (ev.PropertyName == nameof(ProductosViewModel.NoResults))
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                if (ev.PropertyName == nameof(ProductosViewModel.HaySeleccionado))
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                if (ev.PropertyName == nameof(ProductosViewModel.Fabricantes))
                    PoblarFabricantes();
                if (ev.PropertyName == nameof(ProductosViewModel.Paises))
                    PoblarPaises();
                if (ev.PropertyName == nameof(ProductosViewModel.ShowSuggestions))
                    ActualizarSuggestions();
                if (ev.PropertyName == nameof(ProductosViewModel.HighlightIndex))
                    ActualizarHighlight();
            };

            DataContext = _vm;
            DgProductos.ItemsSource = _vm.PageRows;

            await _vm.CargarDatosAsync();
        }

        // ── Filters ───────────────────────────────────────────────────

        private void EstadoFiltro_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            if (RbHabilitados.IsChecked == true)
                _vm.EstadoFiltro = EstadoFilter.Habilitados;
            else if (RbDeshabilitados.IsChecked == true)
                _vm.EstadoFiltro = EstadoFilter.Deshabilitados;
            else
                _vm.EstadoFiltro = EstadoFilter.Todos;
        }

        private void CmbFabricante_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            var selected = CmbFabricante.SelectedItem as FiltroItem;
            _vm.FabricanteIdFiltro = selected?.Id;
        }

        private void CmbPais_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            var selected = CmbPais.SelectedItem as FiltroItem;
            _vm.PaisIdFiltro = selected?.Id;
        }

        private void PoblarFabricantes()
        {
            _suppressFilterChange = true;
            CmbFabricante.Items.Clear();
            CmbFabricante.Items.Add(new FiltroItem { Id = null, Nombre = "(Todos)" });
            foreach (var f in _vm.Fabricantes)
                CmbFabricante.Items.Add(f);
            CmbFabricante.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        private void PoblarPaises()
        {
            _suppressFilterChange = true;
            CmbPais.Items.Clear();
            CmbPais.Items.Add(new FiltroItem { Id = null, Nombre = "(Todos)" });
            foreach (var p in _vm.Paises)
                CmbPais.Items.Add(p);
            CmbPais.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        // ── Search ────────────────────────────────────────────────────

        private void TxtBusqueda_GotFocus(object sender, RoutedEventArgs e)
        {
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
            if (_vm == null) return;
            _vm.Query = TxtBusqueda.Text;
            BtnClearSearch.Visibility = string.IsNullOrEmpty(TxtBusqueda.Text)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void TxtBusqueda_PreviewKeyDown(object sender, WpfKey e)
        {
            if (_vm == null) return;
            if (e.Key == Key.Down)
            {
                if (_vm.Suggestions.Count > 0)
                    _vm.HighlightIndex = Math.Min(_vm.HighlightIndex + 1, _vm.Suggestions.Count - 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (_vm.Suggestions.Count > 0)
                    _vm.HighlightIndex = Math.Max(_vm.HighlightIndex - 1, 0);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (_vm.Suggestions.Count > 0 && _vm.HighlightIndex >= 0 && _vm.HighlightIndex < _vm.Suggestions.Count)
                {
                    _vm.SeleccionarSugerencia(_vm.Suggestions[_vm.HighlightIndex]);
                    TxtBusqueda.Text = "";
                    BtnClearSearch.Visibility = Visibility.Collapsed;
                    SuggestionsPopup.IsOpen = false;
                    SearchBoxBorder.CornerRadius = new CornerRadius(8);
                    SeleccionarEnTabla();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _vm.Query = "";
                TxtBusqueda.Text = "";
                SuggestionsPopup.IsOpen = false;
                SearchBoxBorder.CornerRadius = new CornerRadius(8);
                e.Handled = true;
            }
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            _vm.Query = "";
            TxtBusqueda.Text = "";
            BtnClearSearch.Visibility = Visibility.Collapsed;
            SuggestionsPopup.IsOpen = false;
            SearchBoxBorder.CornerRadius = new CornerRadius(8);
        }

        private void ActualizarSuggestions()
        {
            if (_vm.ShowSuggestions && _vm.Suggestions.Count > 0)
            {
                var items = _vm.Suggestions.Select(p => new SuggestionItemData
                {
                    Codigo = p.CodigoInterno,
                    Nombre = p.Nombre,
                    Meta   = $"{p.Fabricante} · {p.Pais} · {p.Categoria}",
                    Activo = p.IdEstado == 1,
                    Source = p
                }).ToList();

                SuggestionsList.ItemsSource = items;
                SugCountLabel.Text = $"\u2191\u2193 navegar · \u21b5 seleccionar · {items.Count} coincidencias";
                SuggestionsPopup.IsOpen = true;
                SearchBoxBorder.CornerRadius = new CornerRadius(8, 8, 0, 0);
            }
            else
            {
                SuggestionsPopup.IsOpen = false;
                if (!TxtBusqueda.IsFocused)
                    SearchBoxBorder.CornerRadius = new CornerRadius(8);
            }
        }

        private static readonly SolidColorBrush _highlightBrush =
            new(Color.FromRgb(0xD1, 0xDC, 0xF5));
        private static readonly SolidColorBrush _transparentBrush =
            Brushes.Transparent;

        private void ActualizarHighlight()
        {
            SuggestionsList.UpdateLayout();

            for (int i = 0; i < SuggestionsList.Items.Count; i++)
            {
                var container = SuggestionsList.ItemContainerGenerator
                    .ContainerFromIndex(i) as ContentPresenter;
                if (container == null) continue;

                var border = VisualTreeHelper.GetChildrenCount(container) > 0
                    ? VisualTreeHelper.GetChild(container, 0) as System.Windows.Controls.Border
                    : null;
                if (border == null) continue;

                border.Background = (i == _vm.HighlightIndex)
                    ? _highlightBrush
                    : _transparentBrush;
            }
        }

        private void SuggestionItem_Click(object sender, WpfMouseButton e)
        {
            if (sender is Border b && b.Tag is SuggestionItemData data)
            {
                _vm.SeleccionarSugerencia(data.Source);
                TxtBusqueda.Text = "";
                BtnClearSearch.Visibility = Visibility.Collapsed;
                SuggestionsPopup.IsOpen = false;
                SearchBoxBorder.CornerRadius = new CornerRadius(8);
                SeleccionarEnTabla();
            }
        }

        private void SuggestionItem_MouseEnter(object sender, WpfMouse e)
        {
            if (sender is Border b && b.Tag is SuggestionItemData data)
            {
                int idx = _vm.Suggestions.IndexOf(data.Source);
                if (idx >= 0) _vm.HighlightIndex = idx;
            }
        }

        private void SeleccionarEnTabla()
        {
            if (_vm.Seleccionado == null) return;
            DgProductos.SelectedItem = _vm.Seleccionado;
            DgProductos.ScrollIntoView(_vm.Seleccionado);
        }

        // ── Table ─────────────────────────────────────────────────────

        private void DgProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgProductos.SelectedItem as ProductoDto;
        }

        private void DgProductos_MouseDoubleClick(object sender, WpfMouseButton e)
        {
            if (_vm?.Seleccionado != null)
                AbrirModalEditar(_vm.Seleccionado);
        }

        // ── Pagination ────────────────────────────────────────────────

        private void RefrescarPaginacion()
        {
            if (_vm == null) return;
            DgProductos.ItemsSource = _vm.PageRows;

            PaginacionPanel.Items.Clear();
            int total   = _vm.TotalPages;
            int current = _vm.Page;

            foreach (var p in CalcularPaginas(current, total))
            {
                if (p == -1)
                {
                    PaginacionPanel.Items.Add(new TextBlock
                    {
                        Text = "\u2026",
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                        FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0),
                        Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString("#6B7280"))
                    });
                }
                else
                {
                    var btn = new System.Windows.Controls.Button
                    {
                        Content = p.ToString(),
                        Margin  = new Thickness(2, 0, 2, 0),
                        Style   = (Style)(p == current
                            ? FindResource("ActivePageBtn")
                            : FindResource("PageBtn")),
                        Tag = p
                    };
                    btn.Click += (s, ev) => { if (s is System.Windows.Controls.Button b && b.Tag is int pg) _vm.Page = pg; };
                    PaginacionPanel.Items.Add(btn);
                }
            }
        }

        private static IEnumerable<int> CalcularPaginas(int current, int total)
        {
            if (total <= 7)
                return Enumerable.Range(1, total);

            var pages = new List<int> { 1 };
            if (current > 3) pages.Add(-1);
            for (int i = Math.Max(2, current - 1); i <= Math.Min(total - 1, current + 1); i++)
                pages.Add(i);
            if (current < total - 2) pages.Add(-1);
            pages.Add(total);
            return pages;
        }

        // ── Modal ─────────────────────────────────────────────────────

        private void AbrirModalNuevo()
        {
            var repo  = App.Services.GetRequiredService<IProductoRepository>();
            var modal = new ProductoModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnProductoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(ProductoDto p)
        {
            var repo  = App.Services.GetRequiredService<IProductoRepository>();
            var modal = new ProductoModal(repo, p);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnProductoGuardado;
            MostrarModal(modal);
        }

        private void MostrarModal(System.Windows.Controls.UserControl modal)
        {
            ModalContent.Content    = modal;
            ModalOverlay.Visibility = Visibility.Visible;

            var sb   = new Storyboard();
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fade, ModalOverlay);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fade);
            sb.Begin();
        }

        private void CerrarModal()
        {
            var sb   = new Storyboard();
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            Storyboard.SetTarget(fade, ModalOverlay);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            fade.Completed += (s, e) =>
            {
                ModalOverlay.Visibility = Visibility.Collapsed;
                ModalContent.Content    = null;
            };
            sb.Children.Add(fade);
            sb.Begin();
        }

        private void OnProductoGuardado()
        {
            CerrarModal();
            _vm.RefrescarDatos();
        }
    }
}
