using System.Diagnostics;
using CapaAplicacion.Search.Queries;
using CapaAplicacion.Search.Registry;
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
        var tasks = _registry.GetAll().Select(s => s.SearchAsync(request.Term.Trim(), ct));
        var all   = await Task.WhenAll(tasks);
        var items = all.SelectMany(r => r).ToList();

        sw.Stop();
        return new UniversalSearchResult(items, items.Count, sw.Elapsed);
    }
}
