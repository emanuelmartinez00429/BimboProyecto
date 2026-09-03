namespace CapaAplicacion.Common.Cache;

/// <summary>
/// Caché de lectura de la aplicación. Un único almacén en memoria, compartido
/// por todo el proceso.
///
/// <para><b>Debe registrarse como Singleton.</b> Registrado como Transient cada
/// consumidor recibe su propio almacén vacío, la tasa de aciertos cae a cero y
/// no se lanza ninguna excepción: compila, corre y no hace absolutamente nada.</para>
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Devuelve el valor cacheado o ejecuta la fábrica. Las llamadas concurrentes
    /// sobre la misma clave comparten UNA sola ejecución (single-flight): abrir
    /// tres lupas del mismo catálogo a la vez produce una consulta, no tres.
    ///
    /// <para>Un <see cref="Result{T}"/> fallido NUNCA se cachea. Si hay un valor
    /// viejo y la política lo tolera se devuelve ése; si no, el error vuelve al
    /// llamador. Cachear el fallo convertiría un corte de dos segundos en horas
    /// de catálogo vacío.</para>
    ///
    /// <para>La cancelación del llamador abandona la espera pero NO cancela la
    /// fábrica: otras pantallas pueden estar esperando ese mismo single-flight.</para>
    ///
    /// <para><paramref name="esCacheable"/> decide, ya con el valor en la mano, si
    /// vale la pena guardarlo. Devolver <c>false</c> lo entrega al llamador sin
    /// retenerlo: es como se evita cachear un catálogo que no entró completo, cuya
    /// vista parcial contaminaría el modo paginado.</para>
    /// </summary>
    Task<Result<T>> ObtenerOCrearAsync<T>(
        string clave,
        Func<CancellationToken, Task<Result<T>>> fabrica,
        PoliticaCache politica,
        IEnumerable<string>? etiquetas = null,
        Func<T, bool>? esCacheable = null,
        CancellationToken ct = default);

    /// <summary>Invalida una clave puntual.</summary>
    void Invalidar(string clave);

    /// <summary>
    /// Invalida todas las entradas que lleven la etiqueta.
    ///
    /// <para><b>Es síncrono a propósito.</b> Lo llama el invalidador de Realtime,
    /// cuyos handlers corren en el hilo de UI, y los ViewModels suscritos a la
    /// misma tabla ejecutan su propio handler inmediatamente después, dentro del
    /// mismo recorrido de suscriptores. Si la purga fuera diferida, esos
    /// ViewModels recargarían contra la entrada vieja.</para>
    /// </summary>
    void InvalidarEtiqueta(string etiqueta);

    /// <summary>Variante asíncrona, para usar fuera del hilo de UI (reconexión de red).</summary>
    Task InvalidarEtiquetaAsync(string etiqueta, CancellationToken ct = default);

    /// <summary>
    /// Purga total. Se invoca al cerrar sesión: sin esto, los datos que cacheó un
    /// supervisor se le sirven al operario que entra después en la misma máquina,
    /// porque el contenedor de dependencias nunca se reconstruye entre sesiones.
    /// </summary>
    Task LimpiarTodoAsync();
}
