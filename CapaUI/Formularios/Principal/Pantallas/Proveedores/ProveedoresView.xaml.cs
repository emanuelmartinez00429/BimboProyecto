using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Common;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using static CapaAplicacion.Common.EstadoRegistro;
using WpfKey         = System.Windows.Input.KeyEventArgs;
using WpfMouse       = System.Windows.Input.MouseEventArgs;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;
using Key            = System.Windows.Input.Key;

namespace CapaUI.Formularios.Principal.Pantallas.Proveedores
{
    public class SuggestionItemData
    {
        public string       Nombre { get; set; } = "";
        public string       Meta   { get; set; } = "";
        public bool         Activo { get; set; }
        public ProveedorDto Source { get; set; } = null!;
    }

    public partial class ProveedoresView : System.Windows.Controls.UserControl
    {
        private ProveedoresViewModel _vm = null!;
        private bool _suppressFilterChange = false;
        private Storyboard? _spinnerStory;

        public ProveedoresView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<ProveedoresViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;
            DgProveedores.ItemsSource = _vm.PageRows;

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
            _vm = null!;
            DetenerSpinner();
        }

        private void OnFiltrosLimpiados()
        {
            _suppressFilterChange = true;
            RbHabilitados.IsChecked = true;
            _suppressFilterChange = false;
        }

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(ProveedoresViewModel.PageRows):        RefrescarPaginacion(); break;
                case nameof(ProveedoresViewModel.IsLoading):       ActualizarCarga();     break;
                case nameof(ProveedoresViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ProveedoresViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ProveedoresViewModel.Seleccionado):    SeleccionarEnTabla();  break;
                case nameof(ProveedoresViewModel.ShowSuggestions): ActualizarSuggestions(); break;
            }
        }

        private void ActualizarCarga()
        {
            if (_vm.IsLoading)
            {
                DgProveedores.Visibility = Visibility.Collapsed;
                EmptyState.Visibility    = Visibility.Collapsed;
                LoadingPanel.Visibility  = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility  = Visibility.Collapsed;
                DetenerSpinner();
                DgProveedores.Visibility = Visibility.Visible;
            }
        }

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
            _spinnerStory.Remove();
            _spinnerStory.Children.Clear();
            _spinnerStory = null;
        }

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
                    Nombre = p.Nombre,
                    Meta   = p.Rtn,
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
            if (DgProveedores.SelectedItem == _vm.Seleccionado) return;
            DgProveedores.SelectedItem = _vm.Seleccionado;
            DgProveedores.ScrollIntoView(_vm.Seleccionado);
        }

        private void DgProveedores_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgProveedores.SelectedItem as ProveedorDto;
        }

        private void DgProveedores_MouseDoubleClick(object sender, WpfMouseButton e)
        {
            if (_vm?.Seleccionado != null)
                AbrirModalEditar(_vm.Seleccionado);
        }

        private void RefrescarPaginacion()
        {
            if (_vm == null) return;
            DgProveedores.ItemsSource = _vm.PageRows;

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
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"))
                    });
                }
                else
                {
                    var btn = new System.Windows.Controls.Button
                    {
                        Content = p.ToString(),
                        Margin  = new Thickness(2, 0, 2, 0),
                        Style   = (Style)(p == current ? FindResource("ActivePageBtn") : FindResource("PageBtn")),
                        Tag     = p
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
            if (total <= 7) return Enumerable.Range(1, total);
            var pages = new List<int> { 1 };
            if (current > 3) pages.Add(-1);
            for (int i = Math.Max(2, current - 1); i <= Math.Min(total - 1, current + 1); i++)
                pages.Add(i);
            if (current < total - 2) pages.Add(-1);
            pages.Add(total);
            return pages;
        }

        private void AbrirModalNuevo()
        {
            var repo  = App.Services.GetRequiredService<IProveedorRepository>();
            var modal = new ProveedorModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnProveedorGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(ProveedorDto p)
        {
            var repo  = App.Services.GetRequiredService<IProveedorRepository>();
            var modal = new ProveedorModal(repo, p);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnProveedorGuardado;
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

        private void OnProveedorGuardado()
        {
            CerrarModal();
            _vm.RefrescarDatos();
        }
    }
}
