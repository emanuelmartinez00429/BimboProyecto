namespace CapaDominio.Interfaces;

public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> SearchAsync(string term, CancellationToken ct = default);
}
