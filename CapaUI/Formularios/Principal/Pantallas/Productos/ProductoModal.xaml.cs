using CapaAplicacion.Common;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public partial class ProductoModal : System.Windows.Controls.UserControl
    {
        private readonly IProductoRepository _repo;
        private readonly ICatalogoRepository _catalogos;
        private readonly ProductoDto?        _producto;
        private readonly bool                _esNuevo;

        // IDs de respaldo de los campos de catálogo. Los textos son solo la
        // etiqueta visible; lo que se persiste es esto.
        private int  _idPresentacion;
        private int  _idFabricante;
        private int  _idCategoria;
        private int  _idPais;
        private int? _idTara;
        private int? _idProveedor;

        private SelectorCatalogoModal? _selectorAbierto;

        public event Action? Cerrado;
        public event Action? Guardado;

        public ProductoModal(IProductoRepository repo, ProductoDto? producto)
        {
            _repo      = repo;
            _catalogos = App.Services.GetRequiredService<ICatalogoRepository>();
            _producto  = producto;
            _esNuevo   = producto == null;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear producto"  : "Editar producto";

            // Sin consultas al abrir: los nombres de los catálogos ya vienen
            // resueltos dentro del DTO. Cada catálogo se carga recién al abrir
            // su lupa.
            if (!_esNuevo && _producto != null)
            {
                TxtCodigo.Text       = _producto.CodigoInterno;
                TxtNombre.Text       = _producto.Nombre;
                TxtPresentacion.Text = _producto.Presentacion;
                CargarContenido(_producto.Contenido);
                TxtPesoTeorico.Text  = FormatearDecimal(_producto.PesoTeorico);
                TxtTara.Text         = _producto.Tara;
                TxtProveedor.Text    = _producto.Proveedor;
                TxtFabricante.Text   = _producto.Fabricante;
                TxtCategoria.Text    = _producto.Categoria;
                TxtPais.Text         = _producto.Pais;
                TxtPrecioPorKg.Text  = FormatearDecimal(_producto.PrecioPorKg);
                TxtCreatedAt.Text    = FormatearFecha(_producto.CreatedAt);
                TxtUpdatedAt.Text    = FormatearFecha(_producto.UpdatedAt);

                _idPresentacion = _producto.IdPresentacion;
                _idFabricante   = _producto.IdFabricante;
                _idCategoria    = _producto.IdCategoria;
                _idPais         = _producto.IdPais;
                _idTara         = _producto.IdTara;

                RbActivo.IsChecked   = _producto.IdEstado == 1;
                RbInactivo.IsChecked = _producto.IdEstado != 1;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        // ── Selectores de catálogo ────────────────────────────────────────────

        private void BuscarPresentacion_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Presentaciones(_catalogos), item =>
            {
                TxtPresentacion.Text = item.Nombre;
                _idPresentacion      = item.Id ?? 0;
            });

        private void BuscarTara_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Taras(_catalogos), item =>
            {
                TxtTara.Text = item.Nombre;
                _idTara      = item.Id;
            });

        private void BuscarCategoria_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Categorias(_catalogos), item =>
            {
                TxtCategoria.Text = item.Nombre;
                _idCategoria      = item.Id ?? 0;
            });

        private void BuscarPais_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Paises(_catalogos), item =>
            {
                TxtPais.Text = item.Nombre;
                _idPais      = item.Id ?? 0;
            });

        /// <summary>
        /// Elegir proveedor acota la lupa de fabricante. Si el fabricante ya
        /// cargado no pertenece al proveedor nuevo se limpia, para no dejar una
        /// combinación imposible que después falle al guardar.
        /// </summary>
        private void BuscarProveedor_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Proveedores(_catalogos), item =>
            {
                bool cambio       = _idProveedor != item.Id;
                TxtProveedor.Text = item.Nombre;
                _idProveedor      = item.Id;

                if (cambio && _idFabricante != 0)
                {
                    TxtFabricante.Text = string.Empty;
                    _idFabricante      = 0;
                }
            });

        /// <summary>
        /// Fabricantes acotados al proveedor elegido (si hay). Usa la misma
        /// factory que el filtro de la vista — la regla del encadenamiento vive
        /// en un solo lugar.
        /// </summary>
        private void BuscarFabricante_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Fabricantes(_catalogos, _idProveedor), item =>
            {
                TxtFabricante.Text = item.Nombre;
                _idFabricante      = item.Id ?? 0;

                // Elegir fabricante directo mantiene el proveedor coherente.
                if (item.IdPadre.HasValue) _idProveedor = item.IdPadre;
            });

        /// <summary>
        /// Cambia el contenido del modal por la tabla del catálogo. No se
        /// superpone: el formulario se colapsa y el marco se reajusta al alto de
        /// la tabla, así se ve una sola tarjeta y no dos encimadas.
        /// </summary>
        private void AbrirSelector(CatalogoConfig cfg, Action<FiltroItem> alSeleccionar)
        {
            CerrarSelector();

            var selector = new SelectorCatalogoModal(cfg);
            selector.Cerrado += CerrarSelector;
            selector.Seleccionado += item =>
            {
                alSeleccionar(item);
                CerrarSelector();
            };

            _selectorAbierto        = selector;
            SelectorHost.Content    = selector;
            SelectorHost.Visibility = Visibility.Visible;
            FormHost.Visibility     = Visibility.Collapsed;
        }

        private void CerrarSelector()
        {
            _selectorAbierto?.Dispose();
            _selectorAbierto        = null;
            SelectorHost.Content    = null;
            SelectorHost.Visibility = Visibility.Collapsed;
            FormHost.Visibility     = Visibility.Visible;
        }

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCodigo.Text) || string.IsNullOrWhiteSpace(TxtNombre.Text))
            {
                MessageBox.Show("Código y nombre son obligatorios.", "Validación",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Guardar es un viaje de red: sin este aviso el segundo de espera se
            // lee como que la aplicacion se colgo.
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";
            try
            {
                if (!TryParseDecimal(TxtPesoTeorico.Text, "El peso teórico", out var pesoTeorico) ||
                    !TryParseDecimal(TxtPrecioPorKg.Text, "El precio por kg", out var precioPorKg))
                    return;

                var dto = new ProductoDto
                {
                    Id             = _esNuevo ? 0 : _producto!.Id,
                    CodigoInterno  = TxtCodigo.Text.Trim(),
                    Nombre         = TxtNombre.Text.Trim(),
                    Contenido      = ObtenerContenido(),
                    Presentacion   = TxtPresentacion.Text.Trim(),
                    IdFabricante   = _idFabricante,
                    IdCategoria    = _idCategoria,
                    IdEstado       = RbActivo.IsChecked == true ? 1 : 2,
                    IdPais         = _idPais,
                    IdPresentacion = _idPresentacion,
                    PesoTeorico    = pesoTeorico,
                    IdTara         = _idTara,
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
                BtnGuardar.Content   = "Guardar";
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
