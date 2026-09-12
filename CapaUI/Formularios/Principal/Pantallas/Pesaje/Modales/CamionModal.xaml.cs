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

    public record ResultadoEdicionPlaca(string Placa, string Observaciones);

    /// <summary>
    /// Alta o edición de UN camión — solo sus datos (proveedor, placa,
    /// observaciones). Soporta modo solo placa para modificar el vehículo físico.
    /// </summary>
    public partial class CamionModal : UserControl, IDisposable
    {
        private readonly CamionPesaje? _camion;   // null = alta
        private readonly ICatalogoRepository _catalogos = null!;
        private readonly bool _soloPlaca;

        /// <summary>
        /// Recepciones abiertas al momento de abrir el modal. Solo se usan para avisar
        /// que la placa que se está escribiendo ya está abierta con otro proveedor.
        /// </summary>
        private readonly IReadOnlyList<CamionPesaje> _camionesAbiertos = null!;

        private SelectorCatalogoModal? _selectorCatalogo;
        private ProveedorItem? _proveedorSeleccionado;
        private bool _cargando = true;

        private readonly double _anchoPropio;
        private readonly double _altoPropio;

        public event Action? Cerrado;
        public event Action<ResultadoCamion>? Confirmado;
        public event Action<ResultadoEdicionPlaca>? ConfirmadoPlaca;

        public CamionModal()
        {
            _catalogos        = null!;
            _camionesAbiertos = null!;
            InitializeComponent();
        }

        public CamionModal(CamionPesaje? camion, IReadOnlyList<CamionPesaje> camionesAbiertos, bool soloPlaca = false)
        {
            InitializeComponent();

            _camion           = camion;
            _camionesAbiertos = camionesAbiertos;
            _soloPlaca        = soloPlaca;
            _catalogos        = App.Services.GetRequiredService<ICatalogoRepository>();

            _anchoPropio = Width;
            _altoPropio  = Height;

            if (_soloPlaca)
            {
                PanelProveedor.Visibility = Visibility.Collapsed;
                TxtEyebrow.Text     = $"EDICIÓN · VEHÍCULO {_camion?.Placa}";
                TxtTitulo.Text      = "Editar vehículo";
                TxtBtnGuardar.Text  = "Guardar cambios";
                TxtPie.Text         = "Modificar placa y observaciones del camión";

                TxtPlaca.Text       = _camion?.Placa ?? "";
                TxtObs.Text         = _camion?.Observaciones ?? "";
            }
            else if (_camion is null)
            {
                PanelProveedor.Visibility = Visibility.Visible;
                TxtEyebrow.Text     = "NUEVO CAMIÓN";
                TxtTitulo.Text      = "Registrar camión";
                TxtBtnGuardar.Text  = "Registrar";
                TxtPie.Text         = "";
            }
            else
            {
                PanelProveedor.Visibility = Visibility.Visible;
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

        private void BuscarProveedor_Click(object sender, RoutedEventArgs e)
        {
            string placaActual = TxtPlaca.Text.Trim();
            var config = Catalogos.Proveedores(_catalogos, estaYaElegido: id =>
                id.HasValue && !string.IsNullOrEmpty(placaActual) && _camionesAbiertos.Any(c =>
                    string.Equals(c.Placa?.Trim(), placaActual, StringComparison.OrdinalIgnoreCase) &&
                    c.IdProveedor == id.Value));

            var selector = new SelectorCatalogoModal(config);
            selector.Cerrado += CerrarSelectorCatalogo;
            selector.Seleccionado += item =>
            {
                _proveedorSeleccionado = new ProveedorItem(item.Id ?? 0, item.Nombre);
                TxtProveedor.Text      = item.Nombre;
                Validar();
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

        private void AplicarMarcoSelector(bool abierto)
        {
            Width  = abierto ? 720 : _anchoPropio;
            Height = abierto ? 780 : _altoPropio;
        }

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

        private void RevisarPlacaCompartida()
        {
            if (_soloPlaca) { PanelAviso.Visibility = Visibility.Collapsed; return; }

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
            if (_soloPlaca)
            {
                bool okPlaca = !string.IsNullOrWhiteSpace(TxtPlaca.Text);
                if (BtnGuardar != null) BtnGuardar.IsEnabled = okPlaca;
                return;
            }

            bool ok = _proveedorSeleccionado is not null && !string.IsNullOrWhiteSpace(TxtPlaca.Text);
            if (BtnGuardar != null) BtnGuardar.IsEnabled = ok;
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            if (_soloPlaca)
            {
                if (string.IsNullOrWhiteSpace(TxtPlaca.Text)) return;
                ConfirmadoPlaca?.Invoke(new ResultadoEdicionPlaca(
                    TxtPlaca.Text.Trim(), TxtObs.Text?.Trim() ?? ""));
                return;
            }

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

        public void Dispose()
        {
            CerrarSelectorCatalogo();
        }
    }
}
