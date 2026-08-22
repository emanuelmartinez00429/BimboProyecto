using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CapaAplicacion.Common.Catalogos;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>Lo que el modal devuelve al confirmarse.</summary>
    public record ResultadoCamion(string Placa, string Proveedor, int? IdProveedor, string Observaciones);

    /// <summary>
    /// Alta o edición de UN camión — solo sus datos (proveedor, placa,
    /// observaciones). Los productos ya no viven acá: se agregan/editan desde
    /// la pantalla principal con <see cref="ProductoCamionModal"/>, uno a la
    /// vez. Revive el split que existía antes de que ambos pasos se
    /// unificaran en <c>ProcesoDescargaModal</c> (ver commit bf1117f).
    /// </summary>
    public partial class CamionModal : UserControl
    {
        private readonly CamionPesaje? _camion;   // null = alta
        private readonly ICatalogoRepository _catalogos;

        /// <summary>
        /// Recepciones abiertas al momento de abrir el modal. Solo se usan para avisar
        /// que la placa que se está escribiendo ya está abierta con otro proveedor.
        /// </summary>
        private readonly IReadOnlyList<CamionPesaje> _camionesAbiertos;

        private SelectorCatalogoModal? _selectorCatalogo;
        private ProveedorItem? _proveedorSeleccionado;
        private bool _cargando = true;

        /// <summary>
        /// Tamaño propio del modal, leído del XAML al construirlo. El buscador de
        /// catálogo necesita bastante más marco del que necesita este formulario, así
        /// que mientras está abierto el modal crece y al cerrarlo vuelve acá. Se guarda
        /// en vez de hardcodearse para que cambiar el tamaño en el XAML alcance.
        /// </summary>
        private readonly double _anchoPropio;
        private readonly double _altoPropio;

        public event Action? Cerrado;
        public event Action<ResultadoCamion>? Confirmado;

        public CamionModal(CamionPesaje? camion, IReadOnlyList<CamionPesaje> camionesAbiertos)
        {
            InitializeComponent();

            _camion           = camion;
            _camionesAbiertos = camionesAbiertos;
            _catalogos        = App.Services.GetRequiredService<ICatalogoRepository>();

            _anchoPropio = Width;
            _altoPropio  = Height;

            if (_camion is null)
            {
                TxtEyebrow.Text     = "NUEVO CAMIÓN";
                TxtTitulo.Text      = "Registrar camión";
                TxtBtnGuardar.Text  = "Registrar";
                TxtPie.Text         = "";
            }
            else
            {
                TxtEyebrow.Text     = $"EDICIÓN · CAMIÓN {_camion.Placa}";
                TxtTitulo.Text      = "Editar camión";
                TxtBtnGuardar.Text  = "Guardar cambios";

                TxtPlaca.Text       = _camion.Placa;
                TxtObs.Text         = _camion.Observaciones;
                TxtProveedor.Text   = _camion.Proveedor;
                _proveedorSeleccionado = _camion.IdProveedor.HasValue
                    ? new ProveedorItem(_camion.IdProveedor.Value, _camion.Proveedor)
                    : null;
            }

            _cargando = false;
            Validar();
        }

        // ── Selector de catálogo (Proveedor) ────────────────────────────────
        // El selector reemplaza todo el contenido del modal mientras está abierto
        // (es "chromeless": hereda este marco, no tiene fondo ni tamaño propio), y
        // el marco crece para que la tabla del buscador entre completa.

        private void BuscarProveedor_Click(object sender, RoutedEventArgs e)
        {
            var selector = new SelectorCatalogoModal(Catalogos.Proveedores(_catalogos));
            selector.Cerrado += CerrarSelectorCatalogo;
            // NO se cierra el selector acá: lo cierra él mismo (evento Cerrado) apenas
            // termina de emitir. Cerrar desde este handler lo dispone a mitad de su
            // propio bucle de emisión y la excepción que sale de ahí se lleva la app
            // puesta — no hay DispatcherUnhandledException que la ataje.
            selector.Seleccionado += item =>
            {
                _proveedorSeleccionado = new ProveedorItem(item.Id ?? 0, item.Nombre);
                TxtProveedor.Text      = item.Nombre;
                Validar();
                // El aviso depende del proveedor elegido: la misma placa con el MISMO
                // proveedor sí sería un duplicado, con otro es una recepción aparte.
                RevisarPlacaCompartida();
            };

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

        /// <summary>Doble clic en el campo de solo lectura abre el selector.</summary>
        private void TxtCatalogo_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement campo && campo.Tag is Button lupa)
            {
                e.Handled = true;
                lupa.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

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

        // ── Reacciones a cambios / validación ───────────────────────────────

        private void Placa_Changed(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;

            int caret = TxtPlaca.CaretIndex;
            string up = TxtPlaca.Text.ToUpperInvariant();
            if (TxtPlaca.Text != up)
            {
                TxtPlaca.Text = up;
                TxtPlaca.CaretIndex = caret;
            }
            Validar();
            RevisarPlacaCompartida();
        }

        /// <summary>
        /// Avisa —sin bloquear— que la placa ya está abierta con OTRO proveedor. No es
        /// un error: un camión que trae carga de dos proveedores se registra como dos
        /// recepciones, una por proveedor, y así cada manifiesto se firma y se reporta
        /// por separado. El aviso está para que no parezca un duplicado por error.
        /// </summary>
        private void RevisarPlacaCompartida()
        {
            string placa = TxtPlaca.Text.Trim();
            if (placa.Length == 0) { PanelAviso.Visibility = Visibility.Collapsed; return; }

            var otra = _camionesAbiertos.FirstOrDefault(c =>
                c.Id != (_camion?.Id ?? 0)
                && string.Equals(c.Placa?.Trim(), placa, StringComparison.OrdinalIgnoreCase)
                && c.IdProveedor != _proveedorSeleccionado?.Id);

            if (otra is null) { PanelAviso.Visibility = Visibility.Collapsed; return; }

            TxtAviso.Text = $"La placa {placa} ya está abierta con {otra.Proveedor}. " +
                            "Se registrará una recepción aparte para este proveedor.";
            PanelAviso.Visibility = Visibility.Visible;
        }

        private void Obs_Changed(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
        }

        private void Validar()
        {
            bool ok = _proveedorSeleccionado is not null && !string.IsNullOrWhiteSpace(TxtPlaca.Text);
            if (BtnGuardar != null) BtnGuardar.IsEnabled = ok;
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            var prov = _proveedorSeleccionado;
            if (prov is null || string.IsNullOrWhiteSpace(TxtPlaca.Text)) return;

            Confirmado?.Invoke(new ResultadoCamion(
                TxtPlaca.Text.Trim(), prov.Nombre, prov.Id, TxtObs.Text?.Trim() ?? ""));
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            if (_selectorCatalogo is not null) { CerrarSelectorCatalogo(); return; }
            Cerrado?.Invoke();
        }
    }
}
