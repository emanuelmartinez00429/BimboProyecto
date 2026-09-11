using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Common;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Interfaces;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using Microsoft.Extensions.DependencyInjection;
using WpfKey         = System.Windows.Input.KeyEventArgs;
using WpfMouseButton = System.Windows.Input.MouseButtonEventArgs;
using Key            = System.Windows.Input.Key;

namespace CapaUI.Formularios.Principal.Pantallas.Proveedores
{
    public partial class ProveedoresView : System.Windows.Controls.UserControl
    {
        private ProveedoresViewModel _vm = null!;

        public ProveedoresView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.CrearVm<ProveedoresViewModel>();
            _vm.SolicitarNuevo   += AbrirModalNuevo;
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;

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
                Serilog.Log.Error(ex, "[Proveedores] Falló la carga inicial de la pantalla");
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SolicitarNuevo   -= AbrirModalNuevo;
            _vm.SolicitarEditar  -= AbrirModalEditar;
            _vm.PropertyChanged  -= OnVmPropertyChanged;
            _vm.Dispose();
            DataContext = null;
            _vm = null!;
        }

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            if (ev.PropertyName == nameof(ProveedoresViewModel.Seleccionado) && _vm.Seleccionado != null)
            {
                DgProveedores.ScrollIntoView(_vm.Seleccionado);
            }
        }

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((ProveedorDto)e.Source);
        }

        private void DgProveedores_MouseDoubleClick(object sender, WpfMouseButton e)
        {
            if (_vm?.Seleccionado != null)
                AbrirModalEditar(_vm.Seleccionado);
        }

        // Enter con una fila seleccionada abre el modal de edición, igual que el
        // doble clic — antes Enter solo hacía la navegación de celda por defecto
        // de WPF (sin efecto real acá, la grilla es IsReadOnly), inconsistente
        // con que el mouse sí tuviera una acción.
        private void DgProveedores_PreviewKeyDown(object sender, WpfKey e)
        {
            if (e.Key != Key.Enter || _vm?.Seleccionado == null) return;
            e.Handled = true;
            AbrirModalEditar(_vm.Seleccionado);
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

        private void OnProveedorGuardado()
        {
            CerrarModal();
            _vm.RefrescarTrasGuardar();
        }
    }
}
