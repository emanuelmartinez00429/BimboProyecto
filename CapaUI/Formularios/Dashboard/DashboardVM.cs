using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CapaAplicacion.Conexion;
using CapaAplicacion.Dashboard;
using CapaAplicacion.Dashboard.Interfaces;
using CapaAplicacion.Realtime;
using CapaUI.Core.MVVM;

namespace CapaUI.Formularios.Dashboard
{
    public enum MermaTone { Normal, Warning, Critical }

    // ══════════════════════════════════════════════════════════════════════
    //  ViewModel principal del Dashboard
    // ══════════════════════════════════════════════════════════════════════
    public class DashboardVM : RealtimeAwareViewModel
    {
        private readonly IDashboardRepository _dashboardRepo;
        private readonly CancellationTokenSource _ctsLifetime = new();
        private CancellationTokenSource? _ctsInventario;
        private CancellationTokenSource? _ctsKpis;
        private CancellationTokenSource? _ctsMerma;
        private CancellationTokenSource? _ctsUltimos;
        private int _cargasActivas;
        private readonly object _errorLock = new();
        private string? _errorInventario;
        private string? _errorKpis;
        private string? _errorMerma;
        private string? _errorUltimos;

        // ── Fecha capitalizada ────────────────────────────────────────────
        public string DateLabel { get; }

        // ── Estados de carga y error ──────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        // ── KPI Inventario ────────────────────────────────────────────────
        private string _prodCount = "0";
        public string ProdCount
        {
            get => _prodCount;
            private set => SetProperty(ref _prodCount, value);
        }

        private string _provCount = "0";
        public string ProvCount
        {
            get => _provCount;
            private set => SetProperty(ref _provCount, value);
        }

        private string _marcaCount = "0";
        public string MarcaCount
        {
            get => _marcaCount;
            private set => SetProperty(ref _marcaCount, value);
        }

        // ── KPI Pesajes y Mermas ──────────────────────────────────────────
        private string _pesajeCount = "0";
        public string PesajeCount
        {
            get => _pesajeCount;
            private set => SetProperty(ref _pesajeCount, value);
        }

        private string _totalNeto = "0";
        public string TotalNeto
        {
            get => _totalNeto;
            private set => SetProperty(ref _totalNeto, value);
        }

        private string _pctMerma = "0.0";
        public string PctMerma
        {
            get => _pctMerma;
            private set => SetProperty(ref _pctMerma, value);
        }

        // ── Indicadores de tendencia (Badges) ─────────────────────────────
        private string _prodTrend = "-";
        public string ProdTrend
        {
            get => _prodTrend;
            private set => SetProperty(ref _prodTrend, value);
        }

        private string _provTrend = "-";
        public string ProvTrend
        {
            get => _provTrend;
            private set => SetProperty(ref _provTrend, value);
        }

        private string _pesajesTrend = "-";
        public string PesajesTrend
        {
            get => _pesajesTrend;
            private set => SetProperty(ref _pesajesTrend, value);
        }

        private string _netoTrend = "-";
        public string NetoTrend
        {
            get => _netoTrend;
            private set => SetProperty(ref _netoTrend, value);
        }

        private string _mermaTrend = "-";
        public string MermaTrend
        {
            get => _mermaTrend;
            private set => SetProperty(ref _mermaTrend, value);
        }

        // ── Períodos independientes ───────────────────────────────────────
        private PeriodoDashboard _periodoKpis = PeriodoDashboard.Hoy;
        public PeriodoDashboard PeriodoKpis
        {
            get => _periodoKpis;
            private set
            {
                if (SetProperty(ref _periodoKpis, value))
                    OnPropertyChanged(nameof(PeriodoKpisTexto));
            }
        }

        public string PeriodoKpisTexto => EtiquetaPeriodo(PeriodoKpis);

        private PeriodoDashboard _periodoMerma = PeriodoDashboard.Hoy;
        public PeriodoDashboard PeriodoMerma
        {
            get => _periodoMerma;
            private set => SetProperty(ref _periodoMerma, value);
        }

