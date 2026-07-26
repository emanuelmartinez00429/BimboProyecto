using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Interfaces;
using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CapaUI.Formularios.Principal.Pantallas.Empleados
{
    /// <summary>
    /// Modal de Empleados — solo lectura por ahora. Carga y muestra los datos
    /// correctamente (ver/crear), pero <see cref="BtnGuardar_Click"/> es un
    /// no-op deliberado: el módulo está en revisión y no debe persistir nada
    /// todavía. Ver Sesión 2026-07-26 - Módulo Empleados (solo lectura).
    /// </summary>
    public partial class EmpleadoModal : System.Windows.Controls.UserControl
    {
        private readonly IEmpleadoRepository _repo;
        private readonly EmpleadoDto?        _empleado;
        private readonly bool                _esNuevo;

        public event Action? Cerrado;

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

            try
            {
                var uri = new Uri("pack://application:,,,/CapaUI;component/Resources/bimbo_no_bg.png");
                ModalLogo.Source = new BitmapImage(uri);
            }
            catch { }

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
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        /// <summary>
        /// Deshabilitado a propósito (ver docstring de la clase): no llama a
        /// <see cref="_repo"/> ni persiste ningún cambio. Solo informa.
        /// </summary>
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Guardado deshabilitado temporalmente — módulo en revisión.",
                "Módulo en revisión", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
