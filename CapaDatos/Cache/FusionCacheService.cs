using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using ZiggyCreatures.Caching.Fusion;

namespace CapaDatos.Cache;

/// <summary>
/// Adaptador de <see cref="ICacheService"/> sobre FusionCache. Es la única clase
/// del sistema que conoce la librería: el resto habla contra el contrato de
/// CapaAplicacion.
/// </summary>
public sealed class FusionCacheService : ICacheService
{
    private readonly IFusionCache _cache;

    public FusionCacheService(IFusionCache cache) => _cache = cache;

    public async Task<Result<T>> ObtenerOCrearAsync<T>(
        string clave,
        Func<CancellationToken, Task<Result<T>>> fabrica,
        PoliticaCache politica,
        IEnumerable<string>? etiquetas = null,
        Func<T, bool>? esCacheable = null,
        CancellationToken ct = default)
    {
        try
        {
            var valor = await _cache.GetOrSetAsync<T>(
                clave,
                async (ctx, _) =>
                {
                    // La fábrica corre con CancellationToken.None a propósito. El
                    // single-flight fusiona las llamadas concurrentes sobre la misma
                    // clave: si esta ejecución respetara el token de SU llamador,
                    // cerrar un modal a los 100 ms abortaría la carga de las OTRAS
                    // pantallas que están esperando el mismo resultado.
                    var r = await fabrica(CancellationToken.None).ConfigureAwait(false);

                    // Un Result fallido no se cachea. ctx.Fail() le dice a FusionCache
                    // "esto salió mal": si hay valor viejo y la política lo tolera lo
                    // devuelve, y si no, la excepción sube y la traducimos abajo.
                    // Devolver Result.Fail(...) sería, para FusionCache, un valor
                    // perfectamente válido — y lo guardaría durante horas.
                    if (!r.Success)
                        return ctx.Fail(r.Error);

                    // Caché adaptativa: el valor ya está calculado, pero el llamador
                    // puede decidir que no vale la pena retenerlo (p. ej. un catálogo
                    // que no entró completo en una página). Se entrega igual, con
                    // vigencia cero y sin contingencia, para que no quede guardado.
                    if (esCacheable is not null && !esCacheable(r.Value!))
                    {
                        ctx.Options.Duration          = TimeSpan.Zero;
                        ctx.Options.IsFailSafeEnabled = false;
                    }

                    return r.Value!;
                },
                default,
                Aplicar(politica),
                etiquetas,
                ct).ConfigureAwait(false);

            return Result<T>.Ok(valor);
        }
        catch (OperationCanceledException)
        {
            // Neutralización defensiva. Los modales arrancan su carga desde un
            // handler `async void OnLoaded`: una excepción que escape de ahí no la
            // atrapa nadie y WPF cierra el proceso entero. Cerrar la lupa rápido es
            // una cancelación legítima, no un motivo para tirar la aplicación.
            return Result<T>.Fail("Operación cancelada.");
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[Cache] fallo resolviendo {Clave}", clave);
            return Result<T>.Fail(ex.Message);
        }
    }

    public void Invalidar(string clave) => _cache.Remove(clave);

    public void InvalidarEtiqueta(string etiqueta) => _cache.RemoveByTag(etiqueta);

    public Task InvalidarEtiquetaAsync(string etiqueta, CancellationToken ct = default) =>
        _cache.RemoveByTagAsync(etiqueta, token: ct).AsTask();

    /// <summary>
    /// allowFailSafe:false es obligatorio. Con el valor por defecto, la purga
    /// marca las entradas como vencidas pero el fail-safe puede volver a servirlas
    /// como "viejas aceptables" al siguiente usuario — que es exactamente la fuga
    /// entre sesiones que esta llamada existe para cerrar.
    /// </summary>
    public Task LimpiarTodoAsync() => _cache.ClearAsync(false).AsTask();

    private static FusionCacheEntryOptions Aplicar(PoliticaCache p)
    {
        var o = new FusionCacheEntryOptions
        {
            Duration          = p.Duracion,
            JitterMaxDuration = p.Jitter,
            IsFailSafeEnabled = p.ToleraViejo,

            // Si Supabase tarda más de lo razonable y HAY un valor viejo, se devuelve
            // ese y la carga termina por detrás. En un fallo en frío no hay atajo
            // posible: se espera hasta el timeout duro.
            FactorySoftTimeout = TimeSpan.FromMilliseconds(1500),
            FactoryHardTimeout = TimeSpan.FromSeconds(20),
        };

        if (p.MaxViejo is { } max)                 o.FailSafeMaxDuration      = max;
        if (p.EsperaEntreReintentos is { } espera) o.FailSafeThrottleDuration = espera;
        if (p.UmbralRefrescoAnticipado is { } u)   o.EagerRefreshThreshold    = u;

        return o;
    }
}
