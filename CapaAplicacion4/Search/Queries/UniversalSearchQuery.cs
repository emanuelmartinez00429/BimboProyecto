using CapaAplicacion.Search.Dtos;
using MediatR;

namespace CapaAplicacion.Search.Queries;

public record UniversalSearchQuery(
    string Term,
    int    MaxResultsPerEntity = 10
) : IRequest<UniversalSearchResult>;

public record UniversalSearchResult(
    IReadOnlyList<SearchResultDto> Items,
    int                            TotalCount,
    TimeSpan                       Elapsed
)
{
    public static UniversalSearchResult Empty =>
        new([], 0, TimeSpan.Zero);
}
