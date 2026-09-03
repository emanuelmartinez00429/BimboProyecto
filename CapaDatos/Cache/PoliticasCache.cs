using CapaAplicacion.Common.Cache;

namespace CapaDatos.Cache;

/// <summary>
/// Políticas nombradas por familia de dato. Están todas acá, y no repartidas por
/// los repositorios, para que "cuánto vale un dato viejo" sea una sola
/// conversación y no una decisión que cada quien toma por su cuenta.
///
/// <para><b>El TTL es la red de seguridad, no la estrategia.</b> La frescura la
/// garantiza el evento de Realtime: las 8 tablas de catálogo están publicadas, así
/// que una edición invalida su etiqueta al instante. Los TTL cubren el hueco de
/// que el socket se haya caído y se hayan perdido eventos.</para>
///
/// <para><b>MaxViejo siempre &gt; Duracion.</b> Si fuera menor, el valor se
/// descartaría antes de poder servirse como contingencia y el fail-safe quedaría
/// anulado sin que nada lo indique.</para>
/// </summary>
public static class PoliticasCache
{
    /// <summary>
    /// Países, unidades de medida y taras: cambian con una migración, no con el uso.
    /// </summary>
    public static readonly PoliticaCache UltraEstable = new(
        Duracion:                 TimeSpan.FromHours(24),
        Jitter:                   TimeSpan.FromMinutes(30),
        MaxViejo:                 TimeSpan.FromDays(7),
        EsperaEntreReintentos:    TimeSpan.FromSeconds(30),
        UmbralRefrescoAnticipado: 0.9f);

    /// <summary>
    /// Categorías, fabricantes, presentaciones y proveedores: se editan desde la
    /// propia aplicación, con su CRUD y su evento de Realtime.
    /// </summary>
    public static readonly PoliticaCache Negocio = new(
        Duracion:                 TimeSpan.FromHours(2),
        Jitter:                   TimeSpan.FromMinutes(15),
        MaxViejo:                 TimeSpan.FromHours(24),
        EsperaEntreReintentos:    TimeSpan.FromSeconds(30),
        UmbralRefrescoAnticipado: 0.85f);

    /// <summary>Productos: la tabla de catálogo de mayor rotación.</summary>
    public static readonly PoliticaCache Dinamico = new(
        Duracion:                 TimeSpan.FromMinutes(30),
        Jitter:                   TimeSpan.FromMinutes(5),
        MaxViejo:                 TimeSpan.FromHours(2),
        EsperaEntreReintentos:    TimeSpan.FromSeconds(15),
        UmbralRefrescoAnticipado: 0.8f);

    /// <summary>
    /// Estructura de módulos y acciones del RBAC. Son etiquetas del sistema, no
    /// asignaciones de permisos: las asignaciones por rol (acciones_roles) son
    /// zona sin caché, porque cachear una decisión de autorización no es una
    /// optimización, es un agujero.
    /// </summary>
    public static readonly PoliticaCache Rbac = new(
        Duracion:                 TimeSpan.FromHours(1),
        Jitter:                   TimeSpan.FromMinutes(10),
        MaxViejo:                 TimeSpan.FromHours(4),
        EsperaEntreReintentos:    TimeSpan.FromSeconds(30),
        UmbralRefrescoAnticipado: 0.9f);

    /// <summary>
    /// Sugerencias de búsqueda al teclear en SuggestionSearchBox.
    /// TTL corto (10 min) con jitter, tolerancia de contingencia (fail-safe 1 hora)
    /// y purga reactiva vinculada a los tags de Realtime de la tabla.
    /// </summary>
    public static readonly PoliticaCache Sugerencias = new(
        Duracion:                 TimeSpan.FromMinutes(10),
        Jitter:                   TimeSpan.FromMinutes(2),
        MaxViejo:                 TimeSpan.FromHours(1),
        EsperaEntreReintentos:    TimeSpan.FromSeconds(15),
        UmbralRefrescoAnticipado: null);
}
