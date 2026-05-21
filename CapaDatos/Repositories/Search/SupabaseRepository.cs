using CapaDominio.Interfaces;
using ServicioConexión.Conexion;
using Supabase.Postgrest.Models;

namespace CapaDatos.Repositories.Search;

public abstract class SupabaseRepository<TDomain, TSupabase> : RepositorioBase, IRepository<TDomain>
    where TDomain  : class
    where TSupabase : BaseModel, new()
{
    protected virtual string SelectStatement => "*";

    protected abstract TDomain MapToDomain(TSupabase model);

    public abstract Task<IEnumerable<TDomain>> SearchAsync(string term, CancellationToken ct = default);

    protected async Task<Supabase.Client> GetClientAsync()
        => await ConexionSupabase.GetClientAsync();
}
