using CapaAplicacion.Realtime;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace CapaDatos.Realtime;

/// <summary>
/// Implementación de IRealtimeService sobre Supabase Realtime.
/// Singleton — gestiona una sola conexión WebSocket con canales on-demand.
///
/// Patrones: Facade (oculta Supabase API), Mediator (desacopla tablas de ViewModels),
///           Observer (suscriptores por tabla).
///
/// Thread-safety:
///   _stateLock (object)   — protege mutaciones rápidas de diccionarios
///   _lock (SemaphoreSlim) — serializa I/O async (apertura de canales)
///   Los dos nunca se adquieren juntos → sin riesgo de deadlock.
/// </summary>
public class RealtimeService : IRealtimeService
{
    private readonly SynchronizationContext? _syncContext;
    private readonly SemaphoreSlim _lock      = new(1, 1);
    private readonly object        _stateLock = new();

    private readonly Dictionary<string, IRealtimeChannel> _canales      = new();
    private readonly Dictionary<string, List<Action<CambioRealtime>>> _suscriptores = new();

    // Flag para registrar el handler de reconexión solo una vez (C7)
    private bool _estadoHandlerRegistrado;

    /// <summary>
    /// Mapeo tabla → columna PK. Usado para extraer el ID del registro
    /// desde el payload JSON crudo de Supabase Realtime.
    /// </summary>
    private static readonly Dictionary<string, string> _pkColumns = new()
    {
        ["productos"]              = "id_producto",
        ["categoria"]              = "id_categoria",
        ["empleados"]              = "id_empleado",
        ["usuarios"]               = "id_usuario",
        ["movimientos"]            = "id_movimiento",
        ["movimiento_productos"]   = "id_mov_producto",
        ["proveedores"]            = "id_proveedor",
        ["fabricante"]             = "id_fabricante",
        ["contactos_fabricante"]   = "id_contacto_fabricante",
        ["contactos_proveedor"]    = "id_contacto_proveedor",
        ["presentacion_producto"]  = "id_presentacion",
        ["paises"]                 = "id_pais",
        ["taras"]                  = "id_tara",
        ["tarimas"]                = "id_tarima",
    };

    public RealtimeService()
    {
        _syncContext = SynchronizationContext.Current;
        if (_syncContext is null)
            Serilog.Log.Warning(
                "RealtimeService: SynchronizationContext nulo en el constructor — " +
                "los handlers correrán en el hilo del socket en vez del UI thread. " +
                "Resolver este servicio en el hilo de UI para evitar cross-thread exceptions.");
    }

    // ── IRealtimeService ─────────────────────────────────────────────

