using CapaDatos.Modelados;
using CapaDatos.Modelados.Productos;
using ServicioConexión.Conexion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Op = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositorios.productos_movimientos
{
    /// <summary>
    /// Resultado paginado que incluye los items de la página y el conteo total.
    /// </summary>
    public class PaginaProductos
    {
        public List<Productos> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActivosCount { get; set; }
        public int InactivosCount { get; set; }
    }

    public class RepositorioProducto
    {
        private const string SelectJoin =
            "*, presentacion_producto(*), fabricante(*), categoria(*), paises(*)";

        public static async Task<List<Productos>> ObtenerTodosLosProductos()
        {
            try
            {
                    var client = await ConexionSupabase.GetClientAsync();
                    var resultado = await client
                                                .From<Productos>()
                                                .Get();
                return resultado?.Models ?? new List<Productos>();
            }
            catch (Exception ex)
            {

                Serilog.Log.Error(ex, "Error al obtener productos");
                throw;
            }
        }

        public static async Task<List<Productos>> obtenerProductosJoin()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                                            .From<Productos>()
                                            .Select(SelectJoin)
                                            .Order("id_producto", Ord.Ascending)
                                            .Get();
                return resultado?.Models ?? new List<Productos>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener productos");
                throw;
            }
        }

        /// <summary>
        /// Obtiene una página de productos con filtros aplicados en Supabase.
        /// </summary>
        public static async Task<PaginaProductos> ObtenerPaginaAsync(
            int page, int pageSize,
            int? idEstado = null,
            int? idFabricante = null,
            int? idPais = null,
            string? busqueda = null)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();

                // ── Query para los items de la página ──
                var query = client.From<Productos>().Select(SelectJoin);

                if (idEstado.HasValue)
                    query = query.Filter("id_estado", Op.Equals, idEstado.Value.ToString());
                if (idFabricante.HasValue)
                    query = query.Filter("id_fabricante", Op.Equals, idFabricante.Value.ToString());
                if (idPais.HasValue)
                    query = query.Filter("id_pais", Op.Equals, idPais.Value.ToString());
                if (!string.IsNullOrWhiteSpace(busqueda))
                    query = query.Filter("nombre_producto", Op.ILike, $"%{busqueda}%");

                query = query.Order("id_producto", Ord.Ascending);

                int from = (page - 1) * pageSize;
                int to = from + pageSize - 1;
                query = query.Range(from, to);

                var resultado = await query.Get();
                var items = resultado?.Models ?? new List<Productos>();

                // ── Conteos server-side ──
                var conteos = await ObtenerConteosAsync(idFabricante, idPais, busqueda);

                return new PaginaProductos
                {
                    Items = items,
                    TotalCount = conteos.total,
                    ActivosCount = conteos.activos,
                    InactivosCount = conteos.inactivos
                };
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener página de productos");
                throw;
            }
        }

        /// <summary>
        /// Busca productos por código o nombre (máx 10 resultados) para sugerencias.
        /// </summary>
        public static async Task<List<Productos>> BuscarSugerenciasAsync(
            string termino,
            int? idEstado = null,
            int? idFabricante = null,
            int? idPais = null,
            int limite = 10)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var query = client.From<Productos>().Select(SelectJoin);

                if (idEstado.HasValue)
                    query = query.Filter("id_estado", Op.Equals, idEstado.Value.ToString());
                if (idFabricante.HasValue)
                    query = query.Filter("id_fabricante", Op.Equals, idFabricante.Value.ToString());
                if (idPais.HasValue)
                    query = query.Filter("id_pais", Op.Equals, idPais.Value.ToString());

                // Buscar por nombre O código
                query = query.Filter("nombre_producto", Op.ILike, $"%{termino}%");

                query = query.Order("nombre_producto", Ord.Ascending)
                             .Limit(limite);

                var resultado = await query.Get();
                return resultado?.Models ?? new List<Productos>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al buscar sugerencias");
                throw;
            }
        }

        /// <summary>
        /// Obtiene las listas de fabricantes y países disponibles para los filtros.
        /// </summary>
        public static async Task<(List<(int id, string nombre)> fabricantes, List<(int id, string nombre)> paises)> ObtenerFiltrosDisponiblesAsync()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client.From<Productos>()
                    .Select("id_fabricante, fabricante(nombre_fabricante), id_pais, paises(nombre_pais)")
                    .Order("id_producto", Ord.Ascending)
                    .Get();

                var todos = resultado?.Models ?? new List<Productos>();
                // idFabricante/idPais son nullable (la columna lo es); acá solo
                // interesan los productos que sí tienen el catálogo cargado.
                var fabricantes = todos
                    .Where(p => p.Fabricante != null && p.idFabricante.HasValue)
                    .Select(p => (id: p.idFabricante!.Value, nombre: p.Fabricante!.nombreFabricante))
                    .DistinctBy(x => x.id)
                    .OrderBy(x => x.nombre)
                    .ToList();
                var paises = todos
                    .Where(p => p.Paises != null && p.idPais.HasValue)
                    .Select(p => (id: p.idPais!.Value, nombre: p.Paises!.nombrePais))
                    .DistinctBy(x => x.id)
                    .OrderBy(x => x.nombre)
                    .ToList();

                return (fabricantes, paises);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener filtros");
                throw;
            }
        }

        private static async Task<(int total, int activos, int inactivos)> ObtenerConteosAsync(
            int? idFabricante, int? idPais, string? busqueda)
        {
            var client = await ConexionSupabase.GetClientAsync();

            var baseQuery = client.From<Productos>().Select("id_producto, id_estado");
            if (idFabricante.HasValue)
                baseQuery = baseQuery.Filter("id_fabricante", Op.Equals, idFabricante.Value.ToString());
            if (idPais.HasValue)
                baseQuery = baseQuery.Filter("id_pais", Op.Equals, idPais.Value.ToString());
            if (!string.IsNullOrWhiteSpace(busqueda))
                baseQuery = baseQuery.Filter("nombre_producto", Op.ILike, $"%{busqueda}%");

            var resultado = await baseQuery.Get();
            var models = resultado?.Models ?? new List<Productos>();

            int total = models.Count;
            int activos = models.Count(p => p.idEstado == 1);
            return (total, activos, total - activos);
        }

        public static async Task<ProductosInsertar> ingresarProducto(ProductosInsertar datos)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var response = await client .From<ProductosInsertar>()
                                            .Insert(datos);
                return response.Model ?? null;

            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al ingresar producto");
                throw;
            }
        }

    }
}
