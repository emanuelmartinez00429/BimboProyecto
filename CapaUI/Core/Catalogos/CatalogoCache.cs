using System.Collections.Concurrent;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;

namespace CapaUI.Core.Catalogos;

/// <summary>
/// Caché de sesión para los catálogos que entran enteros en memoria.
///
/// Solo se retiene lo que cupo completo en la primera página (Total &lt;= umbral).
/// Un catálogo que pagina nunca queda guardado: sería una vista parcial y además
/// podría ser grande.
///
/// <para><b>Mostrar y revalidar.</b> Lo cacheado se devuelve al instante y, en
/// paralelo, se vuelve a consultar la tabla; si la lista cambió, se avisa por
/// <c>alRevalidar</c> para que la pantalla se corrija sola. Así abrir una lupa no
/// espera a la red y, aun así, nunca se queda con un dato viejo: antes esta caché
/// no vencía nunca y editar un catálogo en la base no se veía hasta reiniciar la
/// aplicación. Un vencimiento por tiempo no alcanzaba — dejaba una ventana en la
/// que lo mostrado seguía siendo viejo.</para>
///
/// <para>La invalidación por Realtime tampoco alcanza como única red de
/// seguridad: solo unas pocas tablas están publicadas, así que para el resto el
/// evento no llega nunca. Cuando llega, <see cref="Invalidar"/> se suma a esto
/// sin conflicto.</para>
/// </summary>
public static class CatalogoCache
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<FiltroItem>> _cache = new();

    /// <summary>
    /// Devuelve el catálogo completo si está cacheado. Si no, lo pide y lo guarda
    /// únicamente cuando entró entero.
    /// </summary>
    /// <param name="alRevalidar">
    /// Se invoca solo si la revalidación encontró una lista distinta de la que se
    /// devolvió. Corre en el contexto de sincronización del llamador — llamando
    /// desde el hilo de UI, el callback vuelve solo al Dispatcher y puede tocar
    /// controles. Pasar <c>null</c> desactiva la revalidación.
    /// </param>
    /// <returns>
    /// La lista completa, o <c>null</c> si el catálogo excede el umbral — en ese
    /// caso el llamador debe operar en modo paginado contra el servidor.
    /// </returns>
    public static async Task<Result<IReadOnlyList<FiltroItem>?>> ObtenerCompletoAsync(
        CatalogoConfig cfg, int umbral,
        Action<IReadOnlyList<FiltroItem>>? alRevalidar = null,
        CancellationToken ct = default)
    {
        if (_cache.TryGetValue(cfg.Clave, out var cacheado))
        {
            if (alRevalidar is not null)
                _ = RevalidarAsync(cfg, umbral, cacheado, alRevalidar, ct);

            return Result<IReadOnlyList<FiltroItem>?>.Ok(cacheado);
        }

        var r = await cfg.Cargar(string.Empty, 1, umbral, ct);
        if (!r.Success)
            return Result<IReadOnlyList<FiltroItem>?>.Fail(r.Error);

        var pagina = r.Value!;

        // No entró entero: es un catálogo grande, no se cachea.
        if (pagina.Total > umbral)
            return Result<IReadOnlyList<FiltroItem>?>.Ok(null);

        _cache[cfg.Clave] = pagina.Items;
        return Result<IReadOnlyList<FiltroItem>?>.Ok(pagina.Items);
    }

    /// <summary>
    /// Variante para poblar combos, donde siempre hace falta una lista concreta.
    /// Si el catálogo excediera el umbral devuelve la primera página en vez de
    /// null — un combo con miles de items no es usable igual, y para esos casos
    /// corresponde la lupa con su selector paginado.
    /// </summary>
    /// <param name="alRevalidar">Igual que en <see cref="ObtenerCompletoAsync"/>.</param>
    public static async Task<Result<IReadOnlyList<FiltroItem>>> ObtenerParaComboAsync(
        CatalogoConfig cfg, int umbral = 200,
        Action<IReadOnlyList<FiltroItem>>? alRevalidar = null,
        CancellationToken ct = default)
    {
        if (_cache.TryGetValue(cfg.Clave, out var cacheado))
        {
            if (alRevalidar is not null)
                _ = RevalidarAsync(cfg, umbral, cacheado, alRevalidar, ct);

            return Result<IReadOnlyList<FiltroItem>>.Ok(cacheado);
        }

        var r = await cfg.Cargar(string.Empty, 1, umbral, ct);
        if (!r.Success)
            return Result<IReadOnlyList<FiltroItem>>.Fail(r.Error);

        var pagina = r.Value!;
        if (pagina.Total <= umbral)
            _cache[cfg.Clave] = pagina.Items;

        return Result<IReadOnlyList<FiltroItem>>.Ok(pagina.Items);
    }

    /// <summary>
    /// Vuelve a consultar el catálogo detrás de lo que ya se mostró. Es
    /// silenciosa: si falla no avisa nada, porque en pantalla hay una lista
    /// válida y molestar con un error por una verificación de fondo sería peor
    /// que quedarse con lo que había.
    /// </summary>
    private static async Task RevalidarAsync(
        CatalogoConfig cfg, int umbral,
        IReadOnlyList<FiltroItem> previo,
        Action<IReadOnlyList<FiltroItem>> alRevalidar,
        CancellationToken ct)
    {
        try
        {
            var r = await cfg.Cargar(string.Empty, 1, umbral, ct);
            if (!r.Success || ct.IsCancellationRequested) return;

            var pagina = r.Value!;

            // Creció y ya no entra en memoria: se descarta lo guardado para que la
            // próxima apertura arranque en modo paginado.
            if (pagina.Total > umbral)
            {
                Invalidar(cfg.Clave);
                return;
            }

            // Caso normal: nadie tocó la tabla. No se avisa, así la pantalla ni se
            // entera de que hubo revalidación y no parpadea al pedo.
            if (SonIguales(previo, pagina.Items)) return;

            _cache[cfg.Clave] = pagina.Items;
            alRevalidar(pagina.Items);
        }
        catch (OperationCanceledException) { /* se cerró la pantalla */ }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "CatalogoCache: revalidación de {Clave} falló", cfg.Clave);
        }
    }

    /// <summary>
    /// <see cref="FiltroItem"/> es class, no record: no tiene igualdad por valor y
    /// hay que comparar campo por campo.
    /// </summary>
    private static bool SonIguales(IReadOnlyList<FiltroItem> a, IReadOnlyList<FiltroItem> b)
    {
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Id          != b[i].Id          ||
                a[i].Nombre      != b[i].Nombre      ||
                a[i].Descripcion != b[i].Descripcion ||
                a[i].Activo      != b[i].Activo      ||
                a[i].IdPadre     != b[i].IdPadre)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Invalida una entrada y todos sus alcances derivados: pasar
    /// <c>"fabricantes"</c> también limpia <c>"fabricantes:7"</c>.
    /// </summary>
    public static void Invalidar(string clave)
    {
        foreach (var k in _cache.Keys)
            if (k == clave || k.StartsWith(clave + ":", StringComparison.Ordinal))
                _cache.TryRemove(k, out _);
    }

    public static void InvalidarTodo() => _cache.Clear();
}
