using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>
    /// Lo que el modal devuelve al guardar. Lleva el proveedor porque el destino
    /// del producto NO es el camión seleccionado sino la recepción de esa placa
    /// para ESE proveedor — el ViewModel la resuelve, y la crea si no existe.
    /// </summary>
    public record ResultadoProductoCamion(
        int IdProveedor, string Proveedor, int IdProducto,
        double PesoManifestado, int BultosDeclarados, string Observaciones);

    /// <summary>
    /// Agrega o edita UN producto del camión — reemplaza la tabla que antes vivía
    /// dentro de <c>ProcesoDescargaModal</c>.
    /// <para/>
    /// El proveedor se elige acá adentro y arranca con el del camión. Si se cambia,
    /// el producto se archiva bajo la recepción de ese otro proveedor para la misma
    /// placa: abajo siguen siendo dos <c>movimientos</c> (uno por proveedor, cada uno
    /// con su manifiesto y su reporte), pero el operador solo carga «el camión».
    /// <para/>
    /// "Quitar producto" vive acá adentro (no es un botón aparte en la pantalla
    /// principal): en modo editar dispara <see cref="QuitarSolicitado"/>, y quien abrió
    /// el modal confirma con MessageBox nativo y lo cierra — mismo patrón que
    /// "Quitar camión" y "Cerrar/Reabrir producto" en esta pantalla.
    /// </summary>
    public partial class ProductoCamionModal : UserControl
    {
        private readonly CamionPesaje _camion;
        private readonly ProductoCamion? _producto;   // null = agregar
        private readonly ICatalogoRepository _catalogos;

        /// <summary>
        /// Recepciones abiertas de esta placa, una por proveedor. Sirven para saber qué
        /// productos ya están cargados en la recepción del proveedor elegido y no
        /// ofrecerlos de nuevo. El host las refresca tras cada guardado con
        /// <see cref="ActualizarRecepciones"/>.
        /// </summary>
        private IReadOnlyList<CamionPesaje> _recepcionesDePlaca;

        private SelectorCatalogoModal? _selectorCatalogo;
        private ProveedorItem? _proveedorElegido;
        private int? _idProductoElegido;
        private int _agregados;

        /// <summary>Guarda de reentrada: sin esto, dos clics seguidos insertan dos productos.</summary>
        private bool _guardando;

        /// <summary>
        /// Tamaño propio del modal, leído del XAML al construirlo. El buscador de
        /// catálogo necesita bastante más marco que este formulario, así que mientras
        /// está abierto el modal crece y al cerrarlo vuelve acá. Se guarda en vez de
        /// hardcodearse para que cambiar el tamaño en el XAML alcance.
        /// </summary>
        private readonly double _anchoPropio;
        private readonly double _altoPropio;

        public event Action? Cerrado;
        public event Action<ProductoCamion>? QuitarSolicitado;

        /// <summary>
        /// Persiste el producto. Devuelve <c>Task&lt;bool&gt;</c> y no <c>Action</c> a
        /// propósito: el modal necesita ESPERAR el guardado para bloquearse mientras
        /// corre y saber si salió bien antes de limpiarse para el siguiente producto.
        /// Con <c>Action</c> el handler queda como <c>async void</c> y una excepción ahí
        /// tumba la aplicación — mismo motivo que <c>PesajeModal.GuardarYSeguir</c>.
        /// </summary>
        public event Func<ResultadoProductoCamion, Task<bool>>? Guardar;

        public ProductoCamionModal(
            CamionPesaje camion,
            IReadOnlyList<CamionPesaje> recepcionesDePlaca,
            ProductoCamion? producto)
        {
            InitializeComponent();

            _camion             = camion;
            _recepcionesDePlaca = recepcionesDePlaca;
            _producto           = producto;
            _catalogos          = App.Services.GetRequiredService<ICatalogoRepository>();

            _anchoPropio = Width;
            _altoPropio  = Height;

            // El proveedor arranca siempre con el del camión sobre el que se abrió.
            if (_camion.IdProveedor.HasValue)
                _proveedorElegido = new ProveedorItem(_camion.IdProveedor.Value, _camion.Proveedor);
            TxtProveedorSel.Text = _camion.Proveedor;

            if (_producto is null)
            {
                TxtEyebrow.Text    = "NUEVO PRODUCTO";
                TxtTitulo.Text     = "Agregar producto";
                TxtBtnGuardar.Text = "Agregar";
                TxtPie.Text        = $"Se agregará al camión {camion.Placa}";
            }
            else
            {
                _idProductoElegido = _producto.IdProducto;

                TxtEyebrow.Text    = "EDICIÓN · PRODUCTO";
                TxtTitulo.Text     = "Editar producto";
                TxtBtnGuardar.Text = "Guardar cambios";
                TxtPie.Text        = "El producto y su proveedor no se pueden cambiar";

                // En edición ni el proveedor ni el producto se tocan: el producto ya
                // vive en la recepción de ese proveedor.
                BtnSeleccionarProveedor.Visibility = Visibility.Collapsed;
                TxtProveedorLocked.Visibility      = Visibility.Visible;
                TxtProveedorLocked.Text            = _camion.Proveedor;

                BtnSeleccionarProducto.Visibility = Visibility.Collapsed;
                TxtProductoLocked.Visibility      = Visibility.Visible;
                TxtProductoLocked.Text            = $"{_producto.ProductoCodigo}  ·  {_producto.ProductoNombre}";

                // "Guardar y agregar otro" es solo del alta.
                BtnGuardarYOtro.Visibility = Visibility.Collapsed;

                TxtPeso.Text   = _producto.PesoManifestado.ToString(CultureInfo.InvariantCulture);
                TxtBultos.Text = _producto.BultosDeclarados.ToString(CultureInfo.InvariantCulture);
                TxtObs.Text    = _producto.Observaciones;

                BtnQuitar.Visibility = Visibility.Visible;
                bool puedeQuitar     = !_producto.TienePesajes;
                BtnQuitar.IsEnabled  = puedeQuitar;
                BtnQuitar.ToolTip    = puedeQuitar
                    ? "Quitar este producto de la carga"
                    : "Este producto ya tiene pesajes registrados y no se puede quitar.";
            }

            Validar();
        }

        /// <summary>
        /// Refresca las recepciones tras un guardado, para que <c>estaYaElegido</c> no
        /// vuelva a ofrecer el producto que se acaba de agregar (ni ignore la recepción
        /// que el guardado pudo haber creado).
        /// </summary>
        public void ActualizarRecepciones(IReadOnlyList<CamionPesaje> recepciones) =>
            _recepcionesDePlaca = recepciones;

        // ── Selector de catálogo ────────────────────────────────────────────
        // El selector reemplaza todo el contenido del modal mientras está abierto
        // (es "chromeless": hereda este marco, no tiene fondo ni tamaño propio), y
        // el marco crece para que la tabla del buscador entre completa.

        private void SeleccionarProveedor_Click(object sender, RoutedEventArgs e) =>
            AbrirSelector(Catalogos.Proveedores(_catalogos), item =>
            {
                _proveedorElegido = new ProveedorItem(item.Id ?? 0, item.Nombre);
                TxtProveedorSel.Text = item.Nombre;

                // El producto elegido era de otro catálogo: se limpia.
                _idProductoElegido = null;
                TxtSeleccion.Text  = "Seleccionar producto…";
            });

        private void SeleccionarProducto_Click(object sender, RoutedEventArgs e)
        {
            var cfg = Catalogos.Productos(_catalogos, _proveedorElegido?.Id,
                estaYaElegido: id => ProductosDeLaRecepcion().Any(p => p.IdProducto == id));

            AbrirSelector(cfg, item =>
            {
                _idProductoElegido = item.Id;
                // Este catálogo trae el código en Descripcion (ver Catalogos.Productos).
                TxtSeleccion.Text  = $"{item.Descripcion}  ·  {item.Nombre}";
            });
        }

        /// <summary>
        /// Productos ya cargados en la recepción del proveedor ELEGIDO — no de todas
        /// las de la placa: el mismo producto puede venir legítimamente de dos
        /// proveedores distintos en el mismo camión.
        /// </summary>
        private IEnumerable<ProductoCamion> ProductosDeLaRecepcion() =>
            _recepcionesDePlaca
                .FirstOrDefault(c => c.IdProveedor == _proveedorElegido?.Id)
                ?.Productos ?? Enumerable.Empty<ProductoCamion>();

        private void AbrirSelector(CatalogoConfig cfg, Action<FiltroItem> alSeleccionar)
        {
            var selector = new SelectorCatalogoModal(cfg);
            selector.Cerrado += CerrarSelectorCatalogo;
            // NO se cierra el selector acá: lo cierra él mismo (evento Cerrado) apenas
            // termina de emitir. Cerrar desde este handler lo dispone a mitad de su
            // propio bucle de emisión y la excepción que sale de ahí se lleva la app
            // puesta — no hay DispatcherUnhandledException que la ataje.
            selector.Seleccionado += item => { alSeleccionar(item); Validar(); };

            _selectorCatalogo               = selector;
            CatalogoSelectorHost.Content     = selector;
            CatalogoSelectorHost.Visibility  = Visibility.Visible;
            ContenidoPrincipal.Visibility    = Visibility.Collapsed;
            AplicarMarcoSelector(true);
        }

        private void CerrarSelectorCatalogo()
        {
            _selectorCatalogo?.Dispose();
            _selectorCatalogo               = null;
            CatalogoSelectorHost.Content     = null;
            CatalogoSelectorHost.Visibility  = Visibility.Collapsed;
            ContenidoPrincipal.Visibility    = Visibility.Visible;
            AplicarMarcoSelector(false);
        }

        /// <summary>
        /// Marco grande mientras se ve la tabla del buscador, propio cuando se ve el
        /// formulario. 720×780 es el tamaño con el que el buscador venía funcionando
        /// dentro del megamodal. Los MaxWidth/MaxHeight del XAML siguen acotándolo
        /// contra la ventana.
        /// </summary>
        private void AplicarMarcoSelector(bool abierto)
        {
            Width  = abierto ? 720 : _anchoPropio;
            Height = abierto ? 780 : _altoPropio;
        }

        // ── Validación ──────────────────────────────────────────────────────

        private void Campo_Changed(object sender, TextChangedEventArgs e) => Validar();

        private void Validar()
        {
            bool pesoOk   = double.TryParse(TxtPeso.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var peso) && peso > 0;
            bool bultosOk = int.TryParse(TxtBultos.Text, out var bultos) && bultos > 0;
            bool ok = !_guardando
                      && _proveedorElegido is not null
                      && _idProductoElegido.HasValue
                      && pesoOk && bultosOk;

            if (BtnGuardar     != null) BtnGuardar.IsEnabled     = ok;
            if (BtnGuardarYOtro != null) BtnGuardarYOtro.IsEnabled = ok;
        }

        // ── Guardado ────────────────────────────────────────────────────────

        private void Guardar_Click(object sender, RoutedEventArgs e)   => _ = GuardarAsync(cerrar: true);
        private void GuardarYOtro_Click(object sender, RoutedEventArgs e) => _ = GuardarAsync(cerrar: false);

        private async Task GuardarAsync(bool cerrar)
        {
            if (_guardando || Guardar is null) return;
            if (_proveedorElegido is null || !_idProductoElegido.HasValue) return;
            if (!double.TryParse(TxtPeso.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var peso)) return;
            if (!int.TryParse(TxtBultos.Text, out var bultos)) return;

            AplicarEstadoGuardando(true);
            try
            {
                bool ok = await Guardar(new ResultadoProductoCamion(
                    _proveedorElegido.Id, _proveedorElegido.Nombre, _idProductoElegido.Value,
                    peso, bultos, TxtObs.Text?.Trim() ?? ""));

                if (!ok) return;   // el VM ya avisó por Toast; el modal queda abierto

                if (cerrar) { Cerrado?.Invoke(); return; }
                PrepararSiguienteProducto();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "ProductoCamionModal: falló el guardado del producto");
            }
            finally
            {
                AplicarEstadoGuardando(false);
            }
        }

        /// <summary>
        /// Bloquea el modal mientras el producto viaja al servidor: da la señal visual
        /// y, sobre todo, impide que un segundo clic lo registre dos veces.
        /// </summary>
        private void AplicarEstadoGuardando(bool guardando)
        {
            _guardando = guardando;

            BtnCancelar.IsEnabled             = !guardando;
            BtnSeleccionarProveedor.IsEnabled = !guardando;
            BtnSeleccionarProducto.IsEnabled  = !guardando;
            TxtPeso.IsEnabled                 = !guardando;
            TxtBultos.IsEnabled               = !guardando;
            TxtObs.IsEnabled                  = !guardando;

            // BtnGuardar/BtnGuardarYOtro salen siempre de Validar() (que ya contempla
            // _guardando), para que no haya dos fuentes de verdad sobre si se pueden tocar.
            Validar();
        }

        /// <summary>
        /// Deja el modal listo para el producto siguiente en vez de cerrarlo: eso es lo
        /// que promete "Guardar y agregar otro". El proveedor se mantiene — lo normal es
        /// cargar varios del mismo.
        /// </summary>
        private void PrepararSiguienteProducto()
        {
            _agregados++;
            _idProductoElegido = null;
            TxtSeleccion.Text  = "Seleccionar producto…";
            TxtPeso.Text       = "";
            TxtBultos.Text     = "";
            TxtObs.Text        = "";

            TxtPie.Text = _agregados == 1
                ? "1 producto agregado"
                : $"{_agregados} productos agregados";

            Validar();
        }

        // ── Otras acciones ──────────────────────────────────────────────────

        private void Quitar_Click(object sender, RoutedEventArgs e)
        {
            if (_producto is not null) QuitarSolicitado?.Invoke(_producto);
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            if (_selectorCatalogo is not null) { CerrarSelectorCatalogo(); return; }
            Cerrado?.Invoke();
        }
    }
}
