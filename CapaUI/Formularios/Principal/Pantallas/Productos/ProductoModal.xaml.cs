using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using System;
using System.Globalization;
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
                CargarContenido(_producto.Contenido);
                TxtPesoTeorico.Text  = FormatearDecimal(_producto.PesoTeorico);
                TxtTara.Text         = _producto.Tara;
                TxtProveedor.Text    = _producto.Proveedor;
                TxtPrecioPorKg.Text  = FormatearDecimal(_producto.PrecioPorKg);
                TxtCreatedAt.Text    = FormatearFecha(_producto.CreatedAt);
                TxtUpdatedAt.Text    = FormatearFecha(_producto.UpdatedAt);

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
                if (!TryParseDecimal(TxtPesoTeorico.Text, "El peso teórico", out var pesoTeorico) ||
                    !TryParseDecimal(TxtPrecioPorKg.Text, "El precio por kg", out var precioPorKg))
                    return;

                int idFabricante = CmbFabricanteModal.SelectedItem is ComboBoxItem fi && fi.Tag is int fid ? fid : 0;
                int idCategoria  = CmbCategoriaModal.SelectedItem  is ComboBoxItem ci && ci.Tag is int cid ? cid : 0;

                var dto = new ProductoDto
                {
                    Id             = _esNuevo ? 0 : _producto!.Id,
                    CodigoInterno  = TxtCodigo.Text.Trim(),
                    Nombre         = TxtNombre.Text.Trim(),
                    Contenido      = ObtenerContenido(),
                    Presentacion   = TxtPresentacion.Text.Trim(),
                    IdFabricante   = idFabricante,
                    IdCategoria    = idCategoria,
                    IdEstado       = RbActivo.IsChecked == true ? 1 : 2,
                    IdPais         = _esNuevo ? 0 : _producto!.IdPais,
                    IdPresentacion = _esNuevo ? 0 : _producto!.IdPresentacion,
                    PesoTeorico    = pesoTeorico,
                    IdTara         = _esNuevo ? 0 : _producto!.IdTara,
                    PrecioPorKg    = precioPorKg,
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

        private static string FormatearDecimal(decimal? valor) =>
            valor?.ToString("0.##", CultureInfo.CurrentCulture) ?? string.Empty;

        /// <summary>
        /// Separa una unidad conocida que venga al final del campo legado
        /// <c>contenido</c>, sin perder el texto completo si no tiene unidad.
        /// </summary>
        private void CargarContenido(string contenido)
        {
            var texto = contenido.Trim();
            foreach (ComboBoxItem item in CmbUnidad.Items.OfType<ComboBoxItem>().Skip(1))
            {
                var unidad = item.Content?.ToString() ?? string.Empty;
                var sufijo = $" {unidad}";
                if (!texto.EndsWith(sufijo, StringComparison.OrdinalIgnoreCase)) continue;

                TxtContenido.Text = texto[..^sufijo.Length].TrimEnd();
                CmbUnidad.SelectedItem = item;
                return;
            }

            TxtContenido.Text = texto;
            CmbUnidad.SelectedIndex = 0;
        }

        /// <summary>
        /// Persiste contenido y unidad en la misma columna, separados por un espacio.
        /// </summary>
        private string ObtenerContenido()
        {
            var contenido = TxtContenido.Text.Trim();
            var unidad = CmbUnidad.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(contenido) ||
                string.IsNullOrWhiteSpace(unidad) ||
                unidad == "(Sin seleccionar)")
                return contenido;

            var sufijo = $" {unidad}";
            if (contenido.EndsWith(sufijo, StringComparison.OrdinalIgnoreCase))
                contenido = contenido[..^sufijo.Length].TrimEnd();

            return $"{contenido} {unidad}";
        }

        private static string FormatearFecha(DateTime? valor) =>
            valor?.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? string.Empty;

        private static bool TryParseDecimal(string texto, string etiqueta, out decimal? valor)
        {
            valor = null;
            if (string.IsNullOrWhiteSpace(texto)) return true;

            if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.CurrentCulture, out var resultado))
            {
                valor = resultado;
                return true;
            }

            MessageBox.Show($"{etiqueta} debe ser un número válido.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
