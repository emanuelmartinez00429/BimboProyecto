using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Presentaciones.Dtos;
using CapaAplicacion.Presentaciones.Interfaces;
using CapaAplicacion.Presentaciones.Queries;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using Microsoft.Extensions.DependencyInjection;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;

namespace CapaUI.Formularios.Principal.Pantallas.Presentaciones
{
    public partial class PresentacionesView : System.Windows.Controls.UserControl
    {
        private PresentacionesViewModel _vm = null!;
        private bool _suppressFilterChange = false;
        private Storyboard? _spinnerStory;

        public PresentacionesView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Guard contra re-parenting: sin él, volver a la pantalla duplicaría
            // las suscripciones de Realtime.
            if (_vm != null) return;

            _vm = App.Services.GetRequiredService<PresentacionesViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;
            DgPresentaciones.ItemsSource = _vm.PageRows;

            try
            {
                var vm = _vm;
                await vm.CargarDatosAsync();
            }
            catch (OperationCanceledException)
            {
                // Navegación rápida: la vista se descargó mientras cargaba.
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Presentaciones] Falló la carga inicial de la pantalla");
            }
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
            RbActivos.IsChecked = true;
            RbOrdenId.IsChecked     = true;
            _suppressFilterChange = false;
        }

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(PresentacionesViewModel.PageRows):
                    DgPresentaciones.ItemsSource = _vm.PageRows;   // único punto donde hay filas nuevas
                    RefrescarPaginacion();
                    break;
                // Realtime puede crecer TotalPages sin tocar PageRows (INSERT con el
                // usuario en la vieja última página) — sin este case los botones
                // numerados quedan viejos hasta recargar el módulo.
                case nameof(PresentacionesViewModel.TotalPages):      RefrescarPaginacion();    break;
                case nameof(PresentacionesViewModel.IsLoading):       ActualizarCarga();        break;
                case nameof(PresentacionesViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(PresentacionesViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(PresentacionesViewModel.Seleccionado):    SeleccionarEnTabla();     break;
            }
        }

        private void ActualizarCarga()
        {
            if (_vm.IsLoading)
            {
                DgPresentaciones.Visibility = Visibility.Collapsed;
                EmptyState.Visibility       = Visibility.Collapsed;
                LoadingPanel.Visibility     = Visibility.Visible;
                IniciarSpinner();
            }
            else
            {
                LoadingPanel.Visibility     = Visibility.Collapsed;
                DetenerSpinner();
                DgPresentaciones.Visibility = Visibility.Visible;
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
            if (RbActivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoFilter.Activos;
            else if (RbInactivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoFilter.Inactivos;
            else
                _vm.EstadoFiltro = EstadoFilter.Todos;
        }

        private void Orden_Changed(object sender, RoutedEventArgs e)
        {
            if (_vm == null || _suppressFilterChange) return;
            if (RbOrdenAZ.IsChecked == true)
                _vm.Orden = OrdenPresentacion.NombreAsc;
            else if (RbOrdenZA.IsChecked == true)
                _vm.Orden = OrdenPresentacion.NombreDesc;
            else
                _vm.Orden = OrdenPresentacion.IdAsc;
        }

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((PresentacionDto)e.Source);
            SeleccionarEnTabla();
        }

        private void SeleccionarEnTabla()
        {
            if (_vm.Seleccionado == null) return;
            if (DgPresentaciones.SelectedItem == _vm.Seleccionado) return;
            DgPresentaciones.SelectedItem = _vm.Seleccionado;
            DgPresentaciones.ScrollIntoView(_vm.Seleccionado);
        }

        private void DgPresentaciones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            _vm.Seleccionado = DgPresentaciones.SelectedItem as PresentacionDto;
        }

        private void DgPresentaciones_MouseDoubleClick(object sender, WpfMouseButton e)
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
                        Text = "…",
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
            if (!SesionPermisos.Tiene(Permiso.ModificarConfiguracion)) return;
            var repo  = App.Services.GetRequiredService<IPresentacionRepository>();
            var modal = new PresentacionModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnPresentacionGuardada;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(PresentacionDto p)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarConfiguracion)) return;
            var repo  = App.Services.GetRequiredService<IPresentacionRepository>();
            var modal = new PresentacionModal(repo, p);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnPresentacionGuardada;
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

        private void OnPresentacionGuardada()
        {
            CerrarModal();
            _vm.RefrescarTrasGuardar();
        }
    }
}
