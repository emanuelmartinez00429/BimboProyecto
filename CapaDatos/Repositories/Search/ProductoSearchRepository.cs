using CapaAplicacion.Common;
using CapaDominio.Entities;
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

    /// <summary>
    /// Un solo filtro contra <c>busqueda_producto</c> (columna generada = nombre +
    /// codigo, ya en minusculas y sin tildes) en vez del OR sobre las columnas
    /// crudas: asi "azucar" encuentra "AZÚCAR". El termino se normaliza del mismo
    /// modo para que los dos lados coincidan.
    /// <para/>
    /// Es el mismo filtro que ya usaban el formulario
    /// (<c>ProductoCrudRepository.BuscarSugerenciasInternal</c>) y el picker de
    /// Pesaje; el buscador universal habia quedado atras y era el unico sensible a
    /// acentos y mayusculas. Ver [[ADR-018 - Busqueda insensible a mayusculas y
    /// tildes con columna generada]].
    /// <para/>
    /// Se deja de buscar por <c>contenido</c>, que si cubria el OR anterior: la
    /// columna generada no lo incluye, y en la practica el campo esta vacio o nulo
    /// en 171 de 178 productos — los pocos que lo tienen guardan la unidad ("500 g"),
    /// que no es por lo que se busca. Si algun dia se llena de verdad, la salida es
    /// extender la columna generada (y de paso el formulario y el picker lo ganan),
    /// no volver al OR crudo.
    /// </summary>
    public override async Task<IEnumerable<Producto>> SearchAsync(string term, CancellationToken ct = default)
    {
        var client = await GetClientAsync();
        var aguja  = TextoBusqueda.Normalizar(term);

        var response = await client
            .From<ProductosModel>()
            .Select(SelectStatement)
            .Filter("busqueda_producto", Op.ILike, $"%{aguja}%")
            .Get(ct);

        return response.Models.Select(MapToDomain).ToList();
    }
}
