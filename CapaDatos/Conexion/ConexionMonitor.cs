using System.Configuration;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CapaAplicacion.Conexion;

namespace CapaDatos.Conexion;

/// <summary>
/// Implementación de <see cref="IConexionMonitor"/>. Singleton.
///
/// Detección (decisión del proyecto: ICMP):
///   • <see cref="NetworkChange"/> → alta/baja de NIC (event-driven, sin polling).
///   • <see cref="Ping"/> ICMP a IPs públicas → distingue "Conectado" de "Degradado".
///   • Timer adaptativo que SOLO corre si hay NIC operativa (cero pings sin cable/wifi).
///   • Histéresis sobre las mediciones de ping para que el label no parpadee.
///
/// Threading: captura el SynchronizationContext del hilo de UI en el ctor (igual que
/// RealtimeService) y despacha los eventos ahí. El sondeo corre en background.
/// </summary>
public sealed class ConexionMonitor : IConexionMonitor, IDisposable
{
    private readonly SynchronizationContext? _syncContext;
    private readonly object _gate = new();

    // ── Configuración (App.config con defaults) ──────────────────────
    private readonly string[] _targets;
    private readonly int      _timeoutMs;
    private readonly int      _intervaloOkMs;
    private readonly int      _intervaloDegradadoMs;
    private readonly int      _histeresis;

    // ── Estado ───────────────────────────────────────────────────────
    private System.Threading.Timer? _timer;
    private bool _iniciado;
    private int  _sondeando;   // 0/1 — evita sondeos solapados (Interlocked)

    private EstadoConexion _estado          = EstadoConexion.Desconocido;
    private EstadoConexion _ultimaMedicion  = EstadoConexion.Desconocido;
    private int            _medicionesIguales;

    public EstadoConexion Estado => _estado;

    public event EventHandler<EstadoConexion>? EstadoCambiado;
    public event EventHandler? Reconectado;

    public ConexionMonitor()
    {
        _syncContext = SynchronizationContext.Current;
        if (_syncContext is null)
            Serilog.Log.Warning(
                "ConexionMonitor: SynchronizationContext nulo en el ctor — los eventos correrán " +
                "fuera del UI thread. Resolver este singleton primero en el hilo de UI.");

        _targets              = LeerTargets();
        _timeoutMs            = LeerInt("CONEXION_PING_TIMEOUT_MS",        1500);
        _intervaloOkMs        = LeerInt("CONEXION_INTERVALO_OK_MS",        5000);
        _intervaloDegradadoMs = LeerInt("CONEXION_INTERVALO_DEGRADADO_MS", 3000);
        _histeresis           = Math.Max(1, LeerInt("CONEXION_HISTERESIS", 2));
    }

    // ── IConexionMonitor ──────────────────────────────────────────────

    public void Iniciar()
    {
        lock (_gate)
        {
            if (_iniciado) return;
            _iniciado = true;
        }

        NetworkChange.NetworkAvailabilityChanged += OnDisponibilidadRedCambiada;
        NetworkChange.NetworkAddressChanged       += OnDireccionRedCambiada;

        EvaluarRed();   // evaluación inmediata al arrancar
    }

    public void Detener()
    {
        lock (_gate)
        {
            if (!_iniciado) return;
            _iniciado = false;
            // Reset silencioso para la próxima sesión (sin disparar eventos en el teardown).
            _estado            = EstadoConexion.Desconocido;
            _ultimaMedicion    = EstadoConexion.Desconocido;
            _medicionesIguales = 0;
        }

        NetworkChange.NetworkAvailabilityChanged -= OnDisponibilidadRedCambiada;
        NetworkChange.NetworkAddressChanged       -= OnDireccionRedCambiada;
        PararTimer();
    }

    // ── Eventos de red (event-driven, sin polling) ────────────────────

    private void OnDisponibilidadRedCambiada(object? s, NetworkAvailabilityEventArgs e) => EvaluarRed();
    private void OnDireccionRedCambiada(object? s, EventArgs e)                          => EvaluarRed();

    /// <summary>
    /// Decide si hay NIC operativa. Sin NIC ⇒ SinConexion y se para el timer (cero pings).
    /// Con NIC ⇒ arranca el timer y sondea de inmediato.
    /// </summary>
    private void EvaluarRed()
    {
        if (!HayRedFisica())
        {
            PararTimer();
            AplicarMedicion(EstadoConexion.SinConexion, rtt: -1, conHisteresis: false);
            return;
        }

        ProgramarTimer();          // arranca / mantiene el sondeo periódico
        _ = SondearAhoraAsync();   // sondeo inmediato (no esperar al timer)
    }

    // ── Sondeo periódico ──────────────────────────────────────────────

    private void OnTimerTick(object? _) => _ = SondearAhoraAsync();

    private async Task SondearAhoraAsync()
    {
        // Solo un sondeo a la vez.
        if (Interlocked.CompareExchange(ref _sondeando, 1, 0) != 0) return;

        try
        {
            // La NIC pudo caer entre ticks.
            if (!HayRedFisica())
            {
                PararTimer();
                AplicarMedicion(EstadoConexion.SinConexion, rtt: -1, conHisteresis: false);
                return;
            }

            var (ok, rtt) = await SondearAsync();
            AplicarMedicion(ok ? EstadoConexion.Conectado : EstadoConexion.Degradado, rtt, conHisteresis: true);
        }
        finally
        {
            Interlocked.Exchange(ref _sondeando, 0);
            // Reprogramar el siguiente tick si seguimos activos y hay NIC.
            lock (_gate)
            {
                if (_iniciado && _timer is not null && HayRedFisica())
                    _timer.Change(IntervaloActual(), Timeout.Infinite);
            }
        }
    }

