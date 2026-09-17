using CapaUI.Core.Controls;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
using CapaAplicacion.Common;
using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Interfaces;
using CapaUI.Core.Permisos;
using System;
using System.Windows;

namespace CapaUI.Formularios.Principal.Pantallas.Empleados
{
    /// <summary>
    /// Modal de Empleados — crear y editar registros con ChangeTracker y manejo de fallos parciales.
    /// </summary>
    public partial class EmpleadoModal : System.Windows.Controls.UserControl
    {
        private readonly IEmpleadoRepository _repo;
        private readonly EmpleadoDto?        _empleado;
        private readonly bool                _esNuevo;
        private ValidadorFormulario          _validador = null!;
        private ChangeTracker<EmpleadoSnapshot> _tracker = new(null);

        private sealed record EmpleadoSnapshot(string Nombre, string Apellido, string Identidad, string Telefono, string Correo);

        public event Action? Cerrado;
        public event Action? Guardado;

        /// <summary>Constructor de diseño (el diseñador de VS instancia por acá). Ver ADR-028.</summary>
        public EmpleadoModal()
        {
            _repo = null!;
            InitializeComponent();
        }

        public EmpleadoModal(IEmpleadoRepository repo, EmpleadoDto? empleado)
        {
            _repo     = repo;
            _empleado = empleado;
            _esNuevo  = empleado == null;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtNombre, "El nombre").Segun(ReglasEmpleado.Nombre)
                .Campo(TxtApellido, "El apellido").Segun(ReglasEmpleado.Apellido)
                .Campo(TxtIdentidad, "El número de identidad").Segun(ReglasEmpleado.Identidad)
                .Campo(TxtTelefono, "El teléfono").Segun(ReglasEmpleado.Telefono)
                .Campo(TxtCorreo, "El correo").Segun(ReglasEmpleado.Correo)
                .ValidarAlSalirDelCampo();

            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear empleado"  : "Editar empleado";

            if (!_esNuevo && _empleado != null)
            {
                RowEstado.Visibility = Visibility.Visible;
                RbActivo.IsEnabled   = SesionPermisos.Tiene(Permiso.EliminarEmpleado);
                RbInactivo.IsEnabled = SesionPermisos.Tiene(Permiso.EliminarEmpleado);

                TxtNombre.Text    = _empleado.NombreEmpleado;
                TxtApellido.Text  = _empleado.ApellidoEmpleado;
                TxtIdentidad.Text = _empleado.NumeroIdentidad;
                TxtTelefono.Text  = _empleado.TelefonoEmpleado;
                TxtCorreo.Text    = _empleado.CorreoEmpleado;

                RbActivo.IsChecked   = _empleado.IdEstado == 1;
                RbInactivo.IsChecked = _empleado.IdEstado != 1;

                _tracker = new ChangeTracker<EmpleadoSnapshot>(new EmpleadoSnapshot(
                    (_empleado.NombreEmpleado ?? string.Empty).Trim(),
                    (_empleado.ApellidoEmpleado ?? string.Empty).Trim(),
                    (_empleado.NumeroIdentidad ?? string.Empty).Trim(),
                    (_empleado.TelefonoEmpleado ?? string.Empty).Trim(),
                    (_empleado.CorreoEmpleado ?? string.Empty).Trim()));
            }
            else
            {
                RowEstado.Visibility = Visibility.Collapsed;
            }

            // Foco en el primer campo al abrir: el usuario no tiene que
            // clickear nada para empezar a escribir.
            TxtNombre.Focus();
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!_validador.Validar()) return;

            if (!ConfirmacionEstado.Confirmar(
                    esNuevo:        _esNuevo,
                    estabaActivo:   !_esNuevo && _empleado!.IdEstado == 1,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "empleado",
                    nombreRegistro: $"{TxtNombre.Text.Trim()} {TxtApellido.Text.Trim()}".Trim()))
                return;

            // Guardar es un viaje de red: sin este aviso la espera se lee como
            // que la aplicación se colgó.
            var etiquetaGuardar  = BtnGuardar.Content;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";
            try
            {
                var dto = new EmpleadoDto
                {
                    IdEmpleado       = _esNuevo ? 0 : _empleado!.IdEmpleado,
                    NombreEmpleado   = TxtNombre.Text.Trim(),
                    ApellidoEmpleado = TxtApellido.Text.Trim(),
                    NumeroIdentidad  = TxtIdentidad.Text.Trim(),
                    TelefonoEmpleado = TxtTelefono.Text.Trim(),
                    CorreoEmpleado   = TxtCorreo.Text.Trim(),
                    IdEstado         = RbActivo.IsChecked == true ? 1 : 2,
                };

                bool exito;
                string error;

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto);
                    (exito, error) = (r.Success, r.Error);
                }
                else
                {
                    var snapshotActual = new EmpleadoSnapshot(
                        dto.NombreEmpleado,
                        dto.ApellidoEmpleado,
                        dto.NumeroIdentidad,
                        dto.TelefonoEmpleado,
                        dto.CorreoEmpleado);

                    bool datosCambiaron = _tracker.IsDirty(snapshotActual);
                    bool estadoCambio   = _empleado != null && dto.IdEstado != _empleado.IdEstado;

                    if (!datosCambiaron && !estadoCambio)
                    {
                        Cerrado?.Invoke();
                        return;
                    }

                    exito = true;
                    error = string.Empty;

                    if (datosCambiaron)
                    {
                        var r = await _repo.UpdateAsync(dto);
                        (exito, error) = (r.Success, r.Error);
                    }

                    if (exito && estadoCambio)
                    {
                        var rEstado = await _repo.CambiarEstadoAsync(dto.IdEmpleado, dto.IdEstado);
                        if (!rEstado.Success)
                        {
                            if (datosCambiaron)
                            {
                                // Manejo honesto de fallo parcial sin rollback destructivo (AP-03 / P-061)
                                MessageBox.Show(
                                    $"Los datos del empleado se actualizaron correctamente, pero no se pudo cambiar el estado:\n{rEstado.Error}",
                                    "Aviso de Estado", MessageBoxButton.OK, MessageBoxImage.Warning);
                                Guardado?.Invoke();
                                return;
                            }

                            (exito, error) = (rEstado.Success, rEstado.Error);
                        }
                    }
                }

                if (!exito)
                {
                    ErroresRepositorio.Mostrar(error,
                        "Ya existe un empleado con ese número de identidad.", TxtIdentidad);
                    return;
                }

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                ErroresRepositorio.MostrarInesperado(ex);
            }
            finally
            {
                BtnGuardar.Content   = etiquetaGuardar;
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
