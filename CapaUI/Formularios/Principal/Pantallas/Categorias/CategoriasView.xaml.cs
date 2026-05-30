using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Categorias.Dtos;
using CapaAplicacion.Categorias.Interfaces;
using CapaUI.Core.Controls;
using Microsoft.Extensions.DependencyInjection;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;

namespace CapaUI.Formularios.Principal.Pantallas.Categorias
{
    public partial class CategoriasView : System.Windows.Controls.UserControl
    {
        private CategoriasViewModel _vm = null!;
        private bool _suppressFilterChange = false;
        private Storyboard? _spinnerStory;

        public CategoriasView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<CategoriasViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;
            DgCategorias.ItemsSource = _vm.PageRows;

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
                case nameof(CategoriasViewModel.PageRows):        RefrescarPaginacion();    break;
                case nameof(CategoriasViewModel.IsLoading):       ActualizarCarga();        break;
                case nameof(CategoriasViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(CategoriasViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(CategoriasViewModel.Seleccionado):    SeleccionarEnTabla();     break;
                case nameof(CategoriasViewModel.ShowSuggestions): ActualizarSuggestions();  break;
            }
        }

        private void ActualizarCarga()
        {
            if (_vm.IsLoading)
            {
                DgCategorias.Visibility = Visibility.Collapsed;
                EmptyState.Visibility   = Visibility.Collapsed;
                LoadingPanel.Visibility = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                DetenerSpinner();
                DgCategorias.Visibility = Visibility.Visible;
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

        private void ActualizarSuggestions()
        {
            SearchBox.SuggestItems = (_vm.ShowSuggestions && _vm.Suggestions.Count > 0)
                ? _vm.Suggestions.Select(c => new SuggestionItemData
                  {
                      Nombre = c.Nombre,
                      Meta   = c.Descripcion,
                      Activo = c.EstadoCategoria,
                      Source = c
                  }).ToList()
                : null;
        }

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((CategoriaDto)e.Source);
            SeleccionarEnTabla();
        }

        private void SeleccionarEnTabla()
        {
            if (_vm.Seleccionado == null) return;
            if (DgCategorias.SelectedItem == _vm.Seleccionado) return;
            DgCategorias.SelectedItem = _vm.Seleccionado;
            DgCategorias.ScrollIntoView(_vm.Seleccionado);
        }

        private void DgCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgCategorias.SelectedItem as CategoriaDto;
        }

        private void DgCategorias_MouseDoubleClick(object sender, WpfMouseButton e)
        {
            if (_vm?.Seleccionado != null)
                AbrirModalEditar(_vm.Seleccionado);
        }

        private void RefrescarPaginacion()
        {
            if (_vm == null) return;
            DgCategorias.ItemsSource = _vm.PageRows;

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
                        FontFamily = new FontFamily("Segoe UI"),
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
            var repo  = App.Services.GetRequiredService<ICategoriaRepository>();
            var modal = new CategoriaModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnCategoriaGuardada;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(CategoriaDto c)
        {
            var repo  = App.Services.GetRequiredService<ICategoriaRepository>();
            var modal = new CategoriaModal(repo, c);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnCategoriaGuardada;
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

        private void OnCategoriaGuardada()
        {
            CerrarModal();
            _vm.RefrescarDatos();
        }
    }
}
