using CapaDominio.Entities;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op = Supabase.Postgrest.Constants.Operator;
using ProductosModel = CapaDatos.Modelados.Productos.Productos;

namespace CapaDatos.Repositories.Search;

public class ProductoSearchRepository
    : SupabaseRepository<Producto, ProductosModel>
{
    protected override string SelectStatement =>
        "*, presentacion_producto(*), fabricante(*), categoria(*), paises(*)";

    protected override Producto MapToDomain(ProductosModel p) => new()
    {
        Id            = p.idProducto,
        CodigoInterno = p.codigoProducto    ?? string.Empty,
        Nombre        = p.nombreProducto    ?? string.Empty,
        Contenido     = p.contenidoProducto ?? string.Empty,
        Presentacion  = p.nombre_Presentacion,
        Fabricante    = p.nombre_Fabricante,
        Categoria     = p.nombre_Categoria,
        Pais          = p.nombre_Pais,
        IdEstado      = p.idEstado,
    };

    public override async Task<IEnumerable<Producto>> SearchAsync(string term, CancellationToken ct = default)
    {
        var client  = await GetClientAsync();
        var pattern = $"%{term}%";
        var response = await client
            .From<ProductosModel>()
            .Select(SelectStatement)
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_producto", Op.ILike, pattern),
                new QueryFilter("codigo_producto",  Op.ILike, pattern),
                new QueryFilter("contenido",        Op.ILike, pattern),
            })
            .Get();
        return response.Models.Select(MapToDomain).ToList();
    }
}
