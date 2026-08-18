using CapaUI.Core.Controls;
using CapaUI.Core.Validacion;
using CapaAplicacion.Common;
using CapaAplicacion.Fabricantes.Dtos;
using CapaAplicacion.Fabricantes.Interfaces;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CapaUI.Formularios.Principal.Pantallas.Fabricantes
{
    public partial class FabricanteModal : System.Windows.Controls.UserControl
    {
        private readonly IFabricanteRepository _repo;
        private readonly FabricanteDto?        _fabricante;
        private readonly bool                  _esNuevo;
        private ValidadorFormulario            _validador = null!;

        public event Action? Cerrado;
        public event Action? Guardado;

        public FabricanteModal(IFabricanteRepository repo, FabricanteDto? fabricante)
        {
            _repo       = repo;
            _fabricante = fabricante;
            _esNuevo    = fabricante == null;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Proveedor y País no se validan: son opcionales por diseño y el combo
            // ofrece "(Ninguno)" como primera opción.
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtNombre, "El nombre").Obligatorio().LargoMaximo(100)
                .Campo(TxtDescripcion, "La descripción").LargoMaximo(255)
                .ValidarAlSalirDelCampo();

            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear fabricante"  : "Editar fabricante";

            var rProv = await _repo.GetProveedoresAsync();
            if (rProv.Success)
            {
                CmbProveedor.Items.Clear();
                CmbProveedor.Items.Add(new ComboBoxItem { Content = "(Ninguno)", Tag = (int?)null });
                foreach (var p in rProv.Value!)
                    CmbProveedor.Items.Add(new ComboBoxItem { Content = p.Nombre, Tag = p.Id });
            }
            else
            {
                MessageBox.Show($"No se pudieron cargar los proveedores.\n{rProv.Error}",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var rPaises = await _repo.GetPaisesAsync();
            if (rPaises.Success)
            {
                CmbPais.Items.Clear();
                CmbPais.Items.Add(new ComboBoxItem { Content = "(Ninguno)", Tag = (int?)null });
                foreach (var p in rPaises.Value!)
                    CmbPais.Items.Add(new ComboBoxItem { Content = p.Nombre, Tag = p.Id });
            }
            else
            {
                MessageBox.Show($"No se pudieron cargar los países.\n{rPaises.Error}",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnGuardar.IsEnabled = false;
            }

            if (!_esNuevo && _fabricante != null)
            {
                TxtNombre.Text       = _fabricante.Nombre;
                TxtDescripcion.Text  = _fabricante.Descripcion;

                foreach (ComboBoxItem item in CmbProveedor.Items)
                    if (item.Tag is int pid && pid == _fabricante.IdProveedor) { CmbProveedor.SelectedItem = item; break; }

                foreach (ComboBoxItem item in CmbPais.Items)
                    if (item.Tag is int paisId && paisId == _fabricante.IdPais) { CmbPais.SelectedItem = item; break; }

                RbActivo.IsChecked   = _fabricante.IdEstado == EstadoRegistro.Activo;
                RbInactivo.IsChecked = _fabricante.IdEstado != EstadoRegistro.Activo;
            }
            else
            {
                CmbProveedor.SelectedIndex = 0;
                CmbPais.SelectedIndex      = 0;
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
                    estabaActivo:   !_esNuevo && _fabricante!.IdEstado == EstadoRegistro.Activo,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "fabricante",
                    nombreRegistro: TxtNombre.Text.Trim()))
                return;

            // Guardar es un viaje de red: sin este aviso la espera se lee como
            // que la aplicacion se colgo.
            var etiquetaGuardar  = BtnGuardar.Content;
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";
            try
            {
                int? idProveedor = CmbProveedor.SelectedItem is ComboBoxItem pi && pi.Tag is int pv ? pv : null;
                int? idPais      = CmbPais.SelectedItem      is ComboBoxItem ci && ci.Tag is int cv ? cv : null;

                var dto = new FabricanteDto
                {
                    Id          = _esNuevo ? 0 : _fabricante!.Id,
                    Nombre      = TxtNombre.Text.Trim(),
                    Descripcion = TxtDescripcion.Text.Trim(),
                    IdProveedor = idProveedor,
                    IdPais      = idPais,
                    IdEstado    = RbActivo.IsChecked == true ? EstadoRegistro.Activo : EstadoRegistro.Inactivo,
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
                        "Ya existe un fabricante con ese nombre.", TxtNombre);
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
