using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CapaAplicacion.Categorias.Dtos;
using CapaAplicacion.Categorias.Interfaces;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
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

            _vm = App.CrearVm<CategoriasViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;
            DgCategorias.ItemsSource = _vm.PageRows;

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
                Serilog.Log.Error(ex, "[Categorias] Falló la carga inicial de la pantalla");
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
            _suppressFilterChange = false;
        }

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            switch (ev.PropertyName)
            {
                case nameof(CategoriasViewModel.PageRows):
                    DgCategorias.ItemsSource = _vm.PageRows;   // único punto donde hay filas nuevas
                    break;
                case nameof(CategoriasViewModel.IsLoading):       ActualizarCarga();        break;
                case nameof(CategoriasViewModel.NoResults):
                    EmptyState.Visibility = _vm.NoResults ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(CategoriasViewModel.HaySeleccionado):
                    SelectedInfo.Visibility = _vm.HaySeleccionado ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(CategoriasViewModel.Seleccionado):    SeleccionarEnTabla();     break;
            }
        }

        private void ActualizarCarga()
        {
            if (_vm == null) return;
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
            if (RbActivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoFilter.Activos;
            else if (RbInactivos.IsChecked == true)
                _vm.EstadoFiltro = EstadoFilter.Inactivos;
            else
                _vm.EstadoFiltro = EstadoFilter.Todos;
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

        // Enter con una fila seleccionada abre el modal de edición, igual que el
        // doble clic — antes Enter solo hacía la navegación de celda por defecto
        // de WPF (sin efecto real acá, la grilla es IsReadOnly).
        private void DgCategorias_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || _vm?.Seleccionado == null) return;
            e.Handled = true;
            AbrirModalEditar(_vm.Seleccionado);
        }



        private void AbrirModalNuevo()
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarConfiguracion)) return;
            var repo  = App.Services.GetRequiredService<ICategoriaRepository>();
            var modal = new CategoriaModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnCategoriaGuardada;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(CategoriaDto c)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarConfiguracion)) return;
            var repo  = App.Services.GetRequiredService<ICategoriaRepository>();
            var modal = new CategoriaModal(repo, c);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnCategoriaGuardada;
            MostrarModal(modal);
        }

        private void MostrarModal(System.Windows.Controls.UserControl modal)
        {
            CapaUI.Core.ModalLayout.LimitarAlOverlay(modal, ModalOverlay); // ADR-028: el limite lo pone el overlay, no un ancestro
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
            _vm.RefrescarTrasGuardar();
        }
    }
}