    public async Task SuscribirAsync(string tabla, Action<CambioRealtime> handler)
    {
        bool necesitaCanal;

        lock (_stateLock)
        {
            if (!_suscriptores.ContainsKey(tabla))
                _suscriptores[tabla] = new List<Action<CambioRealtime>>();
            _suscriptores[tabla].Add(handler);
            necesitaCanal = !_canales.ContainsKey(tabla);
        }

        if (necesitaCanal)
        {
            await _lock.WaitAsync();
            try
            {
                bool yaExiste;
                lock (_stateLock) { yaExiste = _canales.ContainsKey(tabla); }
                if (!yaExiste)
                    await AbrirCanalAsync(tabla);
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    public void Desuscribir(string tabla, Action<CambioRealtime> handler)
    {
        lock (_stateLock)
        {
            if (!_suscriptores.TryGetValue(tabla, out var handlers)) return;
            handlers.Remove(handler);

            // Modelo "keep-open": el canal Supabase NO se cierra al quedar sin suscriptores.
            // Cerrar y reabrir canales provoca churn de Pushes (cada Push tiene un System.Timers.Timer)
            // y, como Client.Channel() deduplica por topic, reabrir reutiliza el MISMO canal y vuelve a
            // apilar Register()/AddPostgresChangeHandler() → fuga de bindings, despacho duplicado y
            // timers que no bajan. El canal permanece vivo durante la sesión y se libera de una sola vez
            // en DesconectarAsync() (reset total del cliente).
            if (handlers.Count == 0)
                _suscriptores.Remove(tabla);
        }
    }

    public IDisposable Observar(string tabla, Action<CambioRealtime> handler)
    {
        _ = SuscribirAsync(tabla, handler);   // apertura del canal en background
        return new Suscripcion(this, tabla, handler);
    }

    // ── Canal lifecycle ──────────────────────────────────────────────

    private async Task AbrirCanalAsync(string tabla)
    {
        var client = await ConexionSupabase.GetClientAsync();

        // ConnectAsync() es idempotente — si ya está conectado, no hace nada
        if (client.Realtime.Socket is null || !client.Realtime.Socket.IsConnected)
            await client.Realtime.ConnectAsync();

        // Registrar handler de log de reconexión una sola vez (C7)
        // Nota: RealtimeChannel.HandleSocketStateChanged ya re-suscribe canales
        // automáticamente tras reconexión del WebSocket (SDK 7.0.2, comportamiento nativo).
        lock (_stateLock)
        {
            if (!_estadoHandlerRegistrado)
            {
                _estadoHandlerRegistrado = true;
                client.Realtime.AddStateChangedHandler((_, state) =>
                {
                    if (state == Supabase.Realtime.Constants.SocketState.Reconnect)
                        Serilog.Log.Information("Realtime: WebSocket reconectando — el SDK re-suscribirá los canales activos");
                });
            }
        }

        var channel = client.Realtime.Channel($"rt-{tabla}");
        channel.Register(new PostgresChangesOptions("public", tabla, ListenType.All));
        channel.AddPostgresChangeHandler(ListenType.All, (_, change) => OnCambioRecibido(tabla, change));
        await channel.Subscribe();

        lock (_stateLock) { _canales[tabla] = channel; }
        Serilog.Log.Information("Realtime: canal '{Tabla}' abierto", tabla);
    }

    // ── Procesamiento de eventos ─────────────────────────────────────

    private void OnCambioRecibido(string tabla, PostgresChangesResponse change)
    {
        try
        {
            var cambio = ExtraerCambio(tabla, change);

            List<Action<CambioRealtime>> snapshot;
            lock (_stateLock)
            {
                if (!_suscriptores.TryGetValue(tabla, out var handlers)) return;
                snapshot = handlers.ToList();
            }

            DespacharEnUIThread(() =>
            {
                foreach (var h in snapshot)
                {
                    try   { h(cambio); }
                    catch (Exception ex)
                    { Serilog.Log.Warning(ex, "Realtime: error en handler para '{Tabla}'", tabla); }
                }
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Realtime: error al procesar cambio en '{Tabla}'", tabla);
        }
    }

    private static CambioRealtime ExtraerCambio(string tabla, PostgresChangesResponse change)
    {
        string operacion = change.Payload?.Data?.Type.ToString() ?? "UNKNOWN";

        long? id     = null;
        int?  estado = null;

        var data    = change.Payload?.Data;
        var rowData = data?.Record?.Record;
        if (rowData is JObject obj)
        {
            if (_pkColumns.TryGetValue(tabla, out var pkCol))
                id = obj.Value<long?>(pkCol);
            else
                Serilog.Log.Warning(
                    "Realtime P-008: tabla '{Tabla}' no tiene PK mapeada en _pkColumns.", tabla);

            estado = obj.Value<int?>("id_estado");
        }

        return new CambioRealtime(operacion, id, estado);
    }

    // ── Desconexión ─────────────────────────────────────────────────

    public async Task DesconectarAsync()
    {
        lock (_stateLock)
        {
            _canales.Clear();
            _suscriptores.Clear();
            _estadoHandlerRegistrado = false;   // el próximo login re-registra sobre el cliente nuevo
        }

        // Reset total: dispone el socket Realtime y el cliente Supabase completo (con todos sus
        // timers: Push._timer, RealtimeChannel._rejoinTimer y los del WebsocketClient interno).
        // El próximo login reconstruye un cliente limpio, sin canales ni handlers heredados de la
        // sesión previa. Es la única forma fiable de soltar lo acumulado, porque la librería no
        // expone una baja que libere los canales rooteados por el socket.
        await ConexionSupabase.ResetAsync();

        Serilog.Log.Information("Realtime: reset total — socket dispuesto y estado limpio");
    }

    // ── Marshaling al UI thread ──────────────────────────────────────

    private void DespacharEnUIThread(Action accion)
    {
        if (_syncContext != null)
            _syncContext.Post(_ => accion(), null);
        else
            accion();
    }

    // ── Token de suscripción (C4) ────────────────────────────────────

    /// <summary>
    /// Token devuelto por Observar(). Al hacer Dispose() se desuscribe automáticamente.
    /// Idempotente y thread-safe.
    /// </summary>
    private sealed class Suscripcion : IDisposable
    {
        private RealtimeService?              _svc;
        private readonly string               _tabla;
        private readonly Action<CambioRealtime> _handler;

        public Suscripcion(RealtimeService svc, string tabla, Action<CambioRealtime> handler)
            => (_svc, _tabla, _handler) = (svc, tabla, handler);

        public void Dispose()
        {
            var svc = Interlocked.Exchange(ref _svc, null);
            svc?.Desuscribir(_tabla, _handler);
        }
    }
}
