namespace CapaAplicacion.Common.Cache;

/// <summary>
/// Política de vigencia de una entrada de caché. Vive en CapaAplicacion —y solo
/// con <see cref="TimeSpan"/>, sin dependencias externas— para que la capa de
/// aplicación pueda declarar "cuánto vale este dato" sin conocer el motor.
/// </summary>
/// <param name="Duracion">Vigencia nominal del valor.</param>
/// <param name="Jitter">
/// Desvío aleatorio máximo que se suma a la duración. Los catálogos se pueblan
/// juntos al abrir una pantalla; sin jitter vencen en bloque y la siguiente
/// carga dispara todas las consultas a la vez. Con 15 terminales arrancando
/// turno a la misma hora, el pico se multiplica por 15.
/// </param>
/// <param name="ToleraViejo">
/// Ante un fallo de la fuente, servir el valor vencido en vez de propagar el
/// error. Encaja con el fail-fast de RepositorioBase: sin red es preferible una
/// lista de ayer a un "Sin conexión a internet." con la pantalla vacía.
/// </param>
/// <param name="MaxViejo">
/// Cuánto tiempo sigue siendo aceptable ese valor vencido. Debe ser MAYOR que
/// <paramref name="Duracion"/>; si fuera menor, el fail-safe queda anulado.
/// </param>
/// <param name="EsperaEntreReintentos">
/// Tras un fallo, cuánto esperar antes de volver a llamar a la fuente. Evita
/// martillar un backend caído en cada lectura.
/// </param>
/// <param name="UmbralRefrescoAnticipado">
/// Fracción de la duración a partir de la cual una lectura devuelve el valor
/// vigente y dispara la actualización por detrás, sin hacer esperar a nadie.
/// Es lo que reemplaza al "mostrar y revalidar" de ADR-015, que revalidaba en
/// CADA apertura y hacía que un acierto de caché pagara el viaje igual.
/// <c>null</c> desactiva el refresco anticipado.
/// </param>
public sealed record PoliticaCache(
    TimeSpan  Duracion,
    TimeSpan  Jitter,
    bool      ToleraViejo              = true,
    TimeSpan? MaxViejo                 = null,
    TimeSpan? EsperaEntreReintentos    = null,
    float?    UmbralRefrescoAnticipado = null);
