using CapaAplicacion.Search.Dtos;

namespace CapaAplicacion.Search.Strategies;

public interface ISearchStrategy
{
    string EntityType { get; }
    int    Priority   { get; }
    Task<IEnumerable<SearchResultDto>> SearchAsync(string term, CancellationToken ct);
}
