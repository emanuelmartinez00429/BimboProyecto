using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using static CapaAplicacion.Common.EstadoRegistro;
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
        private Storyboard? _spinnerStory;
        private List<FiltroItem> _todosFabricantes = new();
        private System.ComponentModel.ICollectionView? _fabricantesView;
        private List<FiltroItem> _todosPaises = new();
        private System.ComponentModel.ICollectionView? _paisesView;

        public event Action? SalirSolicitado;

        public ProductosView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Guard: Loaded puede dispararse varias veces (re-parenting en WPF).
            // Sin este guard, dos VMs se suscriben al mismo canal Realtime y el primero
            // queda retenido para siempre en RealtimeService._suscriptores.
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<ProductosViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;
            DgProductos.ItemsSource = _vm.PageRows;

            await _vm.CargarDatosAsync();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SolicitarNuevo   -= AbrirModalNuevo;
            _vm.SolicitarEditar  -= AbrirModalEditar;
            _vm.FiltrosLimpiados -= OnFiltrosLimpiados;
            _vm.PropertyChanged  -= OnVmPropertyChanged;
            _vm.Dispose();
            DataContext = null;
            _vm = null!;          // permite recrear limpio si el control vuelve al árbol
            DetenerSpinner();     // cierra el Storyboard para liberar SpinnerPath
        }

        private void OnFiltrosLimpiados()
        {
            _suppressFilterChange = true;
            RbHabilitados.IsChecked = true;
            if (_fabricantesView != null) _fabricantesView.Filter = null;
            CmbFabricante.SelectedIndex = 0;
            if (_paisesView != null) _paisesView.Filter = null;
            CmbPais.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(ProductosViewModel.PageRows):        RefrescarPaginacion();   break;
                case nameof(ProductosViewModel.IsLoading):       ActualizarCarga();       break;
                case nameof(ProductosViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ProductosViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ProductosViewModel.Seleccionado):    SeleccionarEnTabla();    break;
                case nameof(ProductosViewModel.Fabricantes):     PoblarFabricantes();     break;
                case nameof(ProductosViewModel.Paises):          PoblarPaises();          break;
                case nameof(ProductosViewModel.ShowSuggestions): ActualizarSuggestions(); break;
                // HighlightIndex: resuelto por ListBox.SelectedIndex OneWay binding
            }
        }

        // ── Loading state ─────────────────────────────────────────────

        private void ActualizarCarga()
        {
            if (_vm.IsLoading)
            {
                DgProductos.Visibility  = Visibility.Collapsed;
                EmptyState.Visibility   = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                DetenerSpinner();
                DgProductos.Visibility  = Visibility.Visible;
            }
        }

        // ── Spinner ───────────────────────────────────────────────────

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
            _spinnerStory.Remove();        // desasocia el clock del elemento destino
            _spinnerStory.Children.Clear(); // corta la referencia a SpinnerPath
            _spinnerStory = null;
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
            if (_fabricantesView != null) _fabricantesView.Filter = null;
        }

        private void CmbFabricante_PreviewKeyUp(object sender, WpfKey e)
        {
            if (e.Key is Key.Return or Key.Enter or Key.Up or Key.Down or Key.Escape or Key.Tab)
                return;

            if (_fabricantesView == null) return;
            var texto = CmbFabricante.Text?.Trim() ?? "";
            _fabricantesView.Filter = string.IsNullOrEmpty(texto)
                ? null
                : o => o is FiltroItem f && (f.Nombre?.Contains(texto, StringComparison.OrdinalIgnoreCase) == true);
            CmbFabricante.IsDropDownOpen = true;
        }

        private void CmbPais_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            var selected = CmbPais.SelectedItem as FiltroItem;
            _vm.PaisIdFiltro = selected?.Id;
            // Al seleccionar, quitar el filtro de texto para que el dropdown muestre todo la próxima vez
            if (_paisesView != null) _paisesView.Filter = null;
        }

        private void CmbPais_PreviewKeyUp(object sender, WpfKey e)
        {
            if (e.Key is Key.Return or Key.Enter or Key.Up or Key.Down or Key.Escape or Key.Tab)
                return;

            if (_paisesView == null) return;
            var texto = CmbPais.Text?.Trim() ?? "";
            _paisesView.Filter = string.IsNullOrEmpty(texto)
                ? null
                : o => o is FiltroItem f && (f.Nombre?.Contains(texto, StringComparison.OrdinalIgnoreCase) == true);
            CmbPais.IsDropDownOpen = true;
        }

        private void PoblarFabricantes()
        {
            _suppressFilterChange = true;
            _todosFabricantes = new List<FiltroItem> { new FiltroItem { Id = null, Nombre = "(Todos)" } };
            _todosFabricantes.AddRange(_vm.Fabricantes);
            _fabricantesView = CollectionViewSource.GetDefaultView(_todosFabricantes);
            CmbFabricante.ItemsSource = _fabricantesView;
            CmbFabricante.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        private void PoblarPaises()
        {
            _suppressFilterChange = true;
            _todosPaises = new List<FiltroItem> { new FiltroItem { Id = null, Nombre = "(Todos)" } };
            _todosPaises.AddRange(_vm.Paises);
            _paisesView = CollectionViewSource.GetDefaultView(_todosPaises);
            CmbPais.ItemsSource   = _paisesView;
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
                    Activo = p.IdEstado == Activo,
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
            if (DgProductos.SelectedItem == _vm.Seleccionado) return;
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
                    btn.Click += (s, ev) =>
                    {
                        if (_vm.IsLoading) return;
                        if (s is System.Windows.Controls.Button b && b.Tag is int pg) _vm.Page = pg;
                    };
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
            ModalOverlay.Opacity    = 1;
            ModalOverlay.Visibility = Visibility.Visible;
        }

        private void CerrarModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalContent.Content    = null;
        }

        private void OnProductoGuardado()
        {
            CerrarModal();
            _vm.RefrescarDatos();
        }
    }
}
