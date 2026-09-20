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
    /// Gestión integral de los camiones del andén: muestra las recepciones abiertas para
    /// consultarlas o editarlas, y permite incorporar nuevas al proceso de descarga.
    /// </summary>
    /// <remarks>
    /// Una fila = una recepción (placa + proveedor). Un camión con carga de tres proveedores
    /// se registra en tres filas con la misma placa. El cupo y la unicidad placa + proveedor
    /// los decide <see cref="ReglasCamion.ValidarRecepciones"/>; la BD los vuelve a exigir.
    /// </remarks>
    public partial class RegistroCamionesModal : UserControl, IDisposable
    {
        private readonly ICatalogoRepository _catalogos = null!;
        private System.Windows.Media.Animation.Storyboard? _spinnerGuardar;

        private IReadOnlyList<CamionPesaje> _camionesAbiertos = null!;

        private readonly ObservableCollection<FilaCamion> _filas = new();
        private readonly List<CamionPesaje> _bajas = new();

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
            InitializeComponent();
        }

        public RegistroCamionesModal(
            IReadOnlyList<CamionPesaje> camionesAbiertos,
            CamionPesaje? enfocar = null)
        {
            InitializeComponent();

            _camionesAbiertos = camionesAbiertos;

            _catalogos   = App.Services.GetRequiredService<ICatalogoRepository>();
            _anchoPropio = Width;
            _altoPropio  = Height;

            for (int i = 1; i <= PesajeViewModel.MaxCamiones; i++)
                _filas.Add(new FilaCamion(i) { EsAlterna = i % 2 == 0 });

            // Las recepciones abiertas ocupan las primeras filas. Nunca son más que el cupo
            // (la BD no lo permite), así que todas entran en la tabla.
            for (int i = 0; i < camionesAbiertos.Count && i < _filas.Count; i++)
            {
                _filas[i].EstablecerOriginal(camionesAbiertos[i]);
                _filas[i].Activa = true;
            }

            // Si no había ninguna, se activa la primera fila para empezar a cargar.
            if (camionesAbiertos.Count == 0)
                _filas[0].Activa = true;

            FilasHost.ItemsSource = _filas;

            TxtInstruccion.Text =
                $"Gestioná los camiones del andén o registrá nuevas descargas (hasta " +
                $"{PesajeViewModel.MaxCamiones} en total). Si un camión trae carga de varios " +
                "proveedores, registralo una vez por proveedor con la misma placa.";

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

        private static string Normalizar(CamionPesaje c) => ReglasCamion.NormalizarPlaca(c.Placa);

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

            var primerNueva = _filas.FirstOrDefault(f => f.Activa && !f.EsExistente);
            for (int i = 0; i < guardadas && primerNueva != null; i++)
            {
                CompactarDesde(primerNueva);
                primerNueva = _filas.FirstOrDefault(f => f.Activa && !f.EsExistente);
            }

            if (Ocupadas == 0) _filas[0].Activa = true;

            LimpiarMarcasDeError();
            ActualizarContadores();
        }

        // ── Selector de catálogo (Proveedor) ────────────────────────────────────

        private void BuscarProveedor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement el || el.DataContext is not FilaCamion fila) return;

            _filaDelSelector = fila;

            // R5: el selector atenúa los proveedores que esa placa ya tiene abiertos.
            string placaDeFila = fila.PlacaNormalizada;
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
                    if (f.PlacaNormalizada == placaDeFila)
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
            partes.Add("Ctrl+Enter para finalizar");
            TxtPie.Text = string.Join(" · ", partes);

            BtnAgregarFila.IsEnabled = ocupadas < PesajeViewModel.MaxCamiones;

            // El cupo no se puede pasar desde acá: la tabla tiene exactamente MaxCamiones filas.
            // Las reglas entre filas (duplicados) se reportan al guardar; mientras el operador
            // edita, el error del intento anterior deja de aplicar.
            PanelError.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// R4 + R5 sobre lo que quedaría abierto después de guardar: las filas activas
        /// reemplazan a su recepción original, y las bajas dejan de contar.
        /// </summary>
        private IReadOnlyList<string> ErroresDeReglas()
        {
            var propuestas = _filas
                .Where(f => f.Activa)
                .Select(f => new ReglasCamion.RecepcionClave(f.CamionOriginal?.Id, f.PlacaNormalizada, f.IdProveedor ?? 0, f.Numero))
                .ToList();

            var abiertas = _camionesAbiertos
                .Where(c => !_bajas.Any(b => b.Id == c.Id))
                .Select(c => new ReglasCamion.RecepcionClave(c.Id, c.Placa, c.IdProveedor ?? 0))
                .ToList();

            return ReglasCamion.ValidarRecepciones(abiertas, propuestas);
        }

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

            var errores = ErroresDeReglas();
            if (errores.Count > 0)
            {
                MostrarError(errores[0]);
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

            /// <summary>Campos editables de una recepción existente; el tracker compara por valor.</summary>
            private sealed record Snapshot(string Placa, int? IdProveedor, string Descripcion);

            private ChangeTracker<Snapshot>? _tracker;

            private Snapshot Actual => new(PlacaNormalizada, IdProveedor, Descripcion.Trim());

            public string PlacaNormalizada => ReglasCamion.NormalizarPlaca(Placa);

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
                _tracker             = ChangeTracker.Create(Actual);
            }

            public bool Modificada => EsExistente && _tracker is not null && _tracker.IsDirty(Actual);

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
                _tracker             = otra._tracker;

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
                _tracker             = null;

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
