using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Common;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Interfaces;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using Microsoft.Extensions.DependencyInjection;
using static CapaAplicacion.Common.EstadoRegistro;
using WpfKey         = System.Windows.Input.KeyEventArgs;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;
using Key            = System.Windows.Input.Key;

namespace CapaUI.Formularios.Principal.Pantallas.Proveedores
{
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
                case nameof(ProveedoresViewModel.PageRows):
                    DgProveedores.ItemsSource = _vm.PageRows;   // único punto donde hay filas nuevas
                    RefrescarPaginacion();
                    break;
                // Realtime puede crecer TotalPages sin tocar PageRows (INSERT con el
                // usuario en la vieja última página) — sin este case los botones
                // numerados quedan viejos hasta recargar el módulo.
                case nameof(ProveedoresViewModel.TotalPages):      RefrescarPaginacion(); break;
                case nameof(ProveedoresViewModel.IsLoading):       ActualizarCarga();     break;
                case nameof(ProveedoresViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ProveedoresViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(ProveedoresViewModel.Seleccionado):    SeleccionarEnTabla();  break;
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

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((ProveedorDto)e.Source);
            SeleccionarEnTabla();
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

        /// <summary>
        /// Solo el árbol de botones. El rebind de la grilla vive en el case de
        /// PageRows — ver el comentario de ese case.
        /// </summary>
        private void RefrescarPaginacion()
        {
            if (_vm == null) return;

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
            if (!SesionPermisos.Tiene(Permiso.CrearProveedor)) return;
            var repo  = App.Services.GetRequiredService<IProveedorRepository>();
            var modal = new ProveedorModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnProveedorGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(ProveedorDto p)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarProveedor)) return;
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
            _vm.RefrescarTrasGuardar();
        }
    }
}
