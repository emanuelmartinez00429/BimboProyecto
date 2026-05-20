using System.Linq.Expressions;
using CapaDominio.Interfaces;
using ServicioConexión.Conexion;
using Supabase.Postgrest.Models;

namespace CapaDatos.Repositories.Search;

public abstract class SupabaseRepository<TDomain, TSupabase> : IRepository<TDomain>
    where TDomain  : class
    where TSupabase : BaseModel, new()
{
    protected virtual string SelectStatement => "*";

    protected abstract TDomain MapToDomain(TSupabase model);

    public async Task<IEnumerable<TDomain>> FindAsync(
        Expression<Func<TDomain, bool>> predicate,
        CancellationToken ct = default)
    {
        var client   = await ConexionSupabase.GetClientAsync();
        var response = await client
            .From<TSupabase>()
            .Select(SelectStatement)
            .Get();

        return response.Models
            .Select(MapToDomain)
            .Where(predicate.Compile())
            .ToList();
    }
}
