using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>Modo de presentación del proceso de descarga.</summary>
    public enum ModoProceso
    {
        /// <summary>Alta guiada: una sección a la vez, con Atrás/Siguiente.</summary>
        Wizard,
        /// <summary>Edición de un camión existente: todas las secciones visibles.</summary>
        Edicion,
    }

    /// <summary>
    /// Fila editable de producto dentro del proceso. Existe tanto para productos
    /// ya persistidos (edición) como para los que todavía no se guardaron (wizard).
    /// </summary>
    public partial class ProductoEnProceso : ObservableObject
    {
        /// <summary>id_mov_producto; 0 si todavía no se guardó en la BD.</summary>
        public int    IdMovProducto { get; set; }
        public int    IdProducto    { get; init; }
        public string Codigo        { get; init; } = "";
        public string Nombre        { get; init; } = "";
        public double TaraUnitaria  { get; init; }
        public double PesoTeorico   { get; init; }

        /// <summary>Ya tiene pesajes: no se puede quitar de la carga.</summary>
        public bool TienePesajes { get; init; }

        [ObservableProperty] private string _pesoManifestadoTexto = "";
        [ObservableProperty] private string _bultosDeclaradosTexto = "";

        public bool   PuedeQuitar     => !TienePesajes;
        public string MotivoNoQuitar  => TienePesajes
            ? "Este producto ya tiene pesajes registrados y no se puede quitar."
            : "Quitar este producto de la carga";

        public double PesoManifestado =>
            double.TryParse(PesoManifestadoTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

        public int BultosDeclarados =>
            int.TryParse(BultosDeclaradosTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

        public bool EsValido => PesoManifestado > 0 && BultosDeclarados > 0;
    }

    /// <summary>Lo que el modal devuelve al confirmarse.</summary>
    public record ResultadoProceso(
        string Placa,
        string Proveedor,
        int?   IdProveedor,
        string Observaciones,
        IReadOnlyList<ProductoEnProceso> Productos);

    /// <summary>
    /// Proceso de descarga: un único componente con dos modos.
    /// <para/>
    /// <b>Wizard</b> — alta guiada paso a paso (camión → productos),
    /// con instrucciones escritas en cada sección y navegación Atrás/Siguiente.
    /// <para/>
    /// <b>Edición</b> — "megamodal": las mismas tres secciones, todas visibles a la
    /// vez, para corregir lo ya cargado de un camión.
    /// <para/>
    /// Unifica lo que antes eran CamionModal + ProductoCamionModal + el picker,
    /// que estaban repartidos en botones sueltos de tres toolbars distintas.
    /// </summary>
    public partial class ProcesoDescargaModal : System.Windows.Controls.UserControl
    {
        private const int TotalPasos = 2;

        private readonly ModoProceso _modo;
        private readonly CamionPesaje? _camion;   // solo en modo Edición
        private readonly ICatalogoRepository _catalogos;

        private readonly ObservableCollection<ProductoEnProceso> _productos = new();
        private SelectorCatalogoModal? _selectorCatalogo;   // Proveedor y Producto comparten el mismo host
        private ProveedorItem? _proveedorSeleccionado;
        private int  _paso = 1;
        private bool _cargando = true;

        public event Action? Cerrado;
        public event Action<ResultadoProceso>? Confirmado;

        /// <summary>Productos que el usuario quitó y ya existían en la BD (hay que anularlos).</summary>
        public List<int> IdsProductosQuitados { get; } = new();

        public ProcesoDescargaModal(ModoProceso modo, CamionPesaje? camion)
        {
            InitializeComponent();

            _modo      = modo;
            _camion    = camion;
            _catalogos = App.Services.GetRequiredService<ICatalogoRepository>();

            LstProductos.ItemsSource = _productos;

            if (modo == ModoProceso.Edicion && camion is not null)
                PrecargarDesdeCamion(camion);

            ConfigurarModo();
            _cargando = false;
            ActualizarUI();
        }

        // ── Configuración inicial ───────────────────────────────────────────

        private void PrecargarDesdeCamion(CamionPesaje c)
        {
            TxtPlaca.Text     = c.Placa;
            TxtObs.Text       = c.Observaciones;

            TxtProveedor.Text = c.Proveedor;
            _proveedorSeleccionado = c.IdProveedor.HasValue
                ? new ProveedorItem(c.IdProveedor.Value, c.Proveedor)
                : null;

            foreach (var p in c.Productos)
                _productos.Add(new ProductoEnProceso
                {
                    IdMovProducto         = p.Id,
                    IdProducto            = p.IdProducto,
                    Codigo                = p.ProductoCodigo,
                    Nombre                = p.ProductoNombre,
                    TaraUnitaria          = p.TaraUnitaria,
                    PesoTeorico           = p.PesoTeorico,
                    TienePesajes          = p.TienePesajes,
                    PesoManifestadoTexto  = p.PesoManifestado.ToString(CultureInfo.InvariantCulture),
                    BultosDeclaradosTexto = p.BultosDeclarados.ToString(CultureInfo.InvariantCulture),
                });
        }

        private void ConfigurarModo()
        {
            if (_modo == ModoProceso.Edicion)
            {
                TxtEyebrow.Text        = $"EDICIÓN · CAMIÓN {_camion?.Placa}";
                TxtTitulo.Text         = "Editar proceso de descarga";
                TxtBtnPrincipal.Text   = "Guardar cambios";
                BtnAtras.Visibility    = Visibility.Collapsed;
                MostrarTodasLasSecciones();
            }
            else
            {
                TxtTitulo.Text = "Nuevo proceso de descarga";
                IrAPaso(1);
            }
        }

        private void MostrarTodasLasSecciones()
        {
            SecCamion.Visibility     = Visibility.Visible;
            SecProductos.Visibility  = Visibility.Visible;
            SepCamion.Visibility     = Visibility.Visible;

            // Edición: las dos secciones van apiladas. Camión a su alto natural
            // (Observaciones se queda en su MinHeight, no hace falta que estire
            // acá) y Productos se lleva todo lo que sobra del marco cuadrado —
            // su propio ScrollViewer interno resuelve una lista larga sin que
            // el modal entero tenga que crecer.
            ConfigurarFilas(camionEstrella: false, productosEstrella: true);
        }

        /// <summary>
        /// Alterna cuál de las dos secciones recibe la fila "*" del Grid
        /// contenedor (ver XAML: RowSecCamion / RowSecProductos). Solo una a
        /// la vez puede ser "*" — si las dos lo fueran, el marco cuadrado
        /// repartiría el alto sobrante 50/50 entre una sección visible y una
        /// colapsada, y la que sí se ve (p. ej. Observaciones en el paso 1)
        /// solo llegaría a la mitad del espacio real disponible.
        /// </summary>
        private void ConfigurarFilas(bool camionEstrella, bool productosEstrella)
        {
            RowSecCamion.Height    = camionEstrella
                ? new GridLength(1, GridUnitType.Star)
                : GridLength.Auto;
            RowSecProductos.Height = productosEstrella
                ? new GridLength(1, GridUnitType.Star)
                : GridLength.Auto;
        }

        // ── Navegación del wizard ───────────────────────────────────────────

        private void IrAPaso(int paso)
        {
            _paso = Math.Clamp(paso, 1, TotalPasos);

            SecCamion.Visibility    = _paso == 1 ? Visibility.Visible : Visibility.Collapsed;
            SecProductos.Visibility = _paso == 2 ? Visibility.Visible : Visibility.Collapsed;

            // En wizard cada paso va solo: el separador sobra.
            SepCamion.Visibility    = Visibility.Collapsed;

            // Solo la sección visible recibe la fila "*" — así llena todo el
            // marco cuadrado ella sola (Observaciones en el paso 1, la lista
            // de productos en el paso 2).
            ConfigurarFilas(camionEstrella: _paso == 1, productosEstrella: _paso == 2);

            TxtEyebrow.Text      = $"PASO {_paso} DE {TotalPasos}";
            BtnAtras.Visibility  = _paso > 1 ? Visibility.Visible : Visibility.Collapsed;
            TxtBtnPrincipal.Text = _paso == TotalPasos ? "Finalizar" : "Siguiente";

            ActualizarUI();
        }

        private void Atras_Click(object sender, RoutedEventArgs e)
        {
            LimpiarError();
            if (_modo == ModoProceso.Wizard && _paso > 1) IrAPaso(_paso - 1);
        }

        private void Principal_Click(object sender, RoutedEventArgs e)
        {
            LimpiarError();

            if (_modo == ModoProceso.Edicion)
            {
                if (!ValidarTodo(out var error)) { MostrarError(error); return; }
                Confirmar();
                return;
            }

            // Wizard: valida solo el paso actual antes de avanzar.
            if (!ValidarPaso(_paso, out var errorPaso)) { MostrarError(errorPaso); return; }

            if (_paso < TotalPasos) { IrAPaso(_paso + 1); return; }

            if (!ValidarTodo(out var errorFinal)) { MostrarError(errorFinal); return; }
            Confirmar();
        }

        private void Confirmar()
        {
            var prov = _proveedorSeleccionado;
            Confirmado?.Invoke(new ResultadoProceso(
                TxtPlaca.Text.Trim(),
                prov?.Nombre ?? "",
                prov?.Id,
                TxtObs.Text.Trim(),
                _productos.ToList()));
        }

        // ── Validación ──────────────────────────────────────────────────────

        private bool ValidarPaso(int paso, out string error)
        {
            error = "";
            switch (paso)
            {
                case 1:
                    if (_proveedorSeleccionado is null)
                    { error = "Seleccioná el proveedor que envía la carga."; return false; }
                    if (string.IsNullOrWhiteSpace(TxtPlaca.Text))
                    { error = "Ingresá la placa del vehículo."; return false; }
                    return true;

                case 2:
                    if (_productos.Count == 0)
                    { error = "Agregá al menos un producto a la carga."; return false; }
                    var invalido = _productos.FirstOrDefault(p => !p.EsValido);
                    if (invalido is not null)
                    {
                        error = $"Revisá «{invalido.Nombre}»: el peso manifestado y los bultos declarados deben ser mayores que cero.";
                        return false;
                    }
                    return true;

                default:
                    return true;
            }
        }

        private bool ValidarTodo(out string error)
        {
            for (int p = 1; p <= TotalPasos; p++)
                if (!ValidarPaso(p, out error)) return false;
            error = "";
            return true;
        }

        // ── Selector de catálogo (Proveedor y Producto) ─────────────────────

        /// <summary>
        /// Único punto donde se abre un <see cref="SelectorCatalogoModal"/>
        /// dentro de este modal — Proveedor (paso 1) y Producto (paso 2) son
        /// la misma mecánica, solo cambia el <see cref="CatalogoConfig"/> y
        /// qué se hace con el ítem elegido.
        /// </summary>
        private void AbrirSelectorCatalogo(CatalogoConfig cfg, Action<FiltroItem> alSeleccionar)
        {
            // Congela el marco al alto que tiene ahora mismo (paso 1 o 2 del
            // wizard, o el megamodal completo en edición): SelectorCatalogoModal
            // no tiene fondo ni alto propio (ver su XAML), así que sin esto el
            // marco se movía con la cantidad de filas que dejaba el filtro.
            if (double.IsNaN(RootGrid.Height))
                RootGrid.Height = RootGrid.ActualHeight;

            var selector = new SelectorCatalogoModal(cfg);
            selector.Cerrado += CerrarSelectorCatalogo;
            selector.Seleccionado += item =>
            {
                alSeleccionar(item);
                CerrarSelectorCatalogo();
            };

            _selectorCatalogo               = selector;
            CatalogoSelectorHost.Content     = selector;
            CatalogoSelectorHost.Visibility  = Visibility.Visible;
            ContenidoPrincipal.Visibility    = Visibility.Collapsed;
        }

        private void CerrarSelectorCatalogo()
        {
            _selectorCatalogo?.Dispose();
            _selectorCatalogo               = null;
            CatalogoSelectorHost.Content     = null;
            CatalogoSelectorHost.Visibility  = Visibility.Collapsed;
            ContenidoPrincipal.Visibility    = Visibility.Visible;

            // Libera el alto congelado: el wizard sigue auto-dimensionándose
            // por paso como antes, esto solo lo frenaba mientras el selector
            // reemplazaba el contenido.
            RootGrid.Height = double.NaN;
        }

        private void BuscarProveedor_Click(object sender, RoutedEventArgs e) =>
            AbrirSelectorCatalogo(Catalogos.Proveedores(_catalogos), item =>
            {
                _proveedorSeleccionado = new ProveedorItem(item.Id ?? 0, item.Nombre);
                TxtProveedor.Text      = item.Nombre;
                ActualizarUI();
            });

        /// <summary>
        /// Doble clic en el campo de solo lectura abre el selector, igual que en
        /// ProductoModal. Ver el remarks de ese modal para el porqué de
        /// PreviewMouseDoubleClick en vez de MouseDoubleClick.
        /// </summary>
        private void TxtCatalogo_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement campo && campo.Tag is Button lupa)
            {
                e.Handled = true;
                lupa.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        /// <summary>Lo mismo que <see cref="TxtCatalogo_PreviewMouseDoubleClick"/> pero por teclado.</summary>
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

        // ── Productos ───────────────────────────────────────────────────────

        /// <summary>
        /// Acotado al proveedor elegido en el paso 1 (mismo puente
        /// producto→fabricante→proveedor que usa <c>PickerProductoRepository</c>,
        /// ver <c>CatalogoRepository.GetProductosAsync</c>). Sin proveedor
        /// (no debería pasar en el flujo normal, pero por las dudas) muestra
        /// el catálogo completo.
        /// </summary>
        private void AgregarProducto_Click(object sender, RoutedEventArgs e)
        {
            var prov = _proveedorSeleccionado;
            var cfg = Catalogos.Productos(_catalogos, prov?.Id,
                permiteMultiple: true,
                estaYaElegido: id => _productos.Any(p => p.IdProducto == id));

            AbrirSelectorCatalogo(cfg, item =>
            {
                if (_productos.Any(p => p.IdProducto == item.Id))
                {
                    MostrarError($"«{item.Nombre}» ya está en la carga.");
                    return;
                }

                _productos.Add(new ProductoEnProceso
                {
                    IdMovProducto         = 0,              // todavía no persistido
                    IdProducto            = item.Id ?? 0,
                    Codigo                = item.Descripcion, // este catálogo usa Descripcion para el código
                    Nombre                = item.Nombre,
                    TaraUnitaria          = 0,              // la tara efectiva la resuelve la BD al pesar
                    PesoTeorico           = 0,
                    TienePesajes          = false,
                    PesoManifestadoTexto  = "",
                    BultosDeclaradosTexto = "",
                });
                ActualizarUI();
            });
        }

        private void QuitarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not ProductoEnProceso p) return;

            // Doble guarda: el binding ya deshabilita el botón, pero si algo
            // cambiara el estado en el medio, acá no se pierde un pesaje.
            if (p.TienePesajes) return;

            if (p.IdMovProducto > 0) IdsProductosQuitados.Add(p.IdMovProducto);
            _productos.Remove(p);
            ActualizarUI();
        }

        // ── Reacciones a cambios ────────────────────────────────────────────

        private void Placa_Changed(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;

            // Mayúsculas sin perder la posición del cursor.
            int caret = TxtPlaca.CaretIndex;
            string up = TxtPlaca.Text.ToUpperInvariant();
            if (TxtPlaca.Text != up)
            {
                TxtPlaca.Text = up;
                TxtPlaca.CaretIndex = caret;
            }
            ActualizarUI();
        }

        private void CampoProducto_Changed(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
            ActualizarUI();
        }

        private void ActualizarUI()
        {
            PanelSinProductos.Visibility = _productos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            TxtPie.Text = _modo == ModoProceso.Edicion
                ? $"{_productos.Count} producto(s) en la carga"
                : $"Paso {_paso} de {TotalPasos} · {_productos.Count} producto(s)";
        }

        // ── Error ───────────────────────────────────────────────────────────

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
        }

        private void LimpiarError() => PanelError.Visibility = Visibility.Collapsed;

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            // Si el selector está abierto, el X cierra solo el selector.
            if (_selectorCatalogo is not null) { CerrarSelectorCatalogo(); return; }
            Cerrado?.Invoke();
        }
    }
}
