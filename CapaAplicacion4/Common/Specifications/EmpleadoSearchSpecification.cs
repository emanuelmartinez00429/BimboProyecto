using System.Linq.Expressions;
using CapaDominio.Entities;

namespace CapaAplicacion.Common.Specifications;

public class EmpleadoSearchSpecification : ISpecification<Empleado>
{
    private readonly string _term;
    public EmpleadoSearchSpecification(string term) => _term = term.ToLower().Trim();

    public Expression<Func<Empleado, bool>> Criteria => e =>
        e.Nombres.ToLower().Contains(_term)   ||
        e.Apellidos.ToLower().Contains(_term) ||
        e.Identidad.Contains(_term)           ||
        e.Correo.ToLower().Contains(_term);
}