        public ICommand SelectPeriodoKpisCommand { get; }
        public ICommand SelectPeriodoMermaCommand { get; }
        public ICommand RefreshCommand { get; }

        // ── Datos de gráfica y lista ──────────────────────────────────────
        public ObservableCollection<MermaItemVM> TopMerma { get; } = new();
        public ObservableCollection<PesajeRowVM> Ultimos  { get; } = new();

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
            => SetProperty(ref field, value, name);

        public DashboardVM(
            IDashboardRepository dashboardRepo,
            IRealtimeService realtime,
            IConexionMonitor conexionMonitor)
            : base(realtime, conexionMonitor)
        {
            _dashboardRepo = dashboardRepo;

            var cultura = new CultureInfo("es-MX");
            var raw     = DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy", cultura);
            DateLabel   = cultura.TextInfo.ToTitleCase(raw);

            SelectPeriodoKpisCommand = new RelayCommand(async p =>
                await CambiarPeriodoKpisAsync(ConvertirPeriodo(p)));
            SelectPeriodoMermaCommand = new RelayCommand(async p =>
                await CambiarPeriodoMermaAsync(ConvertirPeriodo(p)));
            RefreshCommand = new RelayCommand(async () => await InicializarAsync());

            Observar("entradas_producto", OnCambioEntradaRealtime);

            _ = InicializarAsync();
        }

        /// <summary>
        /// Inicializa todas las secciones del dashboard de forma asíncrona.
        /// </summary>
        public async Task InicializarAsync()
        {
            if (Disposed) return;

            var inventarioTask = EjecutarCargaAsync(
                ErrorSeccion.Inventario,
                CrearToken(ref _ctsInventario),
                CargarInventarioAsync);
            var kpisTask = EjecutarCargaAsync(
                ErrorSeccion.Kpis,
                CrearToken(ref _ctsKpis),
                ct => CargarKpisPesajesAsync(PeriodoKpis, ct));
            var mermaTask = EjecutarCargaAsync(
                ErrorSeccion.Merma,
                CrearToken(ref _ctsMerma),
                ct => CargarTopMermaAsync(PeriodoMerma, ct));
            var ultimosTask = EjecutarCargaAsync(
                ErrorSeccion.Ultimos,
                CrearToken(ref _ctsUltimos),
                CargarUltimosPesajesAsync);

            await Task.WhenAll(inventarioTask, kpisTask, mermaTask, ultimosTask);
        }

        /// <summary>
        /// Cambia únicamente el período de los KPI de pesajes.
        /// </summary>
        public Task CambiarPeriodoKpisAsync(PeriodoDashboard nuevoPeriodo)
        {
            if (Disposed) return Task.CompletedTask;

            PeriodoKpis = nuevoPeriodo;
            return EjecutarCargaAsync(
                ErrorSeccion.Kpis,
                CrearToken(ref _ctsKpis),
                ct => CargarKpisPesajesAsync(nuevoPeriodo, ct));
        }

        /// <summary>
        /// Cambia únicamente el período del reporte Top 5 de mermas.
        /// </summary>
        public Task CambiarPeriodoMermaAsync(PeriodoDashboard nuevoPeriodo)
        {
            if (Disposed) return Task.CompletedTask;

            PeriodoMerma = nuevoPeriodo;
            return EjecutarCargaAsync(
                ErrorSeccion.Merma,
                CrearToken(ref _ctsMerma),
                ct => CargarTopMermaAsync(nuevoPeriodo, ct));
        }

