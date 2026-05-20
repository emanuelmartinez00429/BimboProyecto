using System.Linq.Expressions;
using CapaDominio.Entities;
using CapaDominio.Interfaces;

namespace CapaDatos.Repositories.Search;

/// <summary>Stub — tabla clientes aún no existe. Devuelve lista vacía.</summary>
public class ClienteRepository : IRepository<Cliente>
{
    public Task<IEnumerable<Cliente>> FindAsync(
        Expression<Func<Cliente, bool>> predicate,
        CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<Cliente>());
}
