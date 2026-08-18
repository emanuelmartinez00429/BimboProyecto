using CapaUI.Core.Controls;
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
            if (string.IsNullOrWhiteSpace(TxtNombre.Text) || string.IsNullOrWhiteSpace(TxtApellido.Text))
            {
                MessageBox.Show("Nombre y apellido son obligatorios.", "Validacion",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ver CategoriaModal: se avisa solo al pasar a inactivo algo que ya
            // existía y estaba activo.
            bool estabaActivo = !_esNuevo && _empleado!.IdEstado == 1;
            if (estabaActivo && RbInactivo.IsChecked == true &&
                !DialogoConfirmacion.ConfirmarInactivacion("empleado", TxtNombre.Text.Trim()))
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

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto);
                    if (!r.Success)
                    {
                        MessageBox.Show(r.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    var r = await _repo.UpdateAsync(dto);
                    if (!r.Success)
                    {
                        MessageBox.Show(r.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error inesperado: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnGuardar.Content   = etiquetaGuardar;
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
