using CapaAplicacion.Common;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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
        // Nullable: las cuatro columnas lo son en la base, hay productos sin
        // presentación/fabricante/categoría/país cargados.
        private int? _idPresentacion;
        private int? _idFabricante;
        private int? _idCategoria;
        private int? _idPais;
        private int? _idTara;
        private int? _idProveedor;
        private int? _idUnidadContenido;

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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            TxtModalContext.Text = _esNuevo ? "NUEVO REGISTRO" : "EDICIÓN";
            TxtModalTitle.Text   = _esNuevo ? "Crear producto"  : "Editar producto";

            // Único catálogo que sí hace falta cargar al abrir (no por lupa): el
            // combo de unidad necesita sus opciones antes de poder seleccionar la
            // que traiga el producto.
            await CargarUnidadesAsync();

            // Sin más consultas al abrir: los nombres de los demás catálogos ya
            // vienen resueltos dentro del DTO. Cada uno se carga recién al abrir
            // su lupa.
            if (!_esNuevo && _producto != null)
            {
                TxtCodigo.Text       = _producto.CodigoInterno;
                TxtNombre.Text       = _producto.Nombre;
                TxtPresentacion.Text = _producto.Presentacion;
                CargarContenido(_producto.Contenido, _producto.IdUnidad);
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
                // Sin esto la lupa de fabricantes abria sin alcance y listaba
                // todos, aunque el formulario ya mostrara un proveedor.
                _idProveedor    = _producto.IdProveedor;

                RbActivo.IsChecked   = _producto.IdEstado == 1;
                RbInactivo.IsChecked = _producto.IdEstado != 1;
            }

            FijarAlturaOriginal();
            // Foco en el primer campo al abrir: el usuario no tiene que
            // clickear nada para empezar a escribir.
            TxtCodigo.Focus();
        }

        /// <summary>
        /// Congela el marco al alto que ocupa el formulario recién cargado.
        /// Sin esto, el modal se auto-dimensiona a su contenido: cambiar a modo
        /// tabla (<see cref="AbrirSelector"/>) y volver a filtrar dentro de ella
        /// hacía que el marco creciera o encogiera con la cantidad de filas
        /// visibles (el Border de la tabla solo tenía un rango Min/Max, no un
        /// alto fijo). Al fijar RootGrid.Height una sola vez, el renglón "*" de
        /// la tabla queda con una altura de verdad —ya no depende de su
        /// contenido— y el marco se mantiene del tamaño del modal original en
        /// ambos modos.
        /// Se difiere un tick (DispatcherPriority.Loaded) para leer el alto ya
        /// asentado tras el primer layout completo, no uno a medio popular.
        /// </summary>
        private void FijarAlturaOriginal() =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (double.IsNaN(RootGrid.Height))
                    RootGrid.Height = RootGrid.ActualHeight;
            }), DispatcherPriority.Loaded);

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        // ── Selectores de catálogo ────────────────────────────────────────────

        private void BuscarPresentacion_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Presentaciones(_catalogos), item =>
            {
                TxtPresentacion.Text = item.Nombre;
                _idPresentacion      = item.Id;
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
                _idCategoria      = item.Id;
            });

        private void BuscarPais_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Paises(_catalogos), item =>
            {
                TxtPais.Text = item.Nombre;
                _idPais      = item.Id;
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

                if (cambio && _idFabricante.HasValue)
                {
                    TxtFabricante.Text = string.Empty;
                    _idFabricante      = null;
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
                _idFabricante      = item.Id;

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
                    IdUnidad       = _idUnidadContenido,
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
        /// Puebla el combo desde el catálogo real (<c>unidad_medida</c>), no de una
        /// lista fija en el XAML. Sin filtro de categoría: el contenido de un
        /// producto puede ser masa (g, kg) o volumen (ml, l). El <see cref="ComboBoxItem.Tag"/>
        /// guarda el id real de la unidad; <c>Content</c> es la abreviatura, igual
        /// que mostraba la lista hardcodeada de antes.
        /// </summary>
        private async Task CargarUnidadesAsync()
        {
            CmbUnidad.Items.Clear();
            CmbUnidad.Items.Add(new ComboBoxItem { Content = "(Sin seleccionar)", Tag = null });

            var r = await CatalogoCache.ObtenerParaComboAsync(Catalogos.Unidades(_catalogos));
            if (r.Success)
            {
                foreach (var u in r.Value!)
                    CmbUnidad.Items.Add(new ComboBoxItem { Content = u.Descripcion, Tag = u.Id });
            }

            CmbUnidad.SelectedIndex = 0;
        }

        /// <summary>
        /// Selecciona la unidad por id cuando el producto ya la tiene (dato
        /// estructurado, vía <c>productos.id_unidad</c>). Si no la tiene —dato
        /// viejo o de prueba sin id_unidad—, cae al sufijo de texto legado dentro
        /// de <c>contenido</c>, igual que antes de este cambio.
        /// </summary>
        private void CargarContenido(string contenido, int? idUnidad)
        {
            var texto = contenido.Trim();

            if (idUnidad.HasValue)
            {
                var directo = CmbUnidad.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(i => i.Tag is int id && id == idUnidad.Value);
                if (directo is not null)
                {
                    CmbUnidad.SelectedItem = directo;
                    _idUnidadContenido = idUnidad;

                    var sufijoDirecto = $" {directo.Content}";
                    TxtContenido.Text = texto.EndsWith(sufijoDirecto, StringComparison.OrdinalIgnoreCase)
                        ? texto[..^sufijoDirecto.Length].TrimEnd()
                        : texto;
                    return;
                }
            }

            foreach (ComboBoxItem item in CmbUnidad.Items.OfType<ComboBoxItem>().Skip(1))
            {
                var unidad = item.Content?.ToString() ?? string.Empty;
                var sufijo = $" {unidad}";
                if (!texto.EndsWith(sufijo, StringComparison.OrdinalIgnoreCase)) continue;

                TxtContenido.Text = texto[..^sufijo.Length].TrimEnd();
                CmbUnidad.SelectedItem = item;
                _idUnidadContenido = item.Tag as int?;
                return;
            }

            TxtContenido.Text = texto;
            CmbUnidad.SelectedIndex = 0;
            _idUnidadContenido = null;
        }

        /// <summary>
        /// Persiste contenido y unidad en la misma columna, separados por un
        /// espacio (compatibilidad con el buscador y el picker de Pesaje, que
        /// siguen leyendo <c>contenido</c> como texto). De paso deja
        /// <see cref="_idUnidadContenido"/> listo para el DTO — esa es la fuente
        /// estructurada que ahora viaja además del texto.
        /// </summary>
        private string ObtenerContenido()
        {
            var contenido = TxtContenido.Text.Trim();
            string? unidad = null;

            if (CmbUnidad.SelectedItem is ComboBoxItem item && item.Tag is int idUnidad)
            {
                unidad = item.Content?.ToString();
                _idUnidadContenido = idUnidad;
            }
            else
            {
                _idUnidadContenido = null;
            }

            if (string.IsNullOrWhiteSpace(contenido) || string.IsNullOrWhiteSpace(unidad))
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
