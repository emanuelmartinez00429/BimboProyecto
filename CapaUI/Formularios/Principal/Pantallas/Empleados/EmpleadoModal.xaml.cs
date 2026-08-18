using CapaUI.Core.Controls;
using CapaUI.Core.Validacion;
using CapaAplicacion.Common;
using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Interfaces;
using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CapaUI.Formularios.Principal.Pantallas.Empleados
{
    /// <summary>
    /// Modal de Empleados — crear y editar registros.
    /// </summary>
    public partial class EmpleadoModal : System.Windows.Controls.UserControl
    {
        private readonly IEmpleadoRepository _repo;
        private readonly EmpleadoDto?        _empleado;
        private readonly bool                _esNuevo;
        private ValidadorFormulario          _validador = null!;

        public event Action? Cerrado;
        public event Action? Guardado;

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
                .Campo(TxtNombre, "El nombre").Obligatorio().LargoMaximo(100)
                .Campo(TxtApellido, "El apellido").Obligatorio().LargoMaximo(100)
                .Campo(TxtTelefono, "El teléfono").Telefono()
                .Campo(TxtCorreo, "El correo").Correo()
                .ValidarAlSalirDelCampo();

            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear empleado"  : "Editar empleado";

            if (!_esNuevo && _empleado != null)
            {
                RowEstado.Visibility = Visibility.Visible;

                TxtNombre.Text    = _empleado.NombreEmpleado;
                TxtApellido.Text  = _empleado.ApellidoEmpleado;
                TxtIdentidad.Text = _empleado.NumeroIdentidad;
                TxtTelefono.Text  = _empleado.TelefonoEmpleado;
                TxtCorreo.Text    = _empleado.CorreoEmpleado;

                RbActivo.IsChecked   = _empleado.IdEstado == 1;
                RbInactivo.IsChecked = _empleado.IdEstado != 1;
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
            // que la aplicacion se colgo.
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
                    var r = await _repo.UpdateAsync(dto);
                    (exito, error) = (r.Success, r.Error);
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
