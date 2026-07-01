using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Pesaje.Interfaces;
using CapaAplicacion.Productos.Dtos;
using CapaDatos.Modelados.Productos;
using CapaDatos.Repositories;
using ServicioConexión.Conexion;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
using ProductosModel = CapaDatos.Modelados.Productos.Productos;

namespace CapaDatos.Repositories.Pesaje;

/// <summary>
/// Selector de productos para el módulo de Pesaje. Extensión aditiva: reutiliza
/// el patrón RepositorioBase + Result + ILike server-side + Limit(10) de
/// <see cref="Productos.ProductoCrudRepository"/>, sin modificar ningún archivo
/// existente del buscador.
///
/// Acota por proveedor con el puente producto→fabricante→proveedor en 2 pasos
/// (fabricante es una tabla diminuta), evitando filtros sobre recursos embebidos
/// de PostgREST (ver nota "Bug - Filter OR con Op.Equals en postgrest-csharp").
/// </summary>
public class PickerProductoRepository : RepositorioBase, IPickerProductoRepository
{
    public PickerProductoRepository(IConexionMonitor conexion) : base(conexion) { }

    private const string Select =
        "*, presentacion_producto(*), fabricante(*), categoria(*), paises(*)";

    public Task<Result<IReadOnlyList<ProductoDto>>> TopPorProveedorAsync(
        int idProveedor, int limit = 10, CancellationToken ct = default) =>
        TryAsync(() => TopPorProveedorInternal(idProveedor, limit), "Top productos del proveedor");

    public Task<Result<IReadOnlyList<ProductoDto>>> BuscarPorProveedorAsync(
        string termino, int idProveedor, CancellationToken ct = default) =>
        TryAsync(() => BuscarPorProveedorInternal(termino, idProveedor), "Buscar productos del proveedor");

    public Task<Result<IReadOnlyList<ProductoDto>>> BuscarTodosAsync(
        string termino, CancellationToken ct = default) =>
        TryAsync(() => BuscarTodosInternal(termino), "Buscar productos (todos)");

    // ── Interno ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ProductoDto>> TopPorProveedorInternal(int idProveedor, int limit)
    {
        var client = await ConexionSupabase.GetClientAsync();

        var idsFabricante = await FabricantesDeProveedor(client, idProveedor);
        if (idsFabricante.Count == 0) return [];

        var resultado = await client.From<ProductosModel>()
            .Select(Select)
            .Filter("id_fabricante", Op.In, idsFabricante)
            .Order("nombre_producto", Ord.Ascending)
            .Limit(limit)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<IReadOnlyList<ProductoDto>> BuscarPorProveedorInternal(string termino, int idProveedor)
    {
        var client = await ConexionSupabase.GetClientAsync();

        var idsFabricante = await FabricantesDeProveedor(client, idProveedor);
        if (idsFabricante.Count == 0) return [];

        var resultado = await client.From<ProductosModel>()
            .Select(Select)
            .Filter("id_fabricante", Op.In, idsFabricante)
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
                new QueryFilter("codigo_producto", Op.ILike, $"%{termino}%"),
            })
            .Order("nombre_producto", Ord.Ascending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<IReadOnlyList<ProductoDto>> BuscarTodosInternal(string termino)
    {
        var client = await ConexionSupabase.GetClientAsync();

        var resultado = await client.From<ProductosModel>()
            .Select(Select)
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
                new QueryFilter("codigo_producto", Op.ILike, $"%{termino}%"),
            })
            .Order("nombre_producto", Ord.Ascending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    /// <summary>
    /// Paso 1 del puente: ids de fabricante que pertenecen al proveedor.
    /// Reutiliza <see cref="FabricanteConsulta"/>; el filtro es por columna
    /// (id_proveedor) aunque el modelo no mapee esa propiedad.
    /// </summary>
    private static async Task<List<object>> FabricantesDeProveedor(Supabase.Client client, int idProveedor)
    {
        var fabricantes = await client.From<FabricanteConsulta>()
            .Select("id_fabricante")
            .Filter("id_proveedor", Op.Equals, idProveedor.ToString())
            .Get();

        return (fabricantes?.Models ?? [])
            .Select(f => (object)f.idFabricante)
            .ToList();
    }

    // Mapeo idéntico al de ProductoCrudRepository (copiado a propósito para no
    // tocar el archivo existente — deuda técnica menor documentada en el plan).
    private static ProductoDto Map(ProductosModel p) => new()
    {
        Id             = p.idProducto,
        CodigoInterno  = p.codigoProducto    ?? string.Empty,
        Nombre         = p.nombreProducto    ?? string.Empty,
        Contenido      = p.contenidoProducto ?? string.Empty,
        Presentacion   = p.nombre_Presentacion,
        Fabricante     = p.nombre_Fabricante,
        Categoria      = p.nombre_Categoria,
        Pais           = p.nombre_Pais,
        IdEstado       = p.idEstado,
        IdFabricante   = p.idFabricante,
        IdCategoria    = p.idCategoria,
        IdPais         = p.idPais,
        IdPresentacion = p.idPresentacion,
    };
}
