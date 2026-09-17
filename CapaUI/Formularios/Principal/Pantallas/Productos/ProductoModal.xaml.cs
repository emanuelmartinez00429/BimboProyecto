using CapaAplicacion.Common;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaDominio.Reglas;
using CapaUI.Core.Permisos;
using CapaUI.Core.Validacion;
using CapaUI.Core.Seguridad;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapaUI.Formularios.Principal.Pantallas.Productos
{
    public partial class ProductoModal : System.Windows.Controls.UserControl
    {
        private readonly IProductoRepository _repo = null!;
        private readonly ICatalogoRepository _catalogos = null!;
        private readonly ProductoDto?        _producto;
        private readonly bool                _esNuevo;
        private ValidadorFormulario          _validador = null!;
        private readonly SolicitudIdempotente _solicitud = new();
        private ChangeTracker<ProductoSnapshot> _tracker = new(null);

        private sealed record ProductoSnapshot(
            string CodigoInterno,
            string Nombre,
            decimal? Contenido,
            int? IdPresentacion,
            int? IdFabricante,
            int? IdCategoria,
            int? IdPais,
            decimal? PesoTeorico,
            decimal? PesoTara,
            int? IdUnidad,
            decimal? PrecioPorKg);

        // IDs de respaldo de los campos de catálogo. Los textos son solo la
        // etiqueta visible; lo que se persiste es esto.
        // Nullable: las cuatro columnas lo son en la base, hay productos sin
        // presentación/fabricante/categoría/país cargados.
        private int? _idPresentacion;
        private int? _idFabricante;
        private int? _idCategoria;
        private int? _idPais;
        private int? _idProveedor;

        /// <summary>Se lee directo del combo (no de un campo aparte que había que
        /// mantener sincronizado a mano en cada handler) — evita que quede
        /// desactualizado si el usuario cambia la unidad después de cargarla.</summary>
        private int? IdUnidadSeleccionada => (CmbUnidad.SelectedItem as ComboBoxItem)?.Tag as int?;

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
        /// engancha su SizeChanged para reajustar el marco cuando la ventana se
        /// achica con el selector abierto; ver <see cref="OverlayAncestor_SizeChanged"/>.
        /// </summary>
        private Border? _overlayAncestor;

        public event Action? Cerrado;
        public event Action? Guardado;

        /// <summary>
        /// Constructor sin parámetros solo para el diseñador de Visual Studio, que
        /// instancia el control por acá. Deja los servicios en <c>null!</c> porque este
        /// camino no opera el modal. Ver <c>ProductosCargaModal</c> y ADR-028.
        /// </summary>
        public ProductoModal()
        {
            _repo      = null!;
            _catalogos = null!;
            InitializeComponent();
        }

        public ProductoModal(IProductoRepository repo, ICatalogoRepository catalogos, ProductoDto? producto)
        {
            InitializeComponent();

            _repo      = repo;
            _catalogos = catalogos;
            _producto  = producto;
            _esNuevo   = producto == null;
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
            _validador = ValidadorFormulario.Nuevo()
                .Campo(TxtCodigo, "El código").Segun(ReglasProducto.Codigo)
                .Campo(TxtNombre, "El nombre").Segun(ReglasProducto.Nombre)
                .Catalogo(TxtPresentacion, "La presentación", () => _idPresentacion).Obligatorio()
                .Catalogo(TxtCategoria, "La categoría", () => _idCategoria).Obligatorio()
                .Catalogo(TxtProveedor, "El proveedor", () => _idProveedor).Obligatorio()
                .Catalogo(TxtFabricante, "El fabricante", () => _idFabricante).Obligatorio()
                .Campo(TxtContenido, "El contenido").Segun(ReglasProducto.Contenido)
                .Combo(CmbUnidad, "La unidad", () => (CmbUnidad.SelectedItem as ComboBoxItem)?.Tag is int).Obligatorio()
                .Campo(TxtPesoTeorico, "El peso teórico").Segun(ReglasProducto.PesoTeorico)
                .Campo(TxtTara, "La tara").Segun(ReglasProducto.Tara)
                .Campo(TxtPrecioPorKg, "El precio por kg").Segun(ReglasProducto.PrecioPorKg)
                .Catalogo(TxtPais, "El país importado", () => _idPais).Obligatorio()
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
                _idPresentacion = _producto.IdPresentacion;
                _idFabricante   = _producto.IdFabricante;
                _idCategoria    = _producto.IdCategoria;
                _idPais         = _producto.IdPais;
                // Sin esto la lupa de fabricantes abria sin alcance y listaba
                // todos, aunque el formulario ya mostrara un proveedor.
                _idProveedor    = _producto.IdProveedor;

                TxtCodigo.Text       = _producto.CodigoInterno;
                TxtNombre.Text       = _producto.Nombre;
                TxtPresentacion.Text = _producto.Presentacion;
                TxtContenido.Text    = FormatearDecimal(_producto.Contenido);
                SeleccionarUnidad(_producto.IdUnidad);
                TxtPesoTeorico.Text  = FormatearDecimal(_producto.PesoTeorico);
                TxtTara.Text         = FormatearDecimal(_producto.PesoTara);
                TxtProveedor.Text    = _producto.Proveedor;
                TxtFabricante.Text   = _producto.Fabricante;
                TxtCategoria.Text    = _producto.Categoria;
                TxtPais.Text         = _producto.Pais;
                TxtPrecioPorKg.Text  = FormatearDecimal(_producto.PrecioPorKg);
                TxtCreatedAt.Text    = FormatearFecha(_producto.CreatedAt);
                TxtUpdatedAt.Text    = FormatearFecha(_producto.UpdatedAt);

                RbActivo.IsChecked   = _producto.IdEstado == 1;
                RbInactivo.IsChecked = _producto.IdEstado != 1;

                _tracker = new ChangeTracker<ProductoSnapshot>(new ProductoSnapshot(
                    (_producto.CodigoInterno ?? string.Empty).Trim(),
                    (_producto.Nombre ?? string.Empty).Trim(),
                    _producto.Contenido,
                    _producto.IdPresentacion,
                    _producto.IdFabricante,
                    _producto.IdCategoria,
                    _producto.IdPais,
                    _producto.PesoTeorico,
                    _producto.PesoTara,
                    _producto.IdUnidad,
                    _producto.PrecioPorKg));
            }

            // Proteger cambio de estado según permiso RBAC (PRODUCTOS_ELIMINAR).
            bool puedeCambiarEstado = SesionPermisos.Tiene(Permiso.EliminarProducto);
            RbActivo.IsEnabled = puedeCambiarEstado;
            RbInactivo.IsEnabled = puedeCambiarEstado;
            if (!puedeCambiarEstado)
            {
                const string tipSinPermiso = "No tienes permiso para cambiar el estado (activar/desactivar) de productos.";
                RbActivo.ToolTip = tipSinPermiso;
                RbInactivo.ToolTip = tipSinPermiso;
            }

            // El formulario NO se congela al cargar: así los renglones de error
            // del validador (que se insertan bajo el campo) agrandan el marco de
            // verdad en vez de sacar scrollbar. El MaxHeight del XAML lo acota al
            // hueco disponible; la barra queda como último recurso real (ventana
            // demasiado chica). El alto fijo solo se necesita mientras la tabla
            // del selector está abierta — ver AbrirSelector / CerrarSelector.

            // Solo hace falta reajustar el marco congelado si la ventana se
            // achica con el selector abierto; con el formulario a la vista el
            // MaxHeight del XAML ya sigue el tamaño disponible.
            _overlayAncestor = FindAncestor<Border>(this);
            if (_overlayAncestor != null)
                _overlayAncestor.SizeChanged += OverlayAncestor_SizeChanged;

            // Foco en el primer campo al abrir: el usuario no tiene que
            // clickear nada para empezar a escribir.
            TxtCodigo.Focus();
        }

        /// <summary>Margen que el modal deja contra el borde del overlay (igual que el ConverterParameter de MaxHeight en el XAML).</summary>
        private const double MargenOverlay = 48;

        private void OverlayAncestor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Con el formulario a la vista no hay nada que hacer: se auto-dimensiona
            // y el MaxHeight del XAML lo acota al hueco disponible.
            if (_selectorAbierto is null || double.IsNaN(RootGrid.Height)) return;

            // Selector abierto (marco congelado): si la ventana se achicó por
            // debajo del alto fijo, bajarlo al disponible. El scroll interno de
            // la tabla absorbe el recorte.
            double disponible = ((FrameworkElement)sender).ActualHeight - MargenOverlay;
            if (disponible > 0 && RootGrid.Height > disponible)
                RootGrid.Height = disponible;
        }

        private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(start);
            while (parent != null && parent is not T)
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

        // ── Selectores de catálogo ────────────────────────────────────────────

        private void BuscarPresentacion_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Presentaciones(_catalogos), item =>
            {
                _idPresentacion      = item.Id;
                TxtPresentacion.Text = item.Nombre;
            });

        private void BuscarCategoria_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Categorias(_catalogos), item =>
            {
                _idCategoria      = item.Id;
                TxtCategoria.Text = item.Nombre;
            });

        private void BuscarPais_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Paises(_catalogos), item =>
            {
                _idPais      = item.Id;
                TxtPais.Text = item.Nombre;
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
                _idProveedor      = item.Id;
                TxtProveedor.Text = item.Nombre;

                if (cambio && _idFabricante.HasValue)
                {
                    _idFabricante      = null;
                    TxtFabricante.Text = string.Empty;
                }
            });

        /// <summary>
        /// Fabricantes acotados al proveedor elegido (si hay). Usa la misma
        /// factory que el filtro de la vista — la regla del encadenamiento vive
        /// en un solo lugar.
        /// </summary>
        private void BuscarFabricante_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Fabricantes(_catalogos, _idProveedor), async item =>
            {
                _idFabricante      = item.Id;
                TxtFabricante.Text = item.Nombre;

                // Elegir fabricante directo mantiene el proveedor coherente.
                if (item.IdPadre.HasValue)
                {
                    bool cambioProv = _idProveedor != item.IdPadre;
                    _idProveedor = item.IdPadre;

                    if (cambioProv || string.IsNullOrWhiteSpace(TxtProveedor.Text))
                    {
                        try
                        {
                            var r = await _catalogos.GetProveedoresAsync(string.Empty, 1, 200);
                            if (r.Success && r.Value != null)
                            {
                                var prov = r.Value.Items.FirstOrDefault(p => p.Id == item.IdPadre);
                                if (prov != null)
                                    TxtProveedor.Text = prov.Nombre;
                            }
                        }
                        catch
                        {
                            // En caso de fallo de red transitorio, _idProveedor ya quedó consistente.
                        }
                    }
                }
            });

        /// <summary>
        /// Cambia el contenido del modal por la tabla del catálogo. No se
        /// superpone: el formulario se colapsa y el marco toma un alto fijo (el
        /// que tiene el formulario en ese momento), así la tabla lo llena y no
        /// crece/encoge al filtrar. Al elegir un item vuelve el formulario y el
        /// marco se libera para auto-dimensionarse de nuevo.
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

            // Congelar el marco al alto actual del formulario ANTES de colapsarlo:
            // le da a la fila "*" de la tabla una altura de verdad (no depende de
            // su contenido) y evita que el marco crezca/encoja al filtrar.
            RootGrid.Height         = RootGrid.ActualHeight;

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

            // Liberar el marco: el formulario vuelve a auto-dimensionarse, así los
            // renglones de error del validador lo agrandan en vez de sacar barra.
            RootGrid.Height         = double.NaN;

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
            // Se redondea a ReglasProducto.DecimalesPorDefecto — un solo lugar para
            // subir la precisión el día que lo pidan.
            ParseoNumerico.EsDecimalOpcional(TxtContenido.Text, out var contenido);
            ParseoNumerico.EsDecimalOpcional(TxtPesoTeorico.Text, out var pesoTeorico);
            ParseoNumerico.EsDecimalOpcional(TxtTara.Text, out var tara);
            ParseoNumerico.EsDecimalOpcional(TxtPrecioPorKg.Text, out var precioPorKg);
            contenido    = Redondear(contenido);
            pesoTeorico  = Redondear(pesoTeorico);
            tara         = Redondear(tara);
            precioPorKg  = Redondear(precioPorKg);

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
                    Contenido      = contenido,
                    Presentacion   = TxtPresentacion.Text.Trim(),
                    IdFabricante   = _idFabricante,
                    IdCategoria    = _idCategoria,
                    IdEstado       = RbActivo.IsChecked == true ? 1 : 2,
                    IdPais         = _idPais,
                    IdPresentacion = _idPresentacion,
                    PesoTeorico    = pesoTeorico,
                    PesoTara       = tara,
                    IdUnidad       = IdUnidadSeleccionada,
                    PrecioPorKg    = precioPorKg,
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                if (_esNuevo)
                {
                    var r = await _repo.CreateAsync(dto, _solicitud.Obtener("crear_producto", dto), cts.Token);
                    if (!r.Success)
                    {
                        ErroresRepositorio.Mostrar(r.Error,
                            "Ya existe un producto con ese código interno.", TxtCodigo);
                        return;
                    }
                }
                else
                {
                    var snapshotActual = new ProductoSnapshot(
                        dto.CodigoInterno,
                        dto.Nombre,
                        dto.Contenido,
                        dto.IdPresentacion,
                        dto.IdFabricante,
                        dto.IdCategoria,
                        dto.IdPais,
                        dto.PesoTeorico,
                        dto.PesoTara,
                        dto.IdUnidad,
                        dto.PrecioPorKg);

                    bool cambioDatos = _tracker.IsDirty(snapshotActual);
                    bool cambioEstado = dto.IdEstado != _producto!.IdEstado;

                    if (!cambioDatos && !cambioEstado)
                    {
                        Cerrado?.Invoke();
                        return;
                    }

                    if (cambioDatos)
                    {
                        var r = await _repo.UpdateAsync(dto, _solicitud.Obtener("actualizar_producto", dto), cts.Token);
                        if (!r.Success)
                        {
                            ErroresRepositorio.Mostrar(r.Error,
                                "Ya existe un producto con ese código interno.", TxtCodigo);
                            return;
                        }
                    }

                    if (cambioEstado)
                    {
                        var rEstado = await _repo.CambiarEstadoAsync(
                            dto.Id,
                            dto.IdEstado,
                            _solicitud.Obtener("cambiar_estado_producto", new { dto.Id, dto.IdEstado }),
                            cts.Token);

                        if (!rEstado.Success)
                        {
                            if (cambioDatos)
                            {
                                // Los datos generales se consolidaron exitosamente en la llamada previa.
                                _solicitud.Confirmar();
                                MessageBox.Show(
                                    $"Los datos del producto se actualizaron correctamente, pero no se pudo cambiar el estado:\n{rEstado.Error}",
                                    "Aviso de Estado", MessageBoxButton.OK, MessageBoxImage.Warning);
                                Guardado?.Invoke();
                                return;
                            }

                            ErroresRepositorio.Mostrar(rEstado.Error, null, null);
                            return;
                        }
                    }
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

        // "0." + N '#' = hasta N decimales, sin ceros de relleno. Deriva de
        // ReglasProducto.DecimalesPorDefecto para que subir la precisión no
        // requiera tocar el formato de display por separado.
        private static readonly string FormatoDecimal =
            "0." + new string('#', ReglasProducto.DecimalesPorDefecto);

        private static string FormatearDecimal(decimal? valor) =>
            valor?.ToString(FormatoDecimal, CultureInfo.CurrentCulture) ?? string.Empty;

        private static decimal? Redondear(decimal? valor) =>
            valor.HasValue ? Math.Round(valor.Value, ReglasProducto.DecimalesPorDefecto) : null;

        /// <summary>
        /// Puebla el combo desde el catálogo real (<c>unidad_medida</c>), no de una
        /// lista fija en el XAML. Sin filtro de categoría: el contenido de un
        /// producto puede ser masa (g, kg) o volumen (ml, l). El <see cref="ComboBoxItem.Tag"/>
        /// guarda el id real de la unidad; <c>Content</c> es la abreviatura, igual
        /// que mostraba la lista hardcodeada de antes.
        /// </summary>
        private const int UmbralCombo = 200;

        private async Task CargarUnidadesAsync()
        {
            CmbUnidad.Items.Clear();
            CmbUnidad.Items.Add(new ComboBoxItem { Content = "Unidad", Tag = null });

            // La caché vive detrás del repositorio (ADR-026): reabrir el modal no
            // vuelve a consultar la tabla de unidades.
            var r = await Catalogos.Unidades(_catalogos).Cargar(string.Empty, 1, UmbralCombo, default);
            if (r.Success)
            {
                foreach (var u in r.Value!.Items)
                    CmbUnidad.Items.Add(new ComboBoxItem { Content = u.Descripcion, Tag = u.Id });
            }

            CmbUnidad.SelectedIndex = 0;
        }

        /// <summary>
        /// Selecciona la unidad por id (Contenido y Unidad son campos separados
        /// desde que Contenido pasó a ser numérico puro — ya no hace falta separar
        /// un sufijo de texto). Si el producto no tiene unidad cargada (dato viejo,
        /// de antes de que Unidad fuera obligatorio) queda en "Unidad".
        /// </summary>
        private void SeleccionarUnidad(int? idUnidad)
        {
            var item = idUnidad.HasValue
                ? CmbUnidad.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag is int id && id == idUnidad.Value)
                : null;

            CmbUnidad.SelectedItem = item;
            if (item is null) CmbUnidad.SelectedIndex = 0;
        }

        private static string FormatearFecha(DateTime? valor) =>
            valor?.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? string.Empty;

        // ── Filtro de tecleo en campos numéricos ─────────────────────────────
        // El validador (ReglasProducto + Rango) ya rechaza letras al salir del campo o
        // guardar, pero eso deja escribir "fvbdfgbf" entero antes de avisar. Esto corta
        // el caracter inválido en el momento: ni siquiera llega a aparecer en el TextBox.

        private void CajaDecimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var caja = (TextBox)sender;
            string resultado = caja.Text.Remove(caja.SelectionStart, caja.SelectionLength)
                                         .Insert(caja.SelectionStart, e.Text);
            e.Handled = !ParseoNumerico.PuedeSerDecimalEnProgreso(resultado);
        }

        private void CajaDecimal_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string))) { e.CancelCommand(); return; }

            var caja = (TextBox)sender;
            var pegado = (string)e.DataObject.GetData(typeof(string));
            string resultado = caja.Text.Remove(caja.SelectionStart, caja.SelectionLength)
                                         .Insert(caja.SelectionStart, pegado);
            if (!ParseoNumerico.PuedeSerDecimalEnProgreso(resultado))
                e.CancelCommand();
        }
    }
}