        private async Task CargarInventarioAsync(CancellationToken ct)
        {
            try
            {
                var resultado = await _dashboardRepo.ObtenerKpisInventarioAsync(ct);
                if (ct.IsCancellationRequested || Disposed) return;

                if (!resultado.Success || resultado.Value is null)
                {
                    ActualizarError(
                        ErrorSeccion.Inventario,
                        resultado.Error ?? "Error al cargar KPIs de inventario");
                    return;
                }

                var dto = resultado.Value;
                ProdCount  = dto.TotalProductos.ToString("N0", CultureInfo.InvariantCulture);
                ProvCount  = dto.TotalProveedores.ToString("N0", CultureInfo.InvariantCulture);
                MarcaCount = dto.TotalMarcas.ToString("N0", CultureInfo.InvariantCulture);

                ProdTrend = dto.DeltaProductos.HasValue
                    ? $"{(dto.DeltaProductos.Value >= 0 ? "▲ +" : "▼ ")}{Math.Abs(dto.DeltaProductos.Value)}"
                    : "-";

                ProvTrend = dto.DeltaProveedores.HasValue
                    ? $"{(dto.DeltaProveedores.Value >= 0 ? "▲ +" : "▼ ")}{Math.Abs(dto.DeltaProveedores.Value)}"
                    : "-";
            }
            catch (OperationCanceledException) { }
        }

