namespace CapaAplicacion.Common.Cache;

/// <summary>
/// Traduce eventos de Supabase Realtime en invalidaciones de caché.
///
/// <para><b>El ciclo de vida es la sesión, no el proceso.</b> Al cerrar sesión,
/// <c>RealtimeService.DesconectarAsync()</c> vacía su diccionario de suscriptores.
/// Como el contenedor de dependencias nunca se reconstruye, este servicio sigue
/// vivo en memoria y su constructor no vuelve a correr: si se hubiera suscrito
/// solo ahí, a partir del segundo login de la máquina la invalidación reactiva
/// quedaría muerta, sin excepción ni advertencia, sirviendo datos viejos hasta
/// que venza el TTL.</para>
///
/// <para>Por eso la suscripción es un método explícito que <c>MainWindow</c>
/// invoca en cada sesión, y es idempotente.</para>
/// </summary>
public interface IInvalidadorCacheRealtime
{
    /// <summary>
    /// Registra los observadores sobre las tablas de catálogo publicadas.
    /// Idempotente: llamarlo dos veces en la misma sesión no duplica handlers.
    /// </summary>
    void Suscribir();

    /// <summary>
    /// Da de baja los observadores. Debe invocarse ANTES de
    /// <c>IRealtimeService.DesconectarAsync()</c>, para soltar los tokens de forma
    /// limpia en vez de dejarlos huérfanos cuando se vacíe el diccionario.
    /// </summary>
    void Desuscribir();
}
