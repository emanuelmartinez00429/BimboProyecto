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
/// </summary>
public class RealtimeService : IRealtimeService
{
    private readonly SynchronizationContext? _syncContext;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Lock ligero para operaciones rápidas sobre los diccionarios.
    // Evita usar SemaphoreSlim.Wait() (síncrono) que causa deadlock en UI thread.
    private readonly object _stateLock = new();

    private readonly Dictionary<string, IRealtimeChannel> _canales = new();
    private readonly Dictionary<string, List<Action<CambioRealtime>>> _suscriptores = new();

    /// <summary>
    /// Mapeo tabla → columna PK. Se usa para extraer el ID del registro
    /// desde el payload JSON crudo de Supabase Realtime.
    /// </summary>
    private static readonly Dictionary<string, string> _pkColumns = new()
    {
        ["productos"]              = "id_producto",
        ["categorias"]             = "id_categoria",
        ["empleados"]              = "id_empleado",
        ["usuarios"]               = "id_usuario",
        ["movimientos"]            = "id_movimiento",
        ["movimiento_productos"]   = "id_mov_producto",
        ["proveedores"]            = "id_proveedor",
        ["fabricante"]             = "id_fabricante",
        ["presentacion_producto"]  = "id_presentacion",
        ["paises"]                 = "id_pais",
        ["taras"]                  = "id_tara",
        ["tarimas"]                = "id_tarima",
    };

    public RealtimeService()
    {
        // Captura el SynchronizationContext del UI thread.
        // El DI lo construye en el hilo principal de WPF,
        // así que aquí capturamos el Dispatcher context.
        _syncContext = SynchronizationContext.Current;
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

        // Abrir canal fuera del lock rápido — usa SemaphoreSlim solo para I/O
        if (necesitaCanal)
        {
            await _lock.WaitAsync();
            try
            {
                // Double-check: otro hilo pudo abrirlo mientras esperábamos
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

            // Último suscriptor → cerrar canal
            if (handlers.Count == 0)
            {
                _suscriptores.Remove(tabla);
                CerrarCanal(tabla);
            }
        }
    }

    // ── Canal lifecycle ──────────────────────────────────────────────

    private async Task AbrirCanalAsync(string tabla)
    {
        var client = await ConexionSupabase.GetClientAsync();

        // AutoConnectRealtime = true inicia la conexión, pero puede no estar lista aún.
        // ConnectAsync() es idempotente — si ya está conectado, no hace nada.
        if (client.Realtime.Socket is null || !client.Realtime.Socket.IsConnected)
            await client.Realtime.ConnectAsync();

        var channel = client.Realtime.Channel($"rt-{tabla}");
        channel.Register(new PostgresChangesOptions("public", tabla, ListenType.All));
        channel.AddPostgresChangeHandler(ListenType.All, (_, change) => OnCambioRecibido(tabla, change));
        await channel.Subscribe();

        lock (_stateLock) { _canales[tabla] = channel; }
        Serilog.Log.Information("Realtime: canal '{Tabla}' abierto", tabla);
    }

    private void CerrarCanal(string tabla)
    {
        if (!_canales.TryGetValue(tabla, out var channel)) return;

        channel.Unsubscribe();
        _canales.Remove(tabla);
        Serilog.Log.Information("Realtime: canal '{Tabla}' cerrado", tabla);
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
                    try { h(cambio); }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "Realtime: error en handler para '{Tabla}'", tabla);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Realtime: error al procesar cambio en '{Tabla}'", tabla);
        }
    }

    /// <summary>
    /// Extrae operación, ID del registro y nuevo estado desde el payload
    /// crudo de Supabase Realtime, sin requerir un tipo genérico.
    /// </summary>
    private static CambioRealtime ExtraerCambio(string tabla, PostgresChangesResponse change)
    {
        string operacion = change.Payload?.Data?.Type.ToString() ?? "UNKNOWN";

        long? id = null;
        int? estado = null;

        // data.Record   → SocketResponsePayload (wrapper del SDK)
        // data.Record.Record → object (JObject con los valores reales de columnas)
        var data    = change.Payload?.Data;
        var rowData = data?.Record?.Record; // object? — JObject en runtime
        if (rowData is JObject obj)
        {
            if (_pkColumns.TryGetValue(tabla, out var pkCol))
                id = obj.Value<long?>(pkCol);
            else
                Serilog.Log.Warning(
                    "Realtime P-008: tabla '{Tabla}' no tiene PK mapeada en _pkColumns. " +
                    "Agrégala para que IdRegistro se extraiga correctamente.", tabla);

            estado = obj.Value<int?>("id_estado");
        }

        return new CambioRealtime(operacion, id, estado);
    }

    // ── Desconexión ────────────────────────────────────────────────

    public Task DesconectarAsync()
    {
        lock (_stateLock)
        {
            // Cerrar todos los canales
            foreach (var tabla in _canales.Keys.ToList())
                CerrarCanal(tabla);

            _suscriptores.Clear();
        }

        // No llamamos Disconnect() al WebSocket:
        // - La conexión es ligera (~1-2 KB) y se reutiliza en el próximo login.
        // - Disconnect() hace que ConnectAsync() sea lento al reconectar,
        //   lo que ampliaba la ventana de deadlock.
        // - Los canales ya están cerrados, así que no llegan más eventos.

        Serilog.Log.Information("Realtime: desconectado — todos los canales cerrados");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Despacha un callback al UI thread usando el SynchronizationContext
    /// capturado en el constructor. Esto evita que CapaDatos dependa de WPF.
    /// </summary>
    private void DespacharEnUIThread(Action accion)
    {
        if (_syncContext != null)
            _syncContext.Post(_ => accion(), null);
        else
            accion();
    }
}
