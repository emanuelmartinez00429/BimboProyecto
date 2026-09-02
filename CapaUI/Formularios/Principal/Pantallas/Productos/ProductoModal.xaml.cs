using CapaAplicacion.Common;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaDominio.Reglas;
using CapaUI.Core.Validacion;
using CapaUI.Core.Seguridad;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public partial class ProductoModal : System.Windows.Controls.UserControl
    {
        private readonly IProductoRepository _repo;
        private readonly ICatalogoRepository _catalogos;
        private readonly ProductoDto?        _producto;
        private readonly bool                _esNuevo;
        private ValidadorFormulario          _validador = null!;
        private readonly SolicitudIdempotente _solicitud = new();

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

        /// <summary>
        /// Qué tenía el foco antes de abrir la tabla de selección (normalmente la
        /// propia lupa). Al cerrarla hay que devolvérselo: el elemento enfocado
        /// vivía dentro del selector, que se destruye, y el foco de teclado queda
        /// fuera del modal. Eso no solo corta la tabulación — también deja mudo a
        /// Ctrl+Enter, porque PreviewKeyDown es un evento de túnel que baja hasta
        /// el elemento enfocado: si el foco no está dentro del modal, el handler
        /// de AtajoGuardar nunca queda en la ruta del evento.
        /// </summary>
        private IInputElement? _focoPrevio;

        /// <summary>
        /// El Border del overlay (ModalOverlay en la vista que lo hospeda) — el
        /// mismo que ya usan los bindings de MaxWidth/MaxHeight del XAML. Se
        /// engancha su SizeChanged para volver a medir el modal cuando la
        /// ventana cambia de tamaño de verdad; ver <see cref="FijarAlturaOriginal"/>.
        /// </summary>
        private Border? _overlayAncestor;

        public event Action? Cerrado;
        public event Action? Guardado;

        public ProductoModal(IProductoRepository repo, ProductoDto? producto)
        {
            _repo      = repo;
            _catalogos = App.Services.GetRequiredService<ICatalogoRepository>();
            _producto  = producto;
            _esNuevo   = producto == null;
            InitializeComponent();
            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_overlayAncestor != null)
                _overlayAncestor.SizeChanged -= OverlayAncestor_SizeChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Los campos de catálogo (Presentación, Fabricante…) no se validan:
            // son opcionales en la base a propósito.
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtCodigo, "El código").Segun(ReglasProducto.Codigo)
                .Campo(TxtNombre, "El nombre").Segun(ReglasProducto.Nombre)
                .Campo(TxtPesoTeorico, "El peso teórico").Segun(ReglasProducto.PesoTeorico)
                .Campo(TxtPrecioPorKg, "El precio por kg").Segun(ReglasProducto.PrecioPorKg)
                .ValidarAlSalirDelCampo();

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

            // Reenganchar contra el resize real de la ventana: sin esto, el
            // alto quedaba fijo para siempre al valor del primer Loaded (ver
            // FijarAlturaOriginal) y el modal no volvía a crecer si se abría
            // con la ventana chica y esta se agrandaba después — ni mostraba
            // scrollbar si se abría grande y la ventana se achicaba (el marco
            // quedaba más alto que el hueco disponible y el ClipToBounds del
            // Border raíz lo cortaba en silencio). Es el mismo Border que ya
            // usan MaxWidth/MaxHeight arriba en el XAML (RelativeSource
            // AncestorType=Border) — un solo contenedor, una sola fuente de
            // verdad del tamaño disponible.
            _overlayAncestor = FindAncestor<Border>(this);
            if (_overlayAncestor != null)
                _overlayAncestor.SizeChanged += OverlayAncestor_SizeChanged;

            // Foco en el primer campo al abrir: el usuario no tiene que
            // clickear nada para empezar a escribir.
            TxtCodigo.Focus();
        }

        private void OverlayAncestor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Soltar el alto congelado y volver a fijarlo contra el nuevo
            // tamaño disponible — mismo mecanismo que el Loaded inicial, solo
            // que disparado por un resize real en vez de la primera carga.
            RootGrid.Height = double.NaN;
            FijarAlturaOriginal();
        }

        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(start);
            while (parent != null && parent is not T)
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }

        /// <summary>
        /// Congela el marco al alto que ocupa el formulario recién cargado (o
        /// recién recalculado tras un resize, ver <see cref="OverlayAncestor_SizeChanged"/>).
        /// Sin esto, el modal se auto-dimensiona a su contenido: cambiar a modo
        /// tabla (<see cref="AbrirSelector"/>) y volver a filtrar dentro de ella
        /// hacía que el marco creciera o encogiera con la cantidad de filas
        /// visibles (el Border de la tabla solo tenía un rango Min/Max, no un
        /// alto fijo). Al fijar RootGrid.Height, el renglón "*" de la tabla
        /// queda con una altura de verdad —ya no depende de su contenido— y el
        /// marco se mantiene del tamaño del modal en ambos modos, hasta el
        /// próximo resize real de la ventana.
        /// Se difiere un tick (DispatcherPriority.Loaded) para leer el alto ya
        /// asentado tras el layout completo, no uno a medio popular.
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
        /// El campo de catálogo (TextBox de solo lectura) funciona como botón: un
        /// <b>doble</b> clic sobre el texto dispara el mismo
        /// <see cref="Button.Click"/> de la lupa emparejada (referencia en
        /// <c>Tag</c>), así el campo entero abre la tabla de selección sin duplicar
        /// la lógica de cada catálogo.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Antes alcanzaba con un clic simple y era molesto: no se podía ni poner
        /// el cursor en el campo sin que se abriera el selector. El doble clic es
        /// además el mismo gesto que ya usa la grilla del propio selector para
        /// elegir una fila.
        /// </para>
        /// <para>
        /// Tiene que ser <c>PreviewMouseDoubleClick</c> y no
        /// <c>MouseDoubleClick</c>: un <see cref="TextBox"/> dispara los dos, pero
        /// la selección de palabra la hace su editor interno durante el burbujeo
        /// de <c>MouseLeftButtonDown</c>. El túnel corre antes y alcanza a
        /// cancelarla con <c>Handled</c>; el burbujeo corre después y dejaría la
        /// palabra resaltada un instante antes de abrir el selector.
        /// </para>
        /// </remarks>
        private void TxtCatalogo_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement campo && campo.Tag is Button lupa)
            {
                e.Handled = true;
                lupa.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        /// <summary>
        /// Lo mismo que <see cref="TxtCatalogo_PreviewMouseDoubleClick"/> pero por
        /// teclado: al llegar al campo con Tab, Enter o Espacio abren el selector.
        /// Sin esto el campo era alcanzable por Tab pero no se podía abrir sin
        /// mouse — había que tabular una vez más hasta la lupa, que sí responde a
        /// Espacio/Enter por ser un Button.
        ///
        /// Acá alcanza con una sola pulsación: el doble clic es para el mouse, que
        /// necesita distinguirse de "poner el cursor en el campo"; con el teclado
        /// esa ambigüedad no existe.
        ///
        /// Enter simple está libre: <see cref="Core.Controls.AtajoGuardar"/> solo
        /// intercepta Ctrl+Enter.
        /// </summary>
        private void TxtCatalogo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            if (sender is FrameworkElement campo && campo.Tag is Button lupa)
            {
                e.Handled = true;
                lupa.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

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
            // El cierre lo dispara el selector (evento Cerrado), no este
            // handler — ver el mismo comentario en ProcesoDescargaModal.
            selector.Cerrado += CerrarSelector;
            selector.Seleccionado += item => alSeleccionar(item);

            _focoPrevio             = Keyboard.FocusedElement;
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

            // Después de reponer FormHost — no se puede enfocar algo colapsado.
            // Si el elemento previo ya no sirve, se cae al primer campo antes que
            // dejar el modal sin foco.
            if (_focoPrevio is UIElement anterior && anterior.Focus()) return;
            TxtCodigo.Focus();
        }

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!_validador.Validar()) return;

            if (!ConfirmacionEstado.Confirmar(
                    esNuevo:        _esNuevo,
                    estabaActivo:   !_esNuevo && _producto!.IdEstado == 1,
                    quedaActivo:    RbActivo.IsChecked == true,
                    entidad:        "producto",
                    nombreRegistro: TxtNombre.Text.Trim()))
                return;

            // Los números ya los validó el validador; acá solo se convierten. Antes
            // el parseo ocurría DENTRO del try, después de poner "Guardando…": el
            // usuario veía el spinner y recién entonces le rechazaban el número.
            ParseoNumerico.EsDecimalOpcional(TxtPesoTeorico.Text, out var pesoTeorico);
            ParseoNumerico.EsDecimalOpcional(TxtPrecioPorKg.Text, out var precioPorKg);

            // Guardar es un viaje de red: sin este aviso el segundo de espera se
            // lee como que la aplicacion se colgo.
            BtnGuardar.IsEnabled = false;
            BtnGuardar.Content   = "Guardando...";
            try
            {
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

                bool exito;
                string error;

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto, _solicitud.Obtener("crear_producto", dto), CancellationToken.None);
                    (exito, error) = (r.Success, r.Error);
                }
                else
                {
                    var r = await _repo.UpdateAsync(dto, _solicitud.Obtener("actualizar_producto", dto), CancellationToken.None);
                    (exito, error) = (r.Success, r.Error);
                }

                if (!exito)
                {
                    ErroresRepositorio.Mostrar(error,
                        "Ya existe un producto con ese código interno.", TxtCodigo);
                    return;
                }
                _solicitud.Confirmar();

                Guardado?.Invoke();
            }
            catch (Exception ex)
            {
                ErroresRepositorio.MostrarInesperado(ex);
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

    }
}
