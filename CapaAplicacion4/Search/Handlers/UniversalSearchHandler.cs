using System.Diagnostics;
using CapaAplicacion.Search.Dtos;
using CapaAplicacion.Search.Queries;
using CapaAplicacion.Search.Registry;
using CapaAplicacion.Search.Strategies;
using MediatR;

namespace CapaAplicacion.Search.Handlers;

public class UniversalSearchHandler
    : IRequestHandler<UniversalSearchQuery, UniversalSearchResult>
{
    private readonly SearchStrategyRegistry _registry;
    private const    int                    MinTermLength = 2;

    public UniversalSearchHandler(SearchStrategyRegistry registry)
        => _registry = registry;

    public async Task<UniversalSearchResult> Handle(
        UniversalSearchQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Term)
            || request.Term.Trim().Length < MinTermLength)
            return UniversalSearchResult.Empty;

        var sw    = Stopwatch.StartNew();
        var tasks = _registry.GetAll().Select(s => SearchSeguroAsync(s, request.Term.Trim(), ct));
        var all   = await Task.WhenAll(tasks);
        var items = all.SelectMany(r => r).ToList();

        sw.Stop();
        return new UniversalSearchResult(items, items.Count, sw.Elapsed);
    }

    // Aisla el fallo de una estrategia: si una entidad revienta (ej. un filtro
    // mal construido contra Supabase), las demás igual devuelven sus resultados.
    // Sin esto, Task.WhenAll propaga la primera excepcion y el buscador global
    // completo queda sin resultados en vez de degradar solo esa entidad.
    private static async Task<IEnumerable<SearchResultDto>> SearchSeguroAsync(
        ISearchStrategy strategy, string term, CancellationToken ct)
    {
        try
        {
            return await strategy.SearchAsync(term, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UniversalSearch:{strategy.EntityType}] {ex}");
            return Enumerable.Empty<SearchResultDto>();
        }
    }
}
