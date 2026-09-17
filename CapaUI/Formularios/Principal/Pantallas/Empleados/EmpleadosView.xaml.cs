using System;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using CapaUI.Formularios.Principal.Pantallas.Usuarios;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Empleados
{
    public partial class EmpleadosView : System.Windows.Controls.UserControl
    {
        private EmpleadosViewModel _vm = null!;

        public EmpleadosView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.CrearVm<EmpleadosViewModel>();
            _vm.SolicitarNuevo        += AbrirModalNuevo;
            _vm.SolicitarEditar       += AbrirModalEditar;
            _vm.SolicitarCrearUsuario += AbrirModalCrearUsuario;
            _vm.FiltrosLimpiados      += OnFiltrosLimpiados;
            _vm.PropertyChanged       += OnVmPropertyChanged;

            DataContext = _vm;
            ScrollHorizontalConShift.Habilitar(DgEmpleados);

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
                Serilog.Log.Error(ex, "[Empleados] Falló la carga inicial de la pantalla");
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SolicitarNuevo        -= AbrirModalNuevo;
            _vm.SolicitarEditar       -= AbrirModalEditar;
            _vm.SolicitarCrearUsuario -= AbrirModalCrearUsuario;
            _vm.FiltrosLimpiados      -= OnFiltrosLimpiados;
            _vm.PropertyChanged       -= OnVmPropertyChanged;
            _vm.Dispose();
            DataContext = null;
            _vm = null!;
        }

        // ── VM property changes ────────────────────────────────────────
        // Los cambios de IsLoading, NoResults, PageRows, ErrorCarga, HaySeleccionado
        // y Seleccionado los maneja el binding declarativo del XAML.
        // Aquí sólo queda lo que el XAML no puede resolver solo:
        // • Seleccionado → hacer scroll en la tabla.

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            if (_vm == null) return;

            switch (ev.PropertyName)
            {
                case nameof(EmpleadosViewModel.Seleccionado): SeleccionarEnTabla(); break;
            }
        }

        // ── Filters ────────────────────────────────────────────────────
        // El filtro de ESTADO lo resuelve el binding declarativo (EnumToBooleanConverter).
        // OnFiltrosLimpiados está suscrito pero sin body: el binding reactivo ya lo maneja.

        private void OnFiltrosLimpiados()
        {
            // El binding de EstadoFiltro se actualiza porque el VM llama OnPropertyChanged(nameof(EstadoFiltro)).
            // No hay ComboFiltros de catálogo en Empleados que reiniciar.
        }

        // ── Search ─────────────────────────────────────────────────────

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((EmpleadoDto)e.Source);
            SeleccionarEnTabla();
        }

        // ── Table ──────────────────────────────────────────────────────

        private void DgEmpleados_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.Seleccionado != null)
                AbrirModalEditar(_vm.Seleccionado);
        }

        // Enter con una fila seleccionada abre el modal de edición, igual que el
        // doble clic — antes Enter solo hacía la navegación de celda por defecto
        // de WPF (sin efecto real acá, la grilla es IsReadOnly).
        private void DgEmpleados_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || _vm?.Seleccionado == null) return;
            e.Handled = true;
            AbrirModalEditar(_vm.Seleccionado);
        }

        private void SeleccionarEnTabla()
        {
            if (_vm?.Seleccionado == null) return;
            DgEmpleados.ScrollIntoView(_vm.Seleccionado);
        }



        // ── Modal ──────────────────────────────────────────────────────

        private void AbrirModalNuevo()
        {
            if (!SesionPermisos.Tiene(Permiso.CrearEmpleado)) return;
            var repo  = App.Services.GetRequiredService<IEmpleadoRepository>();
            var modal = new EmpleadoModal(repo, null);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnEmpleadoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalEditar(EmpleadoDto empleado)
        {
            if (!SesionPermisos.Tiene(Permiso.ModificarEmpleado)) return;
            var repo  = App.Services.GetRequiredService<IEmpleadoRepository>();
            var modal = new EmpleadoModal(repo, empleado);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnEmpleadoGuardado;
            MostrarModal(modal);
        }

        private void AbrirModalCrearUsuario(EmpleadoDto emp)
        {
            if (!SesionPermisos.Tiene(Permiso.CrearUsuario)
                || !SesionPermisos.Tiene(Permiso.AsignarRolUsuario)) return;
            var rolRepo     = App.Services.GetRequiredService<IRolRepository>();
            var usuarioRepo = App.Services.GetRequiredService<IUsuarioRepository>();
            var modal = new UsuarioModal(usuarioRepo, rolRepo,
                emp.IdEmpleado, emp.NombreEmpleado, emp.CorreoEmpleado);
            modal.Cerrado  += CerrarModal;
            modal.Guardado += OnUsuarioGuardado;
            MostrarModal(modal);
        }

        private void MostrarModal(UserControl modal)
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

        private void OnUsuarioGuardado()
        {
            CerrarModal();
            _vm.RefrescarTrasGuardar();
        }

        private void OnEmpleadoGuardado()
        {
            CerrarModal();
            _vm.RefrescarTrasGuardar();
        }
    }
}
