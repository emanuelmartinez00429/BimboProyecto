using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Common;
using CapaAplicacion.Fabricantes.Dtos;
using CapaAplicacion.Fabricantes.Interfaces;
using CapaAplicacion.Productos.Dtos;
using CapaUI.Core.Controls;
using Microsoft.Extensions.DependencyInjection;
using static CapaAplicacion.Common.EstadoRegistro;
using WpfKey         = System.Windows.Input.KeyEventArgs;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;
using Key            = System.Windows.Input.Key;

namespace CapaUI.Formularios.Principal.Pantallas.Fabricantes
{
    public partial class FabricantesView : System.Windows.Controls.UserControl
    {
        private FabricantesViewModel _vm = null!;
        private bool _suppressFilterChange = false;
        private Storyboard? _spinnerStory;
        private List<FiltroItem> _todosPaises = new();
        private System.ComponentModel.ICollectionView? _paisesView;

        public FabricantesView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<FabricantesViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;
            DgFabricantes.ItemsSource = _vm.PageRows;

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
            if (_paisesView != null) _paisesView.Filter = null;
            CmbPais.SelectedIndex = 0;
            _suppressFilterChange = false;
        }

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(FabricantesViewModel.PageRows):        RefrescarPaginacion();   break;
                case nameof(FabricantesViewModel.IsLoading):       ActualizarCarga();       break;
                case nameof(FabricantesViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(FabricantesViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(FabricantesViewModel.Seleccionado):    SeleccionarEnTabla();    break;
                case nameof(FabricantesViewModel.Paises):          PoblarPaises();          break;
            }
        }

        private void ActualizarCarga()
        {
            if (_vm.IsLoading)
            {
                DgFabricantes.Visibility = Visibility.Collapsed;
                EmptyState.Visibility    = Visibility.Collapsed;
                LoadingPanel.Visibility  = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility  = Visibility.Collapsed;
                DetenerSpinner();
                DgFabricantes.Visibility = Visibility.Visible;
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

        private void CmbPais_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            var selected = CmbPais.SelectedItem as FiltroItem;
            _vm.PaisIdFiltro = selected?.Id;
            if (_paisesView != null) _paisesView.Filter = null;
        }

        private void CmbPais_PreviewKeyUp(object sender, WpfKey e)
        {
            if (e.Key is Key.Return or Key.Enter or Key.Up or Key.Down or Key.Escape or Key.Tab) return;
            if (_paisesView == null) return;
            var texto = CmbPais.Text?.Trim() ?? "";
            _paisesView.Filter = string.IsNullOrEmpty(texto)
                ? null
                : o => o is FiltroItem f && (f.Nombre?.Contains(texto, StringComparison.OrdinalIgnoreCase) == true);
            CmbPais.IsDropDownOpen = true;
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

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((FabricanteDto)e.Source);
            SeleccionarEnTabla();
        }

        private void SeleccionarEnTabla()
        {
            if (_vm.Seleccionado == null) return;
            if (DgFabricantes.SelectedItem == _vm.Seleccionado) return;
            DgFabricantes.SelectedItem = _vm.Seleccionado;
            DgFabricantes.ScrollIntoView(_vm.Seleccionado);
        }

        private void DgFabricantes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgFabricantes.SelectedItem as FabricanteDto;
        }

        private void DgFabricantes_MouseDoubleClick(object sender, WpfMouseButton e)
        {
            if (_vm?.Seleccionado != null) AbrirModalEditar(_vm.Seleccionado);
        }

        private void RefrescarPaginacion()
        {
            if (_vm == null) return;
            DgFabricantes.ItemsSource = _vm.PageRows;
            PaginacionPanel.Items.Clear();
            int total = _vm.TotalPages, current = _vm.Page;
            foreach (var p in CalcularPaginas(current, total))
            {
                if (p == -1)
                {
                    PaginacionPanel.Items.Add(new TextBlock { Text = "\u2026", FontFamily = new FontFamily("Segoe UI"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 2, 0), Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")) });
                }
                else
                {
                    var btn = new System.Windows.Controls.Button { Content = p.ToString(), Margin = new Thickness(2, 0, 2, 0), Style = (Style)(p == current ? FindResource("ActivePageBtn") : FindResource("PageBtn")), Tag = p };
                    btn.Click += (s, ev) => { if (_vm.IsLoading) return; if (s is System.Windows.Controls.Button b && b.Tag is int pg) _vm.Page = pg; };
                    PaginacionPanel.Items.Add(btn);
                }
            }
        }

        private static IEnumerable<int> CalcularPaginas(int current, int total)
        {
            if (total <= 7) return Enumerable.Range(1, total);
            var pages = new List<int> { 1 };
            if (current > 3) pages.Add(-1);
            for (int i = Math.Max(2, current - 1); i <= Math.Min(total - 1, current + 1); i++) pages.Add(i);
            if (current < total - 2) pages.Add(-1);
            pages.Add(total);
            return pages;
        }

        private void AbrirModalNuevo()
        {
            var repo  = App.Services.GetRequiredService<IFabricanteRepository>();
            var modal = new FabricanteModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnFabricanteGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(FabricanteDto f)
        {
            var repo  = App.Services.GetRequiredService<IFabricanteRepository>();
            var modal = new FabricanteModal(repo, f);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnFabricanteGuardado;
            MostrarModal(modal);
        }

        private void MostrarModal(System.Windows.Controls.UserControl modal)
        {
            ModalContent.Content = modal; ModalOverlay.Opacity = 1; ModalOverlay.Visibility = Visibility.Visible;
        }

        private void CerrarModal()
        {
            ModalOverlay.Visibility = Visibility.Collapsed; ModalContent.Content = null;
        }

        private void OnFabricanteGuardado() { CerrarModal(); _vm.RefrescarDatos(); }
    }
}
