using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public partial class ProductoModal : System.Windows.Controls.UserControl
    {
        private readonly IProductoRepository _repo;
        private readonly ProductoDto?        _producto;
        private readonly bool                _esNuevo;

        public event Action? Cerrado;
        public event Action? Guardado;

        public ProductoModal(IProductoRepository repo, ProductoDto? producto)
        {
            _repo     = repo;
            _producto = producto;
            _esNuevo  = producto == null;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear producto"  : "Editar producto";

            try
            {
                var uri = new Uri("pack://application:,,,/CapaUI;component/Resources/bimbo_no_bg.png");
                ModalLogo.Source = new BitmapImage(uri);
            }
            catch { }

            var rFab = await _repo.GetFabricantesAsync();
            if (rFab.Success)
            {
                CmbFabricanteModal.Items.Clear();
                foreach (var f in rFab.Value!)
                    CmbFabricanteModal.Items.Add(new ComboBoxItem { Content = f.Nombre, Tag = f.Id });
            }
            else
            {
                MessageBox.Show($"No se pudieron cargar los fabricantes.\n{rFab.Error}",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnGuardar.IsEnabled = false;
            }

            var rCat = await _repo.GetCategoriasAsync();
            if (rCat.Success)
            {
                CmbCategoriaModal.Items.Clear();
                foreach (var c in rCat.Value!)
                    CmbCategoriaModal.Items.Add(new ComboBoxItem { Content = c.Nombre, Tag = c.Id });
            }
            else
            {
                MessageBox.Show($"No se pudieron cargar las categorías.\n{rCat.Error}",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnGuardar.IsEnabled = false;
            }

            if (!_esNuevo && _producto != null)
            {
                TxtCodigo.Text       = _producto.CodigoInterno;
                TxtNombre.Text       = _producto.Nombre;
                TxtPresentacion.Text = _producto.Presentacion;
                TxtContenido.Text    = _producto.Contenido;

                foreach (ComboBoxItem item in CmbFabricanteModal.Items)
                    if (item.Tag is int fid && fid == _producto.IdFabricante) { CmbFabricanteModal.SelectedItem = item; break; }

                foreach (ComboBoxItem item in CmbCategoriaModal.Items)
                    if (item.Tag is int cid && cid == _producto.IdCategoria) { CmbCategoriaModal.SelectedItem = item; break; }

                foreach (ComboBoxItem item in CmbPaisModal.Items)
                    if (item.Content?.ToString() == _producto.Pais) { CmbPaisModal.SelectedItem = item; break; }

                RbActivo.IsChecked   = _producto.IdEstado == 1;
                RbInactivo.IsChecked = _producto.IdEstado != 1;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCodigo.Text) || string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("Código y nombre son obligatorios.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnGuardar.IsEnabled = false;
            try
            {
                int idFabricante = CmbFabricanteModal.SelectedItem is ComboBoxItem fi && fi.Tag is int fid ? fid : 0;
                int idCategoria  = CmbCategoriaModal.SelectedItem  is ComboBoxItem ci && ci.Tag is int cid ? cid : 0;

                var dto = new ProductoDto
                {
                    Id             = _esNuevo ? 0 : _producto!.Id,
                    CodigoInterno  = TxtCodigo.Text.Trim(),
                    Nombre         = TxtNombre.Text.Trim(),
                    Contenido      = TxtContenido.Text.Trim(),
                    Presentacion   = TxtPresentacion.Text.Trim(),
                    IdFabricante   = idFabricante,
                    IdCategoria    = idCategoria,
                    IdEstado       = RbActivo.IsChecked == true ? 1 : 2,
                    IdPais         = _esNuevo ? 0 : _producto!.IdPais,
                    IdPresentacion = _esNuevo ? 0 : _producto!.IdPresentacion,
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
                BtnGuardar.IsEnabled = true;
            }
        }
    }
}
