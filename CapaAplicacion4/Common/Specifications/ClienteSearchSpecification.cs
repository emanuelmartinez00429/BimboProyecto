using System.Linq.Expressions;
using CapaDominio.Entities;

namespace CapaAplicacion.Common.Specifications;

public class ClienteSearchSpecification : ISpecification<Cliente>
{
    private readonly string _term;
    public ClienteSearchSpecification(string term) => _term = term.ToLower().Trim();

    public Expression<Func<Cliente, bool>> Criteria => c =>
        c.RazonSocial.ToLower().Contains(_term) ||
        c.RTN.Contains(_term)                   ||
        c.Codigo.ToLower().Contains(_term);
}
