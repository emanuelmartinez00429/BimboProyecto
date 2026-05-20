using CapaAplicacion.Search.Strategies;

namespace CapaAplicacion.Search.Registry;

public class SearchStrategyRegistry
{
    private readonly IReadOnlyList<ISearchStrategy> _strategies;

    public SearchStrategyRegistry(IEnumerable<ISearchStrategy> strategies)
        => _strategies = strategies.OrderBy(s => s.Priority).ToList();

    public IReadOnlyList<ISearchStrategy> GetAll()     => _strategies;
    public ISearchStrategy? GetByType(string entityType)
        => _strategies.FirstOrDefault(s =>
            s.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase));
}
