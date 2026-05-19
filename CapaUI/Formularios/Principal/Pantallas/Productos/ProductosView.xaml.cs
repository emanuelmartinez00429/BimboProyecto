using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using WpfKey = System.Windows.Input.KeyEventArgs;
using WpfMouse = System.Windows.Input.MouseEventArgs;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;
using Producto = CapaDatos.Modelados.Productos.Productos;
using Key = System.Windows.Input.Key;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public class SuggestionItemData
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Meta { get; set; } = "";
        public bool Activo { get; set; }
        public Producto Source { get; set; } = null!;
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
            _vm = new ProductosViewModel();
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
            if (_vm == null) return;
            _vm.FabricanteFiltro = CmbFabricante.SelectedItem as string ?? "";
        }

        private void CmbPais_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.PaisFiltro = CmbPais.SelectedItem as string ?? "";
        }

        private void PoblarFabricantes()
        {
            _suppressFilterChange = true;
            CmbFabricante.Items.Clear();
            CmbFabricante.Items.Add("(Todos)");
            foreach (var f in _vm.Fabricantes) CmbFabricante.Items.Add(f);
            CmbFabricante.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        private void PoblarPaises()
        {
            _suppressFilterChange = true;
            CmbPais.Items.Clear();
            CmbPais.Items.Add("(Todos)");
            foreach (var p in _vm.Paises) CmbPais.Items.Add(p);
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
                    DgProductos.ItemsSource = _vm.PageRows;
                    DgProductos.SelectedItem = _vm.Seleccionado;
                    DgProductos.ScrollIntoView(DgProductos.SelectedItem);
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
                    Codigo = p.codigoProducto ?? "",
                    Nombre = p.nombreProducto ?? "",
                    Meta   = $"{p.nombre_Fabricante} · {p.nombre_Pais} · {p.nombre_Categoria}",
                    Activo = p.idEstado == 1,
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

        private void SuggestionItem_Click(object sender, WpfMouseButton e)
        {
            if (sender is Border b && b.Tag is SuggestionItemData data)
            {
                _vm.SeleccionarSugerencia(data.Source);
                TxtBusqueda.Text = "";
                BtnClearSearch.Visibility = Visibility.Collapsed;
                SuggestionsPopup.IsOpen = false;
                SearchBoxBorder.CornerRadius = new CornerRadius(8);
                DgProductos.ItemsSource = _vm.PageRows;
                DgProductos.SelectedItem = _vm.Seleccionado;
                if (DgProductos.SelectedItem != null)
                    DgProductos.ScrollIntoView(DgProductos.SelectedItem);
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

        // ── Table ─────────────────────────────────────────────────────

        private void DgProductos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgProductos.SelectedItem as Producto;
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
            int total = _vm.TotalPages;
            int current = _vm.Page;

            var pages = CalcularPaginas(current, total);
            foreach (var p in pages)
            {
                if (p == -1)
                {
                    var elipsis = new TextBlock
                    {
                        Text = "\u2026",
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                        FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0),
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280"))
                    };
                    PaginacionPanel.Items.Add(elipsis);
                }
                else
                {
                    var btn = new System.Windows.Controls.Button
                    {
                        Content = p.ToString(),
                        Margin = new Thickness(2, 0, 2, 0),
                        Style = (Style)(p == current
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
                return Enumerable.Range(1, total).Select(x => x);

            var pages = new List<int>();
            pages.Add(1);
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
            var modal = new ProductoModal(null);
            modal.Cerrado    += CerrarModal;
            modal.Guardado   += OnProductoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(Producto p)
        {
            var modal = new ProductoModal(p);
            modal.Cerrado    += CerrarModal;
            modal.Guardado   += OnProductoGuardado;
            MostrarModal(modal);
        }

        private void MostrarModal(System.Windows.Controls.UserControl modal)
        {
            ModalContent.Content    = modal;
            ModalOverlay.Visibility = Visibility.Visible;

            var sb = new Storyboard();
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(fade, ModalOverlay);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(fade);
            sb.Begin();
        }

        private void CerrarModal()
        {
            var sb = new Storyboard();
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
