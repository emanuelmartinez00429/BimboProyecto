using System.Collections.Concurrent;
using System.Threading.Channels;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Realtime;

namespace CapaDatos.Cache;

/// <summary>
/// Único suscriptor de Realtime que sobrevive a las pantallas: traduce eventos de
/// Postgres en purgas de caché.
///
/// <para>Hasta ahora todos los <c>Observar()</c> vivían en ViewModels transitorios
/// creados en <c>UserControl_Loaded</c>: con las pantallas cerradas nadie escuchaba
/// y la caché envejecía en silencio.</para>
/// </summary>
public sealed class InvalidadorCacheRealtime : IInvalidadorCacheRealtime, IDisposable
{
    /// <summary>
    /// Tabla publicada en <c>supabase_realtime</c> → etiqueta específica de caché.
    ///
    /// <para>No figuran acá <c>movimientos</c>, <c>movimiento_productos</c> ni
    /// <c>entradas_producto</c>: son zona sin caché, y suscribirse sólo agregaría
    /// ruido en el hilo de UI durante un pesaje. Tampoco <c>notificaciones_usuario</c>
    /// ni <c>bitacora</c>.</para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Mapa =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TagsCache.TablaPaises]       = TagsCache.DeTabla(TagsCache.TablaPaises),
            [TagsCache.TablaUnidadMedida] = TagsCache.DeTabla(TagsCache.TablaUnidadMedida),
            [TagsCache.TablaTara]         = TagsCache.DeTabla(TagsCache.TablaTara),
            [TagsCache.TablaCategoria]    = TagsCache.DeTabla(TagsCache.TablaCategoria),
            [TagsCache.TablaFabricante]   = TagsCache.DeTabla(TagsCache.TablaFabricante),
            [TagsCache.TablaPresentacion] = TagsCache.DeTabla(TagsCache.TablaPresentacion),
            [TagsCache.TablaProveedores]  = TagsCache.DeTabla(TagsCache.TablaProveedores),
            [TagsCache.TablaProductos]    = TagsCache.DeTabla(TagsCache.TablaProductos),
        };

    private readonly IRealtimeService _realtime;
    private readonly ICacheService    _cache;
    private readonly IConexionMonitor _conexion;

    private readonly ConcurrentDictionary<string, IDisposable> _suscripciones = new(StringComparer.Ordinal);
    private readonly Channel<string> _bitacora =
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly object _gate = new();

    private bool _suscrito;
    private bool _dispuesto;

    public InvalidadorCacheRealtime(
        IRealtimeService realtime, ICacheService cache, IConexionMonitor conexion)
    {
        _realtime = realtime;
        _cache    = cache;
        _conexion = conexion;

        _ = Task.Run(() => ConsumirBitacoraAsync(_cts.Token));
    }

    public void Suscribir()
    {
        lock (_gate)
        {
            if (_dispuesto || _suscrito) return;
            _suscrito = true;

            foreach (var (tabla, etiqueta) in Mapa)
                _suscripciones[tabla] = _realtime.Observar(tabla, _ => OnCambio(tabla, etiqueta));

            _conexion.Reconectado += OnReconectado;
        }

        Serilog.Log.Information(
            "[Cache] invalidador suscrito a {N} tablas de catálogo", Mapa.Count);
    }

    public void Desuscribir()
    {
        lock (_gate)
        {
            if (!_suscrito) return;
            _suscrito = false;

            _conexion.Reconectado -= OnReconectado;

            foreach (var s in _suscripciones.Values) s.Dispose();
            _suscripciones.Clear();
        }

        Serilog.Log.Information("[Cache] invalidador desuscrito");
    }

    /// <summary>
    /// Corre en el HILO DE UI: <c>RealtimeService</c> despacha los handlers por el
    /// contexto de sincronización que capturó al construirse.
    ///
    /// <para>Por eso acá no se hace trabajo. La purga es síncrona —escribir un
    /// marcador de etiqueta en RAM cuesta microsegundos— y es deliberadamente
    /// síncrona: los ViewModels suscritos a la misma tabla ejecutan su handler
    /// inmediatamente después, en el mismo recorrido de suscriptores, y recargan.
    /// Si la purga fuera diferida recargarían contra la entrada vieja.</para>
    ///
    /// <para>Lo único que se difiere es la línea de log, que va a disco.</para>
    /// </summary>
    private void OnCambio(string tabla, string etiqueta)
    {
        try
        {
            _cache.InvalidarEtiqueta(etiqueta);
            _bitacora.Writer.TryWrite(etiqueta);
        }
        catch (Exception ex)
        {
            // Estamos dentro de un handler de evento en el hilo de UI: si esto
            // escapa, se pierde sin dejar rastro.
            Serilog.Log.Warning(ex, "[Cache] falló invalidar {Etiqueta} por cambio en {Tabla}", etiqueta, tabla);
        }
    }

    /// <summary>
    /// Durante un corte de red se pierden eventos, así que al volver se purgan
    /// todos los catálogos de una.
    ///
    /// <para>Se engancha a <see cref="IConexionMonitor.Reconectado"/> —que ya aplica
    /// histéresis y sólo dispara con la conexión confirmada— y NO al estado
    /// <c>Reconnect</c> del socket. Ese estado se emite cuando el cliente detecta
    /// la caída y EMPIEZA a reintentar: purgar ahí vaciaría la caché justo mientras
    /// la máquina está sin red, y la siguiente lupa que se abriera iría a buscar
    /// datos que no puede traer, en vez de servir la copia de contingencia. Sería
    /// destruir el fail-safe exactamente en el momento en que hace falta.</para>
    /// </summary>
    private void OnReconectado(object? _, EventArgs __)
    {
        _ = PurgarCatalogosAsync();
    }

    private async Task PurgarCatalogosAsync()
    {
        try
        {
            await _cache.InvalidarEtiquetaAsync(TagsCache.CatalogosRaiz, _cts.Token).ConfigureAwait(false);
            Serilog.Log.Information("[Cache] purga de catálogos por reconexión de red");
        }
        catch (OperationCanceledException) { /* cierre de sesión */ }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[Cache] falló la purga por reconexión");
        }
    }

    /// <summary>
    /// Agrupa las líneas de log fuera del hilo de UI. Un guardado en cascada puede
    /// emitir varios eventos seguidos sobre la misma tabla; sin esto el log se
    /// llena de líneas repetidas.
    /// </summary>
    private async Task ConsumirBitacoraAsync(CancellationToken ct)
    {
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            while (await _bitacora.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                vistas.Clear();
                while (_bitacora.Reader.TryRead(out var etiqueta)) vistas.Add(etiqueta);

                foreach (var etiqueta in vistas)
                    Serilog.Log.Debug("[Cache] invalidada por Realtime: {Etiqueta}", etiqueta);

                await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* cierre de la aplicación */ }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_dispuesto) return;
            _dispuesto = true;
        }

        Desuscribir();
        _bitacora.Writer.TryComplete();
        _cts.Cancel();
        _cts.Dispose();
    }
}
