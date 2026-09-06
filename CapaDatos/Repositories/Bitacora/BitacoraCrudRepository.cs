using CapaAplicacion.Bitacora.Dtos;
using CapaAplicacion.Bitacora.Interfaces;
using CapaAplicacion.Bitacora.Queries;
using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;
using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
// Alias obligatorios: los namespaces hermanos CapaDatos.Repositories.Usuarios y
// .Bitacora ganan la resolución de nombre sobre el using de Modelados (CS0118),
// igual que hace UsuarioRepository.cs con UsuariosModel.
using BitacoraModel = CapaDatos.Modelados.Usuarios.Bitacora;
using UsuariosModel = CapaDatos.Modelados.Usuarios.Usuarios;
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Usuarios.Bitacora>;

namespace CapaDatos.Repositories.Bitacora;

public class BitacoraCrudRepository : RepositorioBase, IBitacoraRepository
{
    public BitacoraCrudRepository(IConexionMonitor conexion) : base(conexion) { }

    private const string FechaFormato = "yyyy-MM-ddTHH:mm:ss";

    private static BitacoraDto Map(BitacoraModel b) => new()
    {
        IdBitacora         = b.idBitacora,
        FechaHora          = b.fechaHora,
        AliasUsuario       = b.usuarios?.aliasUsuario ?? string.Empty,
        NombreModulo       = b.modulos?.nombreModulo  ?? string.Empty,
        NombreAccion       = b.acciones?.nombreAccion ?? string.Empty,
        CampoAfectado      = b.campoAfectado,
        EstadoAnterior     = b.estadoAnterior,
        EstadoActual       = b.estadoActual,
        CampoExtra         = b.campoExtra,
        TablaAfectada      = b.tablaAfectada,
        IdRegistroAfectado = b.idRegistroAfectado,
    };

    // ── Lectura ─────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<BitacoraDto>>> GetPagedAsync(
        int page, int size, BitacoraFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar bitácora");

    public Task<Result<IReadOnlyList<BitacoraDto>>> BuscarSugerenciasAsync(
        string termino, BitacoraFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => BuscarSugerenciasInternal(termino, filtros), "Buscar sugerencias bitácora");

    public Task<Result<int>> GetPaginaDeRegistroAsync(
        int id, int size, BitacoraFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeRegistroInternal(id, size, filtros), "Calcular página de registro de bitácora");

    public Task<Result<IReadOnlyList<FiltroItem>>> ObtenerModulosAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var resultado = await client.From<Modulo>().Order("nombre_modulo", Ord.Ascending).Get();
            return (IReadOnlyList<FiltroItem>)(resultado?.Models
                .Select(m => new FiltroItem { Id = m.idModulo, Nombre = m.nombreModulo })
                .ToList() ?? []);
        }, "Obtener módulos");

    public Task<Result<IReadOnlyList<FiltroItem>>> ObtenerAccionesAsync(int? idModulo, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var query  = client.From<Accion>().Select("*");
            if (idModulo.HasValue)
                query = query.Filter("id_modulo", Op.Equals, idModulo.Value.ToString());

            var resultado = await query.Order("nombre_accion", Ord.Ascending).Get();
            return (IReadOnlyList<FiltroItem>)(resultado?.Models
                .Select(a => new FiltroItem { Id = a.idAccion, Nombre = a.nombreAccion })
                .ToList() ?? []);
        }, "Obtener acciones");

    public Task<Result<IReadOnlyList<FiltroItem>>> ObtenerUsuariosAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var resultado = await client
                .From<UsuariosModel>()
                .Select("id_usuario, alias_usuario")
                .Order("alias_usuario", Ord.Ascending)
                .Get();
            return (IReadOnlyList<FiltroItem>)(resultado?.Models
                .Select(u => new FiltroItem { Id = u.idUsuario, Nombre = u.aliasUsuario })
                .ToList() ?? []);
        }, "Obtener usuarios");

    // ── Lógica interna ──────────────────────────────────────────────────────

    private async Task<PagedResult<BitacoraDto>> GetPagedInternal(int page, int size, BitacoraFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(
            client.From<BitacoraModel>().Select("*, usuarios(*), acciones(*), modulos(*)"),
            filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        var pageTask    = query.Order("fecha_hora", Ord.Descending).Range(from, to).Get();
        var conteoTask  = GetConteoAsync(client, filtros);
        await Task.WhenAll(pageTask, conteoTask);

        var items = pageTask.Result?.Models.Select(Map).ToList() ?? [];

        return new PagedResult<BitacoraDto>
        {
            Items     = items,
            Total     = conteoTask.Result,
            Activos   = 0,
            Inactivos = 0,
        };
    }

    private async Task<IReadOnlyList<BitacoraDto>> BuscarSugerenciasInternal(string termino, BitacoraFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(
            client.From<BitacoraModel>().Select("*, usuarios(*), acciones(*), modulos(*)"),
            filtros);

        var aguja = TextoBusqueda.Normalizar(termino);
        var resultado = await query
            .Filter("busqueda_bitacora", Op.ILike, $"%{aguja}%")
            .Order("fecha_hora", Ord.Descending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<int> GetPaginaDeRegistroInternal(int id, int size, BitacoraFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();

        var objetivoRes = await client
            .From<BitacoraModel>()
            .Select("fecha_hora")
            .Filter("id_bitacora", Op.Equals, id.ToString())
            .Get();
        var fechaObjetivo = objetivoRes?.Models.FirstOrDefault()?.fechaHora;
        if (fechaObjetivo is null) return 1;

        // Orden es DESC por fecha_hora: los registros "anteriores" en la página
        // son los que tienen fecha_hora MAYOR (más recientes). Sin desempate por
        // ID — timestamptz tiene resolución de microsegundos, riesgo real ~nulo.
        var query = AplicarFiltros(
            client.From<BitacoraModel>()
                  .Select("id_bitacora")
                  .Filter("fecha_hora", Op.GreaterThan, fechaObjetivo.Value.ToString(FechaFormato)),
            filtros);

        var result  = await query.Get();
        int previos = result?.Models.Count ?? 0;
        return (previos / size) + 1;
    }

    private static Table AplicarFiltros(Table query, BitacoraFiltros filtros)
    {
        if (filtros.IdUsuario.HasValue)
            query = query.Filter("id_usuario", Op.Equals, filtros.IdUsuario.Value.ToString());
        if (filtros.IdModulo.HasValue)
            query = query.Filter("id_modulo", Op.Equals, filtros.IdModulo.Value.ToString());
        if (filtros.IdAccion.HasValue)
            query = query.Filter("id_accion", Op.Equals, filtros.IdAccion.Value.ToString());
        if (filtros.FechaDesde.HasValue)
            query = query.Filter("fecha_hora", Op.GreaterThanOrEqual,
                filtros.FechaDesde.Value.Date.ToString(FechaFormato));
        if (filtros.FechaHasta.HasValue)
            query = query.Filter("fecha_hora", Op.LessThanOrEqual,
                filtros.FechaHasta.Value.Date.AddDays(1).AddSeconds(-1).ToString(FechaFormato));
        return query;
    }

    private static async Task<int> GetConteoAsync(Supabase.Client client, BitacoraFiltros filtros)
    {
        var query = AplicarFiltros(
            client.From<BitacoraModel>().Select("id_bitacora"),
            filtros);
        var resultado = await query.Get();
        return resultado?.Models.Count ?? 0;
    }
}
