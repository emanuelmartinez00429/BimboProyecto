using System.Linq.Expressions;

namespace CapaDominio.Interfaces;

public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default);
}
