using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaUI.Core.Controls;
using CapaUI.Core.Permisos;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Usuarios
{
    public partial class UsuariosView : System.Windows.Controls.UserControl
    {
        private UsuariosViewModel _vm = null!;

        // Combo de filtro de Rol: sentinel "(Todos)", autocompletado en memoria
        // y limpieza reactiva — misma mecánica que los catálogos de Productos.
        private readonly ComboFiltro _filtroRol;

        public UsuariosView()
        {
            InitializeComponent();

            _filtroRol = new ComboFiltro(CmbRol);
            _filtroRol.SeleccionCambiada += id => { if (_vm != null) _vm.RolFiltro = id; };
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null) return;

            _vm = App.CrearVm<UsuariosViewModel>();
            _vm.SolicitarEditar  += AbrirModalEditar;
            _vm.FiltrosLimpiados += OnFiltrosLimpiados;
            _vm.PropertyChanged  += OnVmPropertyChanged;

            DataContext = _vm;

            // Se captura la instancia ANTES del await. Si el usuario cierra la
            // pantalla mientras carga, Unloaded pone _vm = null y la continuación
            // del await volvería sobre una vista ya descargada.
            // Se compara por referencia y no contra null para cubrir también el
            // abrir-cerrar-abrir rápido: ahí _vm no es null, pero es OTRO VM.
            try
            {
                var vm = _vm;
                await vm.CargarDatosAsync();
                if (!ReferenceEquals(_vm, vm)) return;

                if (Window.GetWindow(this)?.DataContext is MainViewModel principal &&
                    principal.ConsumirRegistroNotificacionPendiente("usuarios") is int idUsuario)
                    await vm.NavegarARegistroAsync(idUsuario);
            }
            catch (OperationCanceledException)
            {
                // Navegación rápida: la vista se descargó mientras cargaba. Salida limpia sin error.
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Usuarios] Falló la carga inicial de la pantalla");
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.SolicitarEditar  -= AbrirModalEditar;
            _vm.FiltrosLimpiados -= OnFiltrosLimpiados;
            _vm.PropertyChanged  -= OnVmPropertyChanged;
            _vm.Dispose();
            DataContext = null;
            _vm = null!;
        }

        // ── VM property changes ────────────────────────────────────────
        // Los cambios de IsLoading, NoResults, PageRows, ErrorCarga, HaySeleccionado
        // y Seleccionado los maneja el binding declarativo del XAML.
        // Aquí sólo queda lo que el XAML no puede resolver solo:
        // • Roles → poblar el ComboFiltro con los nuevos ítems.
        // • Seleccionado → hacer scroll en la tabla.

        private void OnVmPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            // Segunda línea de defensa: un PropertyChanged emitido justo durante
            // el Unloaded llegaría con _vm ya anulado.
            if (_vm == null) return;

            switch (ev.PropertyName)
            {
                case nameof(UsuariosViewModel.Roles):      _filtroRol.Poblar(_vm.Roles.Select(r => new FiltroItem { Id = r.IdRol, Nombre = r.NombreRol })); break;
                case nameof(UsuariosViewModel.Seleccionado): SeleccionarEnTabla(); break;
            }
        }

        // ── Filters ────────────────────────────────────────────────────
        // El filtro de ESTADO lo resuelve el binding declarativo (EnumToBooleanConverter).
        // El filtro de ROL lo resuelve ComboFiltro._filtroRol.

        private void OnFiltrosLimpiados()
        {
            // El binding de EstadoFiltro se actualiza porque el VM llama OnPropertyChanged.
            // Sólo hace falta reiniciar el ComboFiltro de Rol (no tiene binding directo).
            _filtroRol.Reiniciar();
        }

        // ── Search ─────────────────────────────────────────────────────

        private void SearchBox_ItemSelected(object? sender, SuggestionItemData e)
        {
            _vm.SeleccionarSugerencia((UsuarioVistaDto)e.Source);
            SeleccionarEnTabla();
        }

        // ── Table ──────────────────────────────────────────────────────

        private void DgUsuarios_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm?.EditarCommand.CanExecute(null) == true)
                _vm.EditarCommand.Execute(null);
        }

        // Enter con una fila seleccionada abre el modal de edición, igual que el
        // doble clic — antes Enter solo hacía la navegación de celda por defecto
        // de WPF (sin efecto real acá, la grilla es IsReadOnly).
        private void DgUsuarios_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            if (_vm?.EditarCommand.CanExecute(null) != true) return;
            e.Handled = true;
            _vm.EditarCommand.Execute(null);
        }

        private void SeleccionarEnTabla()
        {
            if (_vm?.Seleccionado == null) return;
            DgUsuarios.ScrollIntoView(_vm.Seleccionado);
        }



        // ── Modal ──────────────────────────────────────────────────────

        private void AbrirModalEditar(UsuarioVistaDto u)
        {
            if (!SesionPermisos.TieneAlguno(Permiso.ModificarUsuario, Permiso.EliminarUsuario, Permiso.AsignarRolUsuario) ||
                _vm.EsUsuarioSesionActual) return;
            var rolRepo       = App.Services.GetRequiredService<IRolRepository>();
            var usuarioRepo   = App.Services.GetRequiredService<IUsuarioRepository>();
            var modal = new UsuarioModal(usuarioRepo, rolRepo, u);
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
    }
}
