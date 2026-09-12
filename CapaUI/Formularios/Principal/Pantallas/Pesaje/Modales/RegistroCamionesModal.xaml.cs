using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CapaAplicacion.Common.Catalogos;
using CapaDominio.Reglas;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaUI.Core.Validacion;
using CapaUI.Formularios.Principal.Pantallas.Pesaje.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Pesaje.Modales
{
    /// <summary>Un camión listo para darse de alta, tal como quedó en la tabla.</summary>
    public record CamionRegistrado(string Placa, string Proveedor, int IdProveedor, string Descripcion);

    /// <summary>Un camión existente que fue modificado en la tabla.</summary>
    public record CamionEditado(CamionPesaje Camion, string Placa, string Proveedor, int IdProveedor, string Descripcion);

    /// <summary>Conjunto completo de altas, modificaciones y bajas a persistir.</summary>
    public record CambiosProcesoCamiones(
        IReadOnlyList<CamionRegistrado> Altas,
        IReadOnlyList<CamionEditado> Cambios,
        IReadOnlyList<CamionPesaje> Bajas);

    /// <summary>
    /// Gestión integral de los camiones del andén: muestra los camiones ya abiertos para
    /// consultarlos o editarlos, y permite incorporar nuevos camiones al proceso de descarga.
    /// </summary>
    public partial class RegistroCamionesModal : UserControl, IDisposable
    {
        private readonly ICatalogoRepository _catalogos = null!;
        private System.Windows.Media.Animation.Storyboard? _spinnerGuardar;

        private IReadOnlyList<CamionPesaje> _camionesAbiertos = null!;
        private IReadOnlyCollection<string> _placasAbiertas = null!;

        private readonly ObservableCollection<FilaCamion> _filas = new();
        private readonly List<CamionPesaje> _bajas = new();
        private readonly string? _placaFija;

        private SelectorCatalogoModal? _selectorCatalogo;
        private FilaCamion? _filaDelSelector;
        private FilaCamion? _filaPendienteEnfoque;

        private readonly double _anchoPropio;
        private readonly double _altoPropio;

        public event Action? Cerrado;
        public event Func<CambiosProcesoCamiones, Task<bool>>? Confirmado;

        /// <summary>
        /// Constructor sin parámetros solo para el diseñador de Visual Studio.
        /// </summary>
        public RegistroCamionesModal()
        {
            _catalogos        = null!;
            _camionesAbiertos = null!;
            _placasAbiertas   = null!;
            InitializeComponent();
        }

        public RegistroCamionesModal(
            IReadOnlyList<CamionPesaje> camionesAbiertos,
            string? placaFija = null,
            CamionPesaje? enfocar = null)
        {
            InitializeComponent();

            _camionesAbiertos = camionesAbiertos;
            _placaFija        = string.IsNullOrWhiteSpace(placaFija) ? null : placaFija.Trim().ToUpperInvariant();
            _placasAbiertas   = PlacasDe(camionesAbiertos);

            _catalogos   = App.Services.GetRequiredService<ICatalogoRepository>();
            _anchoPropio = Width;
            _altoPropio  = Height;

            for (int i = 1; i <= PesajeViewModel.MaxCamiones; i++)
            {
                var f = new FilaCamion(i) { EsAlterna = i % 2 == 0 };
                if (!string.IsNullOrEmpty(_placaFija)) f.Placa = _placaFija;
                _filas.Add(f);
            }

            // Si hay placa fija, cargamos solo los camiones que pertenezcan a esa placa
            var camionesParaCargar = !string.IsNullOrEmpty(_placaFija)
                ? camionesAbiertos.Where(c => Normalizar(c) == _placaFija).ToList()
                : camionesAbiertos;

            // Cargar camiones existentes abiertos
            for (int i = 0; i < camionesParaCargar.Count && i < _filas.Count; i++)
            {
                _filas[i].EstablecerOriginal(camionesParaCargar[i]);
                _filas[i].Activa = true;
            }

            // Si no había ningún camión cargado, activar la primera fila para empezar a cargar
            if (camionesParaCargar.Count == 0)
            {
                _filas[0].Activa = true;
            }

            FilasHost.ItemsSource = _filas;

            if (!string.IsNullOrEmpty(_placaFija))
            {
                TxtEyebrow.Text = $"GESTIÓN DE PROVEEDORES · VEHÍCULO {_placaFija}";
                TxtTituloPrincipal.Text = $"Proveedores · Placa {_placaFija}";
                TxtInstruccion.Text =
                    $"Gestioná los proveedores asociados a la placa {_placaFija} (hasta {PesajeViewModel.MaxCamiones} proveedores). " +
                    "No se puede agregar el mismo proveedor dos veces a la misma placa.";
            }
            else
            {
                TxtInstruccion.Text =
                    $"Gestioná los camiones del andén o registrá nuevas descargas (hasta " +
                    $"{PesajeViewModel.MaxCamiones} camiones en total). La placa y el proveedor son obligatorios.";
            }

            ActualizarContadores();

            // Auto-enfoque y tabulación inmediata en la fila deseada
            FilaCamion? filaAEnfocar = null;
            if (enfocar is not null)
            {
                filaAEnfocar = _filas.FirstOrDefault(f => f.CamionOriginal?.Id == enfocar.Id);
            }

            if (filaAEnfocar is null)
            {
                filaAEnfocar = _filas.FirstOrDefault(f => !f.EsExistente && f.Activa)
                            ?? _filas.FirstOrDefault(f => f.Activa);
            }

            EnfocarFila(filaAEnfocar);
        }

        private int Ocupadas => _filas.Count(f => f.Activa);

        private static string Normalizar(CamionPesaje c) => (c.Placa ?? "").Trim().ToUpperInvariant();

        private static IReadOnlyCollection<string> PlacasDe(IEnumerable<CamionPesaje> camiones) => camiones
            .Select(Normalizar)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // ── Auto-enfoque y tabulación ───────────────────────────────────────────

        public void EnfocarFila(FilaCamion? fila)
        {
            if (fila is null) return;
            _filaPendienteEnfoque = fila;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(IntentarEnfocarPendiente));
        }

        private void IntentarEnfocarPendiente()
        {
            if (_filaPendienteEnfoque?.CajaPlaca is { IsVisible: true } caja)
            {
                caja.Focus();
                caja.SelectAll();
                _filaPendienteEnfoque = null;
            }
        }

        private void CajaPlaca_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            if (sender is not TextBox caja || caja.DataContext is not FilaCamion fila) return;

            e.Handled = true;
            if (fila.IdProveedor.HasValue)
            {
                fila.CajaDescripcion?.Focus();
                fila.CajaDescripcion?.SelectAll();
            }
            else
            {
                BuscarProveedor_Click(caja, new RoutedEventArgs());
            }
        }

        private void CajaDescripcion_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            if (sender is not TextBox caja || caja.DataContext is not FilaCamion fila) return;

            e.Handled = true;
            int idx = _filas.IndexOf(fila);
            if (idx >= 0 && idx < _filas.Count - 1)
            {
                var siguiente = _filas[idx + 1];
                if (!siguiente.Activa)
                {
                    siguiente.Activa = true;
                    ActualizarContadores();
                }
                EnfocarFila(siguiente);
            }
            else
            {
                BtnGuardar.Focus();
            }
        }

        // ── Alta y baja de filas ────────────────────────────────────────────────

        private void AgregarFila_Click(object sender, RoutedEventArgs e)
        {
            var libre = _filas.FirstOrDefault(f => !f.Activa);
            if (libre is null) return;

            libre.Activa = true;
            if (!string.IsNullOrEmpty(_placaFija))
            {
                libre.Placa = _placaFija;
                if (libre.CajaPlaca != null) libre.CajaPlaca.Text = _placaFija;
            }
            ActualizarContadores();

            EnfocarFila(libre);
        }

        private void QuitarFila_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.DataContext is not FilaCamion fila) return;
            if (!fila.PuedeQuitar) return;

            if (fila.CamionOriginal is { } c)
            {
                _bajas.Add(c);
            }

            if (Ocupadas <= 1) fila.Limpiar();
            else               CompactarDesde(fila);

            LimpiarMarcasDeError();
            RevisarPlacas();
            ActualizarContadores();
        }

        /// <summary>
        /// Saca una fila compactando hacia arriba: los valores de las filas de abajo suben
        /// un lugar y se libera la última ocupada. Así no quedan huecos en el medio de la
        /// tabla ("camión 1, camión 3") y los números siguen leyéndose como el orden en que
        /// llegaron los camiones.
        /// </summary>
        private void CompactarDesde(FilaCamion fila)
        {
            int desde = _filas.IndexOf(fila);
            for (int i = desde; i < _filas.Count - 1; i++)
                _filas[i].CopiarDe(_filas[i + 1]);

            var ultima = _filas[^1];
            ultima.Limpiar();
            ultima.Activa = false;
        }

        private void LimpiarMarcasDeError()
        {
            foreach (var f in _filas) f.Validador?.Limpiar();
        }

        public void AplicarGuardadoParcial(int guardadas, IReadOnlyList<CamionPesaje> camionesAbiertos)
        {
            _camionesAbiertos = camionesAbiertos;
            _placasAbiertas   = PlacasDe(camionesAbiertos);

            var primerNueva = _filas.FirstOrDefault(f => f.Activa && !f.EsExistente);
            for (int i = 0; i < guardadas && primerNueva != null; i++)
            {
                CompactarDesde(primerNueva);
                primerNueva = _filas.FirstOrDefault(f => f.Activa && !f.EsExistente);
            }

            if (Ocupadas == 0) _filas[0].Activa = true;

            LimpiarMarcasDeError();
            RevisarPlacas();
            ActualizarContadores();
        }

        // ── Selector de catálogo (Proveedor) ────────────────────────────────────

        private void BuscarProveedor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.DataContext is not FilaCamion fila) return;

            _filaDelSelector = fila;

            string placaDeFila = (fila.PlacaNormalizada.Length > 0 ? fila.PlacaNormalizada : _placaFija) ?? "";
            var idsYaElegidos = new HashSet<int>();

            foreach (var c in _camionesAbiertos)
            {
                if (c.IdProveedor.HasValue &&
                    Normalizar(c) == placaDeFila &&
                    !_bajas.Any(b => b.Id == c.Id))
                {
                    idsYaElegidos.Add(c.IdProveedor.Value);
                }
            }

            foreach (var f in _filas)
            {
                if (f.Activa && !ReferenceEquals(f, fila) && f.IdProveedor.HasValue && f.IdProveedor.Value > 0)
                {
                    string fPlaca = (f.PlacaNormalizada.Length > 0 ? f.PlacaNormalizada : _placaFija) ?? "";
                    if (fPlaca == placaDeFila)
                    {
                        idsYaElegidos.Add(f.IdProveedor.Value);
                    }
                }
            }

            var config = Catalogos.Proveedores(_catalogos, estaYaElegido: id => id.HasValue && idsYaElegidos.Contains(id.Value));
            var selector = new SelectorCatalogoModal(config);
            selector.Cerrado += CerrarSelectorCatalogo;
            selector.Seleccionado += item =>
            {
                var destino = _filaDelSelector;
                if (destino is null) return;

                destino.IdProveedor = item.Id ?? 0;
                destino.Proveedor   = item.Nombre;
                if (destino.CajaProveedor is not null) destino.CajaProveedor.Text = item.Nombre;

                RevisarPlacas();
                ActualizarContadores();
            };

            _selectorCatalogo               = selector;
            CatalogoSelectorHost.Content    = selector;
            CatalogoSelectorHost.Visibility = Visibility.Visible;
            ContenidoPrincipal.Visibility   = Visibility.Collapsed;
            AplicarMarcoSelector(true);
        }

        private void CerrarSelectorCatalogo()
        {
            var fila = _filaDelSelector;
            _selectorCatalogo?.Dispose();
            _selectorCatalogo               = null;
            _filaDelSelector                = null;
            CatalogoSelectorHost.Content    = null;
            CatalogoSelectorHost.Visibility = Visibility.Collapsed;
            ContenidoPrincipal.Visibility   = Visibility.Visible;
            AplicarMarcoSelector(false);

            if (fila?.CajaDescripcion is not null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    fila.CajaDescripcion.Focus();
                    fila.CajaDescripcion.SelectAll();
                }), DispatcherPriority.Input);
            }
        }

        private void AplicarMarcoSelector(bool abierto)
        {
            Width  = abierto ? 880 : _anchoPropio;
            Height = abierto ? 780 : _altoPropio;
        }

        private void CajaProveedor_DobleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            BuscarProveedor_Click(sender, new RoutedEventArgs());
        }

        private void CajaProveedor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            e.Handled = true;
            BuscarProveedor_Click(sender, new RoutedEventArgs());
        }

        // ── Enganche de los campos con su fila ──────────────────────────────────

        private void CajaPlaca_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaPlaca = caja);

        private void CajaProveedor_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaProveedor = caja);

        private void CajaDescripcion_Loaded(object sender, RoutedEventArgs e) =>
            Enganchar(sender, (fila, caja) => fila.CajaDescripcion = caja);

        private void Enganchar(object sender, Action<FilaCamion, TextBox> asignar)
        {
            if (sender is not TextBox caja || caja.DataContext is not FilaCamion fila) return;
            asignar(fila, caja);
            fila.SembrarSiHaceFalta(caja);
            fila.ArmarValidadorSiEstaCompleto();

            if (ReferenceEquals(fila, _filaPendienteEnfoque) && ReferenceEquals(caja, fila.CajaPlaca))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(IntentarEnfocarPendiente));
            }
        }

        // ── Reacciones a cambios ────────────────────────────────────────────────

        private void Placa_Changed(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox caja) return;

            int caret = caja.CaretIndex;
            string arriba = caja.Text.ToUpperInvariant();
            if (caja.Text != arriba)
            {
                caja.Text = arriba;
                caja.CaretIndex = caret;
            }

            if (caja.DataContext is FilaCamion fila) fila.Placa = arriba;
            RevisarPlacas();
            ActualizarContadores();
        }

        private void Descripcion_Changed(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox caja && caja.DataContext is FilaCamion fila)
            {
                fila.Descripcion = caja.Text;
                ActualizarContadores();
            }
        }

        private void Descripcion_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox caja) return;
            caja.CaretIndex = 0;
            caja.ScrollToHome();
        }

        private void RevisarPlacas()
        {
            var avisos = new List<string>();

            foreach (var fila in _filas.Where(f => f.Activa && f.PlacaNormalizada.Length > 0))
            {
                var otra = _camionesAbiertos.FirstOrDefault(c =>
                    c.Id != fila.CamionOriginal?.Id &&
                    !_bajas.Any(b => b.Id == c.Id) &&
                    Normalizar(c) == fila.PlacaNormalizada &&
                    c.IdProveedor != fila.IdProveedor);

                if (otra is not null)
                    avisos.Add($"La placa {fila.PlacaNormalizada} ya está abierta con {otra.Proveedor}.");
            }

            if (avisos.Count == 0) { PanelAviso.Visibility = Visibility.Collapsed; return; }

            TxtAviso.Text = string.Join(" ", avisos.Distinct()) +
                            " Se registrará una recepción aparte para el proveedor que elijas.";
            PanelAviso.Visibility = Visibility.Visible;
        }

        private void ActualizarContadores()
        {
            int ocupadas = Ocupadas;
            int nuevos = _filas.Count(f => f.Activa && !f.EsExistente);
            int modificados = _filas.Count(f => f.Activa && f.Modificada);

            TxtContador.Text = $"{ocupadas} de {PesajeViewModel.MaxCamiones} camiones";

            var partes = new List<string> { $"{ocupadas} camión(es) en el andén" };
            if (nuevos > 0)       partes.Add(nuevos == 1 ? "1 nuevo" : $"{nuevos} nuevos");
            if (modificados > 0)  partes.Add(modificados == 1 ? "1 modificado" : $"{modificados} modificados");
            if (_bajas.Count > 0) partes.Add(_bajas.Count == 1 ? "1 por quitar" : $"{_bajas.Count} por quitar");
            TxtPie.Text = string.Join(" · ", partes);

            BtnAgregarFila.IsEnabled = ocupadas < PesajeViewModel.MaxCamiones;

            int cupo = PesajeViewModel.MaxCamiones - _filas.Where(f => f.Activa && f.EsExistente).Select(f => f.PlacaNormalizada).Distinct(StringComparer.Ordinal).Count();
            int placasNuevas = PlacasNuevas().Count;

            if (placasNuevas > cupo)
                MostrarError($"Ya hay {_camionesAbiertos.Count - _bajas.Count} camión(es) en el andén y solo quedan " +
                             $"{Math.Max(0, cupo)} lugar(es) libres; estás registrando {placasNuevas} placas distintas.");
            else
                PanelError.Visibility = Visibility.Collapsed;
        }

        private List<string> PlacasNuevas() => _filas
            .Where(f => f.Activa && !f.EsExistente && f.PlacaNormalizada.Length > 0)
            .Select(f => f.PlacaNormalizada)
            .Distinct(StringComparer.Ordinal)
            .Where(p => !_placasAbiertas.Contains(p))
            .ToList();

        // ── Guardado ────────────────────────────────────────────────────────────

        public void MostrarGuardando(bool activo)
        {
            BtnGuardar.IsEnabled  = !activo;
            BtnCancelar.IsEnabled = !activo;
            if (activo) BtnAgregarFila.IsEnabled = false;
            else        ActualizarContadores();

            TxtBtnGuardar.Text        = activo ? "Guardando..." : "Guardar cambios";
            IconoGuardar.Visibility   = activo ? Visibility.Collapsed : Visibility.Visible;
            SpinnerGuardar.Visibility = activo ? Visibility.Visible : Visibility.Collapsed;
            if (activo) IniciarSpinnerGuardar(); else DetenerSpinnerGuardar();
        }

        private void IniciarSpinnerGuardar()
        {
            if (_spinnerGuardar != null) return;
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
            { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
            System.Windows.Media.Animation.Storyboard.SetTarget(anim, SpinnerGuardar);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(anim,
                new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            _spinnerGuardar = new System.Windows.Media.Animation.Storyboard();
            _spinnerGuardar.Children.Add(anim);
            _spinnerGuardar.Begin();
        }

        private void DetenerSpinnerGuardar()
        {
            if (_spinnerGuardar is null) return;
            _spinnerGuardar.Stop();
            _spinnerGuardar.Remove();
            _spinnerGuardar.Children.Clear();
            _spinnerGuardar = null;
        }

        private async void Guardar_Click(object sender, RoutedEventArgs e)
        {
            var activas = _filas.Where(f => f.Activa).ToList();
            if (activas.Count == 0 && _bajas.Count == 0) { MostrarError("Registrá al menos un camión."); return; }

            foreach (var fila in activas)
                if (fila.Validador is not null && !fila.Validador.Validar()) return;

            if (!SinDuplicados(activas)) return;

            int cupo = PesajeViewModel.MaxCamiones - _filas.Where(f => f.Activa && f.EsExistente).Select(f => f.PlacaNormalizada).Distinct(StringComparer.Ordinal).Count();
            if (PlacasNuevas().Count > cupo)
            {
                ActualizarContadores();
                return;
            }

            PanelError.Visibility = Visibility.Collapsed;

            var altas = activas
                .Where(f => !f.EsExistente)
                .Select(f => new CamionRegistrado(f.PlacaNormalizada, f.Proveedor, f.IdProveedor!.Value, f.Descripcion.Trim()))
                .ToList();

            var cambios = activas
                .Where(f => f.EsExistente && f.Modificada)
                .Select(f => new CamionEditado(f.CamionOriginal!, f.PlacaNormalizada, f.Proveedor, f.IdProveedor!.Value, f.Descripcion.Trim()))
                .ToList();

            if (altas.Count == 0 && cambios.Count == 0 && _bajas.Count == 0)
            {
                Cerrado?.Invoke();
                return;
            }

            if (Confirmado is null) return;

            MostrarGuardando(true);
            try
            {
                bool ok = await Confirmado(new CambiosProcesoCamiones(altas, cambios, _bajas.ToList()));
                if (ok) Cerrado?.Invoke();
            }
            finally
            {
                MostrarGuardando(false);
            }
        }

        private bool SinDuplicados(IReadOnlyList<FilaCamion> activas)
        {
            var vistas = new HashSet<(string, int)>();

            foreach (var fila in activas)
            {
                var clave = (fila.PlacaNormalizada, fila.IdProveedor!.Value);

                if (!vistas.Add(clave))
                {
                    MostrarError($"El camión {fila.Numero} repite la placa {fila.PlacaNormalizada} " +
                                 $"con el mismo proveedor ({fila.Proveedor}). Quitá la fila repetida " +
                                 "o cambiale el proveedor.");
                    fila.CajaPlaca?.Focus();
                    return false;
                }

                if (_camionesAbiertos.Any(c => c.Id != fila.CamionOriginal?.Id &&
                                               !_bajas.Any(b => b.Id == c.Id) &&
                                               Normalizar(c) == fila.PlacaNormalizada &&
                                               c.IdProveedor == fila.IdProveedor))
                {
                    MostrarError($"La placa {fila.PlacaNormalizada} ya tiene una recepción abierta con " +
                                 $"{fila.Proveedor}. Agregale los productos a esa recepción en vez de " +
                                 "registrarla de nuevo.");
                    fila.CajaPlaca?.Focus();
                    return false;
                }
            }

            return true;
        }

        private void MostrarError(string mensaje)
        {
            TxtError.Text = mensaje;
            PanelError.Visibility = Visibility.Visible;
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

        // ── Fila de la tabla ────────────────────────────────────────────────────

        public partial class FilaCamion : ObservableObject
        {
            public FilaCamion(int numero) => Numero = numero;

            public int Numero { get; }

            public bool EsAlterna { get; init; }

            [ObservableProperty] private bool _activa;

            public CamionPesaje? CamionOriginal { get; set; }

            public bool EsExistente => CamionOriginal != null;

            public bool PuedeQuitar => CamionOriginal?.PuedeQuitar ?? true;

            public string MotivoQuitar => EsExistente && !PuedeQuitar
                ? "Este camión ya tiene productos registrados y no se puede quitar del andén"
                : "Quitar este camión";

            public string  Placa       { get; set; } = "";
            public string  Proveedor   { get; set; } = "";
            public int?    IdProveedor { get; set; }
            public string  Descripcion { get; set; } = "";

            private string _placaOriginal       = "";
            private int?   _idProveedorOriginal;
            private string _descripcionOriginal = "";

            public string PlacaNormalizada => Placa.Trim().ToUpperInvariant();

            public TextBox? CajaPlaca       { get; set; }
            public TextBox? CajaProveedor   { get; set; }
            public TextBox? CajaDescripcion { get; set; }

            public ValidadorFormulario? Validador { get; private set; }

            public void EstablecerOriginal(CamionPesaje c)
            {
                CamionOriginal       = c;
                Placa                = c.Placa ?? "";
                Proveedor            = c.Proveedor ?? "";
                IdProveedor          = c.IdProveedor;
                Descripcion          = c.Observaciones ?? "";

                _placaOriginal       = (c.Placa ?? "").Trim().ToUpperInvariant();
                _idProveedorOriginal = c.IdProveedor;
                _descripcionOriginal = (c.Observaciones ?? "").Trim();
            }

            public bool Modificada => EsExistente && (
                !string.Equals(PlacaNormalizada, _placaOriginal, StringComparison.Ordinal) ||
                IdProveedor != _idProveedorOriginal ||
                !string.Equals(Descripcion.Trim(), _descripcionOriginal, StringComparison.Ordinal)
            );

            public void SembrarSiHaceFalta(TextBox caja)
            {
                if (ReferenceEquals(caja, CajaPlaca)       && caja.Text.Length == 0 && !string.IsNullOrEmpty(Placa)) caja.Text = Placa;
                if (ReferenceEquals(caja, CajaProveedor)   && caja.Text.Length == 0 && !string.IsNullOrEmpty(Proveedor)) caja.Text = Proveedor;
                if (ReferenceEquals(caja, CajaDescripcion) && caja.Text.Length == 0 && !string.IsNullOrEmpty(Descripcion)) caja.Text = Descripcion;
            }

            public void ArmarValidadorSiEstaCompleto()
            {
                if (Validador is not null) return;
                if (CajaPlaca is null || CajaProveedor is null || CajaDescripcion is null) return;

                Validador = ValidadorFormulario.Nuevo()
                    .Campo(CajaPlaca, $"La placa del camión {Numero}").Segun(ReglasCamion.Placa)
                    .Catalogo(CajaProveedor, $"El proveedor del camión {Numero}", () => IdProveedor.HasValue)
                        .Segun(ReglasCamion.Proveedor)
                    .Campo(CajaDescripcion, $"La descripción del camión {Numero}").Segun(ReglasCamion.Descripcion)
                    .ValidarAlSalirDelCampo();
            }

            public void CopiarDe(FilaCamion otra)
            {
                Activa               = otra.Activa;
                CamionOriginal       = otra.CamionOriginal;
                _placaOriginal       = otra._placaOriginal;
                _idProveedorOriginal = otra._idProveedorOriginal;
                _descripcionOriginal = otra._descripcionOriginal;

                Placa       = otra.Placa;
                Proveedor   = otra.Proveedor;
                IdProveedor = otra.IdProveedor;
                Descripcion = otra.Descripcion;

                if (CajaPlaca       is not null) CajaPlaca.Text       = otra.Placa;
                if (CajaProveedor   is not null) CajaProveedor.Text   = otra.Proveedor;
                if (CajaDescripcion is not null) CajaDescripcion.Text = otra.Descripcion;
            }

            public void Limpiar()
            {
                CamionOriginal       = null;
                _placaOriginal       = "";
                _idProveedorOriginal = null;
                _descripcionOriginal = "";

                Placa       = "";
                Proveedor   = "";
                IdProveedor = null;
                Descripcion = "";

                if (CajaPlaca       is not null) CajaPlaca.Text       = "";
                if (CajaProveedor   is not null) CajaProveedor.Text   = "";
                if (CajaDescripcion is not null) CajaDescripcion.Text = "";
            }
        }
    }
}
