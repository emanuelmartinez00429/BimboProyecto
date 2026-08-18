using CapaUI.Core.Controls;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
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
        private ValidadorFormulario           _validador = null!;

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
            // RTN, teléfono y correo son opcionales, pero si se llenan tienen que
            // tener forma válida. Hasta ahora iban crudos a la base sin mirarlos.
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtNombre, "El nombre").Segun(ReglasProveedor.Nombre)
                .Campo(TxtRtn, "El RTN").Segun(ReglasProveedor.Rtn)
                .Campo(TxtTelefono, "El teléfono").Segun(ReglasProveedor.Telefono)
                .Campo(TxtCorreo, "El correo").Segun(ReglasProveedor.Correo)
                .Campo(TxtDireccion, "La dirección").Segun(ReglasProveedor.Direccion)
                .ValidarAlSalirDelCampo();

            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear proveedor"  : "Editar proveedor";

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
                    estabaActivo:   !_esNuevo && _proveedor!.IdEstado == EstadoRegistro.Activo,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "proveedor",
                    nombreRegistro: TxtNombre.Text.Trim()))
                return;

            // Guardar es un viaje de red: sin este aviso la espera se lee como
            // que la aplicacion se colgo.
            var etiquetaGuardar  = BtnGuardar.Content;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";
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
                        "Ya existe un proveedor con ese nombre o RTN.", TxtNombre);
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
