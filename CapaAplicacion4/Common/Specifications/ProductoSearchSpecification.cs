using System.Linq.Expressions;
using CapaDominio.Entities;

namespace CapaAplicacion.Common.Specifications;

public class ProductoSearchSpecification : ISpecification<Producto>
{
    private readonly string _term;
    public ProductoSearchSpecification(string term) => _term = term.ToLower().Trim();

    public Expression<Func<Producto, bool>> Criteria => p =>
        p.Nombre.ToLower().Contains(_term)       ||
        p.Fabricante.ToLower().Contains(_term)   ||
        p.Contenido.ToLower().Contains(_term)    ||
        p.Presentacion.ToLower().Contains(_term) ||
        p.Categoria.ToLower().Contains(_term)    ||
        p.CodigoInterno.ToLower().Contains(_term);
}
