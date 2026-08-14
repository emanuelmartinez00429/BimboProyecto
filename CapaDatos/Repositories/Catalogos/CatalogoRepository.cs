using CapaAplicacion.Common;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Conexion;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;
using CapaDatos.Modelados.Pesajes;
using CapaDatos.Modelados.Productos;
using ServicioConexión.Conexion;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using Count = Supabase.Postgrest.Constants.CountType;
using Op    = Supabase.Postgrest.Constants.Operator;
using Ord   = Supabase.Postgrest.Constants.Ordering;
// Desambigua contra el namespace CapaDatos.Repositories.Proveedores.
using ProveedorModel = CapaDatos.Modelados.Pesajes.Proveedores;

namespace CapaDatos.Repositories.Catalogos;

/// <summary>
/// Lectura uniforme de los catálogos chicos. Toda la mecánica (búsqueda, rango,
/// conteo) vive una sola vez en <see cref="PagedInternalAsync{T}"/>; cada catálogo
/// solo declara sus columnas y cómo proyectarse a <see cref="FiltroItem"/>.
/// </summary>
public class CatalogoRepository : RepositorioBase, ICatalogoRepository
{
    public CatalogoRepository(IConexionMonitor conexion) : base(conexion) { }

    // ── Catálogos ─────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<FiltroItem>>> GetPresentacionesAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        TryAsync(() => PagedInternalAsync<PresentacionConsulta>(
            q => q.Filter("id_estado", Op.Equals, EstadoRegistro.Activo.ToString()),
            ["nombre_presentacion", "descripcion_presentacion"],
            "nombre_presentacion",
            termino, page, size,
            p => new FiltroItem
            {
                Id          = p.idPresentacion,
                Nombre      = p.nombrePresentacion,
                Descripcion = p.descripcionPresentacion ?? string.Empty,
                Activo      = p.idEstado == EstadoRegistro.Activo,
            },
            ct), "Cargar presentaciones");

    /// <summary>tara no tiene columna de estado; su etiqueta es la descripción.</summary>
    public Task<Result<PagedResult<FiltroItem>>> GetTarasAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        TryAsync(() => PagedInternalAsync<Tara>(
            null,
            ["descripcion_tara"],
            "descripcion_tara",
            termino, page, size,
            t => new FiltroItem
            {
                Id          = t.idTara,
                Nombre      = string.IsNullOrWhiteSpace(t.descripcionTara) ? $"Tara {t.idTara}" : t.descripcionTara.Trim(),
                Descripcion = $"{t.pesoTaraEnvalaje:N2} kg",
            },
            ct), "Cargar taras");

    /// <summary>categoria usa estado_categoria (bool), no id_estado.</summary>
    public Task<Result<PagedResult<FiltroItem>>> GetCategoriasAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        TryAsync(() => PagedInternalAsync<Categoria>(
            q => q.Filter("estado_categoria", Op.Equals, "true"),
            ["nombre_categoria", "descripcion_categoria"],
            "nombre_categoria",
            termino, page, size,
            c => new FiltroItem
            {
                Id          = c.idCategoria,
                Nombre      = c.nombreCategoria,
                Descripcion = c.descripcionCategoria ?? string.Empty,
                Activo      = c.estadoCategoria,
            },
            ct), "Cargar categorías");

    public Task<Result<PagedResult<FiltroItem>>> GetUnidadesAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        TryAsync(() => PagedInternalAsync<UnidadMedida>(
            null,
            ["nombre_unidad", "abreviatura"],
            "nombre_unidad",
            termino, page, size,
            u => new FiltroItem
            {
                Id          = u.idUnidad,
                Nombre      = u.nombreUnidad,
                Descripcion = u.abreviatura ?? string.Empty,
            },
            ct), "Cargar unidades");

    public Task<Result<PagedResult<FiltroItem>>> GetPaisesAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        TryAsync(() => PagedInternalAsync<Paises>(
            null,
            ["nombre_pais", "region"],
            "nombre_pais",
            termino, page, size,
            p => new FiltroItem
            {
                Id          = p.idPais,
                Nombre      = p.nombrePais,
                Descripcion = p.region ?? string.Empty,
            },
            ct), "Cargar países");

    public Task<Result<PagedResult<FiltroItem>>> GetProveedoresAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        TryAsync(() => PagedInternalAsync<ProveedorModel>(
            q => q.Filter("id_estado", Op.Equals, EstadoRegistro.Activo.ToString()),
            ["nombre_proveedor"],
            "nombre_proveedor",
            termino, page, size,
            p => new FiltroItem
            {
                Id     = p.idProveedor,
                Nombre = p.nombreProveedor,
                Activo = p.idEstado == EstadoRegistro.Activo,
            },
            ct), "Cargar proveedores");

    public Task<Result<PagedResult<FiltroItem>>> GetFabricantesAsync(
        string termino, int page, int size, int? idProveedor = null, CancellationToken ct = default) =>
        TryAsync(() => PagedInternalAsync<FabricanteConsulta>(
            q =>
            {
                q = q.Filter("id_estado", Op.Equals, EstadoRegistro.Activo.ToString());
                if (idProveedor.HasValue)
                    q = q.Filter("id_proveedor", Op.Equals, idProveedor.Value.ToString());
                return q;
            },
            ["nombre_fabricante", "descripcion_fabricante"],
            "nombre_fabricante",
            termino, page, size,
            f => new FiltroItem
            {
                Id          = f.idFabricante,
                Nombre      = f.nombreFabricante,
                Descripcion = f.descripcionFabricante ?? string.Empty,
                Activo      = f.idEstado == EstadoRegistro.Activo,
                IdPadre     = f.idProveedor,
            },
            ct), "Cargar fabricantes");

    // ── Motor común ───────────────────────────────────────────────────────────

    /// <summary>
    /// Paginación + búsqueda genérica sobre cualquier catálogo.
    ///
    /// Conteo barato: pide <c>size + 1</c> filas como sonda. Si vuelven menos que
    /// el tope, ya se tiene todo y el total es exacto sin una segunda consulta —
    /// que es el caso de todos los catálogos actuales (1 a 27 filas). Solo cuando
    /// hay más se paga el <c>Count</c>, y ahí el total sí hace falta para pintar
    /// los botones de página.
    /// </summary>
    private static async Task<PagedResult<FiltroItem>> PagedInternalAsync<T>(
        Func<IPostgrestTable<T>, IPostgrestTable<T>>? aplicarFiltros,
        string[] columnasBusqueda,
        string columnaOrden,
        string termino,
        int page,
        int size,
        Func<T, FiltroItem> map,
        CancellationToken ct) where T : BaseModel, new()
    {
        var client = await ConexionSupabase.GetClientAsync();

        IPostgrestTable<T> Construir()
        {
            IPostgrestTable<T> q = client.From<T>();
            if (aplicarFiltros is not null) q = aplicarFiltros(q);

            if (!string.IsNullOrWhiteSpace(termino))
            {
                var patron = $"%{termino.Trim()}%";
                q = q.Or(columnasBusqueda
                    .Select(c => (IPostgrestQueryFilter)new QueryFilter(c, Op.ILike, patron))
                    .ToList());
            }
            return q;
        }

        int from = (page - 1) * size;
        int to   = from + size;   // +1 fila de sonda

        var respuesta = await Construir()
            .Order(columnaOrden, Ord.Ascending)
            .Range(from, to)
            .Get(ct);

        var filas  = respuesta?.Models ?? [];
        bool hayMas = filas.Count > size;

        int total = hayMas
            ? await Construir().Count(Count.Exact, ct)
            : from + filas.Count;

        return new PagedResult<FiltroItem>
        {
            Items = filas.Take(size).Select(map).ToList(),
            Total = total,
        };
    }
}
