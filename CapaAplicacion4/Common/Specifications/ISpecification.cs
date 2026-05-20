using System.Linq.Expressions;

namespace CapaAplicacion.Common.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
}