        private async Task CargarKpisPesajesAsync(PeriodoDashboard periodo, CancellationToken ct)
        {
            try
            {
                var resultado = await _dashboardRepo.ObtenerKpisPesajesAsync(periodo, ct);
                if (ct.IsCancellationRequested || Disposed || periodo != PeriodoKpis)
                    return;

                if (resultado.Success && resultado.Value is { } kpis)
                {
                    PesajeCount = kpis.PesajesActual.ToString("N0", CultureInfo.InvariantCulture);
                    TotalNeto   = kpis.NetoActual.ToString("N0", CultureInfo.InvariantCulture);
                    PctMerma    = kpis.PctMermaActual.HasValue
                        ? kpis.PctMermaActual.Value.ToString("F1", CultureInfo.InvariantCulture)
                        : "0.0";

                    PesajesTrend = kpis.DeltaPesajesPct.HasValue
                        ? $"{(kpis.DeltaPesajesPct.Value >= 0 ? "▲ +" : "▼ ")}{Math.Abs(kpis.DeltaPesajesPct.Value):F0}%"
                        : "-";

                    NetoTrend = kpis.DeltaNetoPct.HasValue
                        ? $"{(kpis.DeltaNetoPct.Value >= 0 ? "▲ +" : "▼ ")}{Math.Abs(kpis.DeltaNetoPct.Value):F0}%"
                        : "-";

                    MermaTrend = kpis.DeltaMermaPct.HasValue
                        ? $"{(kpis.DeltaMermaPct.Value >= 0 ? "▲ +" : "▼ ")}{Math.Abs(kpis.DeltaMermaPct.Value):F1}"
                        : "-";
                }
                else
                {
                    ActualizarError(
                        ErrorSeccion.Kpis,
                        resultado.Error ?? "Error al consultar KPIs de pesajes");
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task CargarTopMermaAsync(PeriodoDashboard periodo, CancellationToken ct)
        {
            try
            {
                var resultado = await _dashboardRepo.ObtenerTopMermaAsync(periodo, 5, ct);
                if (ct.IsCancellationRequested || Disposed || periodo != PeriodoMerma)
                    return;

                if (!resultado.Success || resultado.Value is null)
                {
                    ActualizarError(
                        ErrorSeccion.Merma,
                        resultado.Error ?? "Error al consultar reporte de mermas");
                    return;
                }

                var mermas = resultado.Value;
                double maxPct = mermas.Count > 0 ? Math.Max(5.0, mermas.Max(x => x.Porcentaje)) : 5.8;
                var nuevosItems = mermas.Select(item =>
                    new MermaItemVM(item.Nombre, Math.Round(item.Porcentaje, 1), item.Kilos, maxPct)).ToList();

                EjecutarEnUI(() =>
                {
                    if (ct.IsCancellationRequested || Disposed || periodo != PeriodoMerma) return;
                    TopMerma.Clear();
                    foreach (var item in nuevosItems)
                    {
                        TopMerma.Add(item);
                    }
                });
            }
            catch (OperationCanceledException) { }
        }

        private async Task CargarUltimosPesajesAsync(CancellationToken ct = default)
        {
            try
            {
                var resultado = await _dashboardRepo.ObtenerUltimosPesajesAsync(5, ct);
                if (ct.IsCancellationRequested || Disposed) return;

                if (!resultado.Success || resultado.Value is null)
                {
                    ActualizarError(
                        ErrorSeccion.Ultimos,
                        resultado.Error ?? "Error al obtener los últimos pesajes");
                    return;
                }

                var nuevosPesajes = resultado.Value.Select(p =>
                    new PesajeRowVM(p.Codigo, p.Producto, p.Kilos, p.Hora, p.EsAlerta)).ToList();

                EjecutarEnUI(() =>
                {
                    if (ct.IsCancellationRequested || Disposed) return;
                    Ultimos.Clear();
                    foreach (var p in nuevosPesajes)
                    {
                        Ultimos.Add(p);
                    }
                });
            }
            catch (OperationCanceledException) { }
        }

        private void OnCambioEntradaRealtime(CambioRealtime cambio)
        {
            if (Disposed) return;
            _ = EjecutarCargaAsync(
                ErrorSeccion.Ultimos,
                CrearToken(ref _ctsUltimos),
                CargarUltimosPesajesAsync);
            _ = CambiarPeriodoKpisAsync(PeriodoKpis);
            _ = CambiarPeriodoMermaAsync(PeriodoMerma);
        }

        protected override async Task OnReconexionAsync()
        {
            if (Disposed) return;
            await InicializarAsync();
        }

        protected override void OnDispose()
        {
            _ = CancelarYDisponerAsync(Interlocked.Exchange(ref _ctsInventario, null));
            _ = CancelarYDisponerAsync(Interlocked.Exchange(ref _ctsKpis, null));
            _ = CancelarYDisponerAsync(Interlocked.Exchange(ref _ctsMerma, null));
            _ = CancelarYDisponerAsync(Interlocked.Exchange(ref _ctsUltimos, null));
            _ = CancelarYDisponerAsync(_ctsLifetime);

            base.OnDispose();
        }

        private async Task EjecutarCargaAsync(
            ErrorSeccion seccion,
            CancellationToken token,
            Func<CancellationToken, Task> carga)
        {
            IniciarCarga();
            ActualizarError(seccion, null);
            try
            {
                await carga(token);
            }
            catch (OperationCanceledException)
            {
                // La selección más reciente reemplazó esta carga.
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested && !Disposed)
                    ActualizarError(seccion, ex.Message);
            }
            finally
            {
                FinalizarCarga();
            }
        }

        private CancellationToken CrearToken(ref CancellationTokenSource? campo)
        {
            if (Disposed || _ctsLifetime.IsCancellationRequested)
                return new CancellationToken(canceled: true);

            var nuevo = CancellationTokenSource.CreateLinkedTokenSource(_ctsLifetime.Token);
            var anterior = Interlocked.Exchange(ref campo, nuevo);
            _ = CancelarYDisponerAsync(anterior);
            return nuevo.Token;
        }

        private static async Task CancelarYDisponerAsync(CancellationTokenSource? cts)
        {
            if (cts is null) return;
            try
            {
                await cts.CancelAsync();
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            finally
            {
                cts.Dispose();
            }
        }

        private void IniciarCarga()
        {
            if (Interlocked.Increment(ref _cargasActivas) == 1)
                EjecutarEnUI(() => IsLoading = true);
        }

        private void FinalizarCarga()
        {
            if (Interlocked.Decrement(ref _cargasActivas) == 0)
                EjecutarEnUI(() => IsLoading = false);
        }

        private void ActualizarError(ErrorSeccion seccion, string? mensaje)
        {
            string? errorActual;
            lock (_errorLock)
            {
                switch (seccion)
                {
                    case ErrorSeccion.Inventario: _errorInventario = mensaje; break;
                    case ErrorSeccion.Kpis: _errorKpis = mensaje; break;
                    case ErrorSeccion.Merma: _errorMerma = mensaje; break;
                    case ErrorSeccion.Ultimos: _errorUltimos = mensaje; break;
                }

                errorActual = new[] { _errorInventario, _errorKpis, _errorMerma, _errorUltimos }
                    .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));
            }

            EjecutarEnUI(() =>
            {
                ErrorMessage = errorActual;
                HasError = errorActual is not null;
            });
        }

        private static PeriodoDashboard ConvertirPeriodo(object? valor)
        {
            if (valor is PeriodoDashboard periodo)
                return periodo;

            return Enum.TryParse(valor?.ToString(), ignoreCase: true, out PeriodoDashboard resultado)
                ? resultado
                : PeriodoDashboard.Hoy;
        }

        private static string EtiquetaPeriodo(PeriodoDashboard periodo) => periodo switch
        {
            PeriodoDashboard.Semana => "Semana",
            PeriodoDashboard.Mes => "Últimos 30 días",
            _ => "Hoy",
        };

        private enum ErrorSeccion
        {
            Inventario,
            Kpis,
            Merma,
            Ultimos,
        }

        private static void EjecutarEnUI(Action action)
        {
            try
            {
                var app = Application.Current;
                if (app?.Dispatcher is { } d && !d.HasShutdownStarted)
                {
                    if (d.CheckAccess())
                        action();
                    else
                        d.Invoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException or ObjectDisposedException)
            {
                // Ignorar cancelación o cierre de dispatcher
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Item de barra de merma
    // ══════════════════════════════════════════════════════════════════════
    public class MermaItemVM
    {
        private static readonly Brush CriticalBrush = CreateFrozenGradient(Color.FromRgb(0xDC, 0x26, 0x26), Color.FromRgb(0xF8, 0x71, 0x71));
        private static readonly Brush WarningBrush  = CreateFrozenGradient(Color.FromRgb(0xF5, 0x9E, 0x0B), Color.FromRgb(0xFB, 0xBF, 0x24));
        private static readonly Brush NormalBrush   = CreateFrozenGradient(Color.FromRgb(0x1E, 0x3A, 0x8A), Color.FromRgb(0x3B, 0x82, 0xF6));
        private static readonly Brush FallbackBrush = CreateFrozenSolid(Color.FromRgb(0x25, 0x63, 0xEB));

        private static Brush CreateFrozenGradient(Color c1, Color c2)
        {
            var brush = new LinearGradientBrush(
                new GradientStopCollection(new[]
                {
                    new GradientStop(c1, 0),
                    new GradientStop(c2, 1),
                }),
                new Point(0, 0.5), new Point(1, 0.5));
            brush.Freeze();
            return brush;
        }

        private static Brush CreateFrozenSolid(Color c)
        {
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            return brush;
        }

        private readonly double _maxPct;

        public string    Name       { get; }
        public double    Pct        { get; }
        public int       Kg         { get; }
        public MermaTone Tone       => Pct >= 4 ? MermaTone.Critical
                                     : Pct >= 3 ? MermaTone.Warning
                                                : MermaTone.Normal;
        public double    BarPercent => _maxPct > 0 ? Math.Clamp(Pct / _maxPct * 100.0, 0.0, 100.0) : 0.0;
        public string    PctText    => $"{Pct:N1}%";
        public string    KgText     => $"{Kg:N0} kg";

        public Brush BarBrush => Tone switch
        {
            MermaTone.Critical => CriticalBrush,
            MermaTone.Warning  => WarningBrush,
            _ => Application.Current?.Resources["EmpresaPrimaryGradientBrush"] as Brush
                 ?? NormalBrush,
        };

        public MermaItemVM(string name, double pct, int kg, double maxPct = 5.8)
        {
            Name = name;
            Pct = pct;
            Kg = kg;
            _maxPct = maxPct;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Fila de pesaje reciente
    // ══════════════════════════════════════════════════════════════════════
    public class PesajeRowVM
    {
        public string     Code          { get; }
        public string     Producto      { get; }
        public string     Kg            { get; }
        public string     Hora          { get; }
        public bool       IsWarn        { get; }
        public Visibility WarnVisibility => IsWarn ? Visibility.Visible : Visibility.Collapsed;

        public PesajeRowVM(string code, string producto, string kg, string hora, bool isWarn)
        {
            Code = code;
            Producto = producto;
            Kg = kg;
            Hora = hora;
            IsWarn = isWarn;
        }
    }
}
