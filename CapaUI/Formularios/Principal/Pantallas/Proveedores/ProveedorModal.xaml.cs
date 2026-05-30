using CapaAplicacion.Common;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Interfaces;
using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CapaUI.Formularios.Principal.Pantallas.Proveedores
{
    public partial class ProveedorModal : System.Windows.Controls.UserControl
    {
        private readonly IProveedorRepository _repo;
        private readonly ProveedorDto?        _proveedor;
        private readonly bool                 _esNuevo;

        public event Action? Cerrado;
        public event Action? Guardado;

        public ProveedorModal(IProveedorRepository repo, ProveedorDto? proveedor)
        {
            _repo      = repo;
            _proveedor = proveedor;
            _esNuevo   = proveedor == null;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear proveedor"  : "Editar proveedor";

            try
            {
                var uri = new Uri("pack://application:,,,/CapaUI;component/Resources/bimbo_no_bg.png");
                ModalLogo.Source = new BitmapImage(uri);
            }
            catch { }

            if (!_esNuevo && _proveedor != null)
            {
                TxtNombre.Text    = _proveedor.Nombre;
                TxtRtn.Text       = _proveedor.Rtn;
                TxtTelefono.Text  = _proveedor.Telefono;
                TxtCorreo.Text    = _proveedor.Correo;
                TxtDireccion.Text = _proveedor.Direccion;

                RbActivo.IsChecked   = _proveedor.IdEstado == EstadoRegistro.Activo;
                RbInactivo.IsChecked = _proveedor.IdEstado != EstadoRegistro.Activo;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("El nombre es obligatorio.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGuardar.IsEnabled = false;
            try
            {
                var dto = new ProveedorDto
                {
                    Id        = _esNuevo ? 0 : _proveedor!.Id,
                    Nombre    = TxtNombre.Text.Trim(),
                    Rtn       = TxtRtn.Text.Trim(),
                    Telefono  = TxtTelefono.Text.Trim(),
                    Correo    = TxtCorreo.Text.Trim(),
                    Direccion = TxtDireccion.Text.Trim(),
                    IdEstado  = RbActivo.IsChecked == true ? EstadoRegistro.Activo : EstadoRegistro.Inactivo,
                };

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto);
                    if (!r.Success) { MessageBox.Show(r.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                }
                else
                {
                    var r = await _repo.UpdateAsync(dto);
                    if (!r.Success) { MessageBox.Show(r.Error, "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
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
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