    /// <summary>
    /// Sondeo ICMP. Devuelve (true, rtt) si algún target responde dentro del timeout.
    /// Aislado en su propio método para poder cambiarlo por una sonda HTTP a Supabase
    /// en el futuro sin tocar el resto de la lógica.
    /// </summary>
    private async Task<(bool ok, long rtt)> SondearAsync()
    {
        foreach (var target in _targets)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(target, _timeoutMs);
                if (reply.Status == IPStatus.Success && reply.RoundtripTime <= _timeoutMs)
                    return (true, reply.RoundtripTime);
            }
            catch (PingException) { /* probar el siguiente target */ }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "ConexionMonitor: error al hacer ping a {Target}", target);
            }
        }
        return (false, -1);
    }

    // ── Transición de estado (con histéresis) ─────────────────────────

    private void AplicarMedicion(EstadoConexion medicion, long rtt, bool conHisteresis)
    {
        EstadoConexion anterior;
        bool esReconexion;

        lock (_gate)
        {
            if (!_iniciado) return;

            // Histéresis: exige N mediciones iguales seguidas antes de cambiar el estado
            // publicado (el primer sondeo desde Desconocido se acepta de inmediato).
            if (conHisteresis)
            {
                if (medicion == _ultimaMedicion) _medicionesIguales++;
                else { _ultimaMedicion = medicion; _medicionesIguales = 1; }

                bool confirmado = _medicionesIguales >= _histeresis || _estado == EstadoConexion.Desconocido;
                if (!confirmado) return;
            }
            else
            {
                _ultimaMedicion    = medicion;
                _medicionesIguales = _histeresis;   // baja de NIC: determinista, sin histéresis
            }

            if (medicion == _estado) return;        // sin cambio real

            anterior     = _estado;
            _estado      = medicion;
            esReconexion = medicion == EstadoConexion.Conectado
                           && anterior is EstadoConexion.Degradado or EstadoConexion.SinConexion;
        }

        // Log de la transición a nivel Warning para que sea visible bajo el mínimo
        // actual de Serilog (Warning) sin tocar la configuración del logger.
        Serilog.Log.Warning("Conexión: {Anterior} → {Nuevo} (RTT={Rtt}ms)", anterior, medicion, rtt);

        Despachar(() => EstadoCambiado?.Invoke(this, medicion));
        if (esReconexion)
            Despachar(() => Reconectado?.Invoke(this, EventArgs.Empty));
    }

    // ── Timer helpers ─────────────────────────────────────────────────

    private int IntervaloActual() => _estado == EstadoConexion.Degradado ? _intervaloDegradadoMs : _intervaloOkMs;

    private void ProgramarTimer()
    {
        lock (_gate)
        {
            if (!_iniciado) return;
            _timer ??= new System.Threading.Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
            _timer.Change(IntervaloActual(), Timeout.Infinite);
        }
    }

    private void PararTimer()
    {
        lock (_gate) { _timer?.Change(Timeout.Infinite, Timeout.Infinite); }
    }

    // ── Marshaling al UI thread ───────────────────────────────────────

    private void Despachar(Action accion)
    {
        if (_syncContext is not null) _syncContext.Post(_ => accion(), null);
        else accion();
    }

    // ── Configuración ─────────────────────────────────────────────────

    private static string[] LeerTargets()
    {
        var raw = ConfigurationManager.AppSettings["CONEXION_PING_TARGETS"];
        if (string.IsNullOrWhiteSpace(raw)) return new[] { "1.1.1.1", "8.8.8.8" };
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int LeerInt(string key, int porDefecto)
    {
        var raw = ConfigurationManager.AppSettings[key];
        return int.TryParse(raw, out var v) && v > 0 ? v : porDefecto;
    }

    // ── Detección de red física ───────────────────────────────────────

    /// <summary>
    /// True si hay al menos una interfaz física (Ethernet o Wi-Fi) operativa y con gateway real.
    /// Descarta adaptadores virtuales (Hyper-V, VMware, VirtualBox, WSL, Docker, loopback, túneles),
    /// que suelen estar "Up" aunque no haya cable/Wi-Fi. Sin esto, apagar el Wi-Fi reportaría
    /// Degradado ("Sin internet") en vez de SinConexion ("Sin conexión"), porque
    /// <see cref="NetworkInterface.GetIsNetworkAvailable"/> ve la NIC virtual como disponible.
    /// </summary>
    private static bool HayRedFisica()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet
                                                  or NetworkInterfaceType.Wireless80211)) continue;

                var desc = (ni.Description + " " + ni.Name).ToLowerInvariant();
                if (desc.Contains("virtual")  || desc.Contains("vmware")     || desc.Contains("hyper-v")
                 || desc.Contains("vethernet")|| desc.Contains("virtualbox") || desc.Contains("loopback")
                 || desc.Contains("pseudo")   || desc.Contains("tap")        || desc.Contains("tunnel")
                 || desc.Contains("wsl")      || desc.Contains("docker"))
                    continue;

                // Debe tener un gateway IPv4 real (descarta host-only y APIPA 169.254.x.x).
                var props = ni.GetIPProperties();
                bool tieneGateway = props.GatewayAddresses.Any(g =>
                    g?.Address is { } ip
                    && ip.AddressFamily == AddressFamily.InterNetwork
                    && !ip.Equals(IPAddress.Any));
                if (tieneGateway) return true;
            }
            return false;
        }
        catch
        {
            // Si la enumeración falla, degradar al chequeo básico del SO.
            return NetworkInterface.GetIsNetworkAvailable();
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────

    public void Dispose()
    {
        Detener();
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
