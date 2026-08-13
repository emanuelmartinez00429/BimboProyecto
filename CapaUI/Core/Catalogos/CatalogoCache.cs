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
/// podría ser grande. Así la segunda apertura de una lupa chica no toca la red,
/// y la cascada proveedor → fabricante se resuelve sin consultar.
/// </summary>
public static class CatalogoCache
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<FiltroItem>> _cache = new();

    /// <summary>
    /// Devuelve el catálogo completo si está cacheado. Si no, lo pide y lo guarda
    /// únicamente cuando entró entero.
    /// </summary>
    /// <returns>
    /// La lista completa, o <c>null</c> si el catálogo excede el umbral — en ese
    /// caso el llamador debe operar en modo paginado contra el servidor.
    /// </returns>
    public static async Task<Result<IReadOnlyList<FiltroItem>?>> ObtenerCompletoAsync(
        CatalogoConfig cfg, int umbral, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(cfg.Clave, out var cacheado))
            return Result<IReadOnlyList<FiltroItem>?>.Ok(cacheado);

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
    public static async Task<Result<IReadOnlyList<FiltroItem>>> ObtenerParaComboAsync(
        CatalogoConfig cfg, int umbral = 200, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(cfg.Clave, out var cacheado))
            return Result<IReadOnlyList<FiltroItem>>.Ok(cacheado);

        var r = await cfg.Cargar(string.Empty, 1, umbral, ct);
        if (!r.Success)
            return Result<IReadOnlyList<FiltroItem>>.Fail(r.Error);

        var pagina = r.Value!;
        if (pagina.Total <= umbral)
            _cache[cfg.Clave] = pagina.Items;

        return Result<IReadOnlyList<FiltroItem>>.Ok(pagina.Items);
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
