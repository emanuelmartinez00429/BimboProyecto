using CapaDatos.Modelados.Productos;
using CapaDominio.Entities;

namespace CapaDatos.Repositories.Search;

public class ProductoRepository
    : SupabaseRepository<Producto, Productos>
{
    protected override string SelectStatement =>
        "*, presentacion_producto(*), fabricante(*), categoria(*), paises(*)";

    protected override Producto MapToDomain(Productos p) => new()
    {
        Id            = p.idProducto,
        CodigoInterno = p.codigoProducto ?? string.Empty,
        Nombre        = p.nombreProducto ?? string.Empty,
        Contenido     = p.contenidoProducto ?? string.Empty,
        Presentacion  = p.nombre_Presentacion,
        Fabricante    = p.nombre_Fabricante,
        Categoria     = p.nombre_Categoria,
        Pais          = p.nombre_Pais,
        IdEstado      = p.idEstado,
    };
}
