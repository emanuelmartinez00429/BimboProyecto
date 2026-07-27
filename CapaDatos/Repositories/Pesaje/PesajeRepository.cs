using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Pesaje.Dtos;
using CapaAplicacion.Pesaje.Interfaces;
using CapaDatos.Modelados.Pesajes;
using CapaDatos.Repositories;
using ServicioConexión.Conexion;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Pesaje;

/// <summary>
/// Persistencia real del módulo de Recepción de Materia Prima (Fase 2).
/// Reutiliza los modelos de <c>CapaDatos.Modelados.Pesajes</c>, las constantes
/// <see cref="EstadosPesaje"/> y el trigger de BD (neto/tara). Patrón RepositorioBase + Result.
/// </summary>
public class PesajeRepository : RepositorioBase, IPesajeRepository
{
    public PesajeRepository(IConexionMonitor conexion) : base(conexion) { }

    // ══════════════════════════════════════════════════════════════════════
    //  Camiones
    // ══════════════════════════════════════════════════════════════════════
    public Task<Result<IReadOnlyList<CamionDto>>> GetCamionesActivosAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var res = await client.From<Movimiento>()
                .Select("*, proveedores(*)")
                .Filter("id_estado", Op.In, new List<object> { EstadosPesaje.Abierto, EstadosPesaje.Cerrado })
                .Order("fecha_asignacion", Ord.Descending)
                .Get();

            IReadOnlyList<CamionDto> lista = (res?.Models ?? new()).Select(m => new CamionDto
            {
                Id              = m.idMovimiento,
                IdProveedor     = m.idProveedor,
                Placa           = m.placaVehiculo ?? "",
                Proveedor       = m.nombreProveedor,
                FechaAsignacion = m.fechaAsignacion.ToString("dd/MM/yyyy"),
                Observaciones   = m.observaciones ?? "",
                Cerrado         = m.idEstado == EstadosPesaje.Cerrado,
                TaraExtraTotal  = (double)m.pesoTaraExtra,
            }).ToList();
            return lista;
        }, "Cargar camiones");

    public Task<Result<int>> CrearCamionAsync(int idProveedor, string placa, string observaciones, double taraExtraTotal, int idUsuario, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var nuevo = new Movimiento
            {
                idProveedor     = idProveedor,
                placaVehiculo   = placa,
                fechaAsignacion = DateOnly.FromDateTime(DateTime.Now),
                idUsuario       = idUsuario,
                idEstado        = EstadosPesaje.Abierto,
                observaciones   = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones,
                pesoTaraExtra   = (decimal)taraExtraTotal,
            };
            var r = await client.From<Movimiento>().Insert(nuevo);
            return r.Models.First().idMovimiento;
        }, "Registrar camión");

    public Task<Result> ActualizarCamionAsync(int idMovimiento, int idProveedor, string placa, string observaciones, double taraExtraTotal, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Movimiento>()
                .Where(m => m.idMovimiento == idMovimiento)
                .Set(m => m.idProveedor,   idProveedor)
                .Set(m => m.placaVehiculo, placa)
                .Set(m => m.observaciones, string.IsNullOrWhiteSpace(observaciones) ? null : observaciones)
                .Set(m => m.pesoTaraExtra, (decimal)taraExtraTotal)
                .Update();
        }, "Actualizar camión");

    public Task<Result> CerrarCamionAsync(int idMovimiento, CancellationToken ct = default) =>
        SetEstadoMovimiento(idMovimiento, EstadosPesaje.Cerrado, "Cerrar camión");

    public Task<Result> AnularCamionAsync(int idMovimiento, CancellationToken ct = default) =>
        SetEstadoMovimiento(idMovimiento, EstadosPesaje.Anulado, "Anular camión");

    private Task<Result> SetEstadoMovimiento(int idMovimiento, int estado, string ctx) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Movimiento>()
                .Where(m => m.idMovimiento == idMovimiento)
                .Set(m => m.idEstado, estado)
                .Update();
        }, ctx);

    // ══════════════════════════════════════════════════════════════════════
    //  Productos del camión
    // ══════════════════════════════════════════════════════════════════════
    public Task<Result<IReadOnlyList<MovProductoDto>>> GetProductosAsync(int idMovimiento, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();

            // 1) mov_productos activos (no anulados) + datos del producto
            var mpRes = await client.From<MovimientoProducto>()
                .Select("*, productos(*)")
                .Filter("id_movimiento", Op.Equals, idMovimiento)
                .Filter("id_estado", Op.In, new List<object> { EstadosPesaje.Abierto, EstadosPesaje.Cerrado })
                .Order("id_mov_producto", Ord.Ascending)
                .Get();
            var movProds = mpRes?.Models ?? new();
            if (movProds.Count == 0) return (IReadOnlyList<MovProductoDto>)new List<MovProductoDto>();

            var idProductos    = movProds.Select(m => (object)m.idProducto).Distinct().ToList();
            var idMovProductos = movProds.Select(m => (object)m.idMovProducto).ToList();

            // 2) peso teórico + tara por producto (previsualización del modal
            //    y cálculo de bultos teóricos)
            var taraRes = await client.From<ProductoTaraConsulta>()
                .Select("id_producto, peso_teorico, tara(*)")
                .Filter("id_producto", Op.In, idProductos)
                .Get();
            var datosPorProducto = (taraRes?.Models ?? new())
                .GroupBy(p => p.idProducto)
                .ToDictionary(
                    g => g.Key,
                    g => ((double)g.First().taraUnitaria, (double)(g.First().pesoTeorico ?? 0m)));

            // 3) entradas activas de esos productos
            var entRes = await client.From<EntradaProducto>()
                .Filter("id_mov_producto", Op.In, idMovProductos)
                .Filter("id_estado", Op.NotEqual, EstadosPesaje.Anulado.ToString())
                .Order("fecha_entrada", Ord.Ascending)
                .Order("hora_entrada", Ord.Ascending)
                .Get();
            var entradasPorMp = (entRes?.Models ?? new())
                .GroupBy(e => e.idMovProducto)
                .ToDictionary(g => g.Key, g => g.Select(MapEntrada).ToList());

            IReadOnlyList<MovProductoDto> lista = movProds.Select(m =>
            {
                datosPorProducto.TryGetValue(m.idProducto, out var d);
                return new MovProductoDto
                {
                    Id               = m.idMovProducto,
                    IdProducto       = m.idProducto,
                    Codigo           = m.codigoProducto,
                    Nombre           = m.nombreProducto,
                    TaraUnitaria     = d.Item1,
                    PesoTeorico      = d.Item2,
                    PesoManifestado  = (double)m.pesoManifestado,
                    BultosDeclarados = m.bultosTeóricos,
                    Observaciones    = m.observaciones ?? "",
                    Cerrado          = m.idEstado == EstadosPesaje.Cerrado,
                    Entradas         = entradasPorMp.TryGetValue(m.idMovProducto, out var es)
                                         ? es : new List<EntradaDto>(),
                };
            }).ToList();
            return lista;
        }, "Cargar productos del camión");

    public Task<Result<int>> AgregarProductoAsync(int idMovimiento, int idProducto, double pesoManifestado, int bultosDeclarados, string observaciones, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var nuevo = new MovimientoProducto
            {
                idMovimiento    = idMovimiento,
                idProducto      = idProducto,
                pesoManifestado = (decimal)pesoManifestado,
                bultosTeóricos  = bultosDeclarados,   // columna BD conserva el nombre viejo
                idEstado        = EstadosPesaje.Abierto,
                observaciones   = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones,
            };
            var r = await client.From<MovimientoProducto>().Insert(nuevo);
            return r.Models.First().idMovProducto;
        }, "Agregar producto al camión");

    public Task<Result> ActualizarProductoAsync(int idMovProducto, double pesoManifestado, int bultosDeclarados, string observaciones, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<MovimientoProducto>()
                .Where(mp => mp.idMovProducto == idMovProducto)
                .Set(mp => mp.pesoManifestado, (decimal)pesoManifestado)
                .Set(mp => mp.bultosTeóricos,  bultosDeclarados)
                .Set(mp => mp.observaciones,   string.IsNullOrWhiteSpace(observaciones) ? null : observaciones)
                .Update();
        }, "Actualizar producto");

    public Task<Result> AnularProductoAsync(int idMovProducto, CancellationToken ct = default) =>
        SetEstadoProductoInterno(idMovProducto, EstadosPesaje.Anulado, "Anular producto");

    public Task<Result> SetEstadoProductoAsync(int idMovProducto, bool cerrado, CancellationToken ct = default) =>
        SetEstadoProductoInterno(idMovProducto, cerrado ? EstadosPesaje.Cerrado : EstadosPesaje.Abierto, "Cambiar estado producto");

    private Task<Result> SetEstadoProductoInterno(int idMovProducto, int estado, string ctx) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<MovimientoProducto>()
                .Where(mp => mp.idMovProducto == idMovProducto)
                .Set(mp => mp.idEstado, estado)
                .Update();
        }, ctx);

    // ══════════════════════════════════════════════════════════════════════
    //  Pesajes (entradas)
    // ══════════════════════════════════════════════════════════════════════
    public Task<Result<int>> CrearEntradaAsync(int idMovProducto, int idProducto, double bruto, double taraExtra, int bultos, string observaciones, int idUsuario, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var ahora = DateTime.Now;
            var nueva = new EntradaProducto
            {
                idMovProducto        = idMovProducto,
                idProducto           = idProducto,
                pesoBruto            = (decimal)bruto,
                pesoTaraExtra        = (decimal)taraExtra,
                numeroBultosRecibido = bultos,
                fechaEntrada         = DateOnly.FromDateTime(ahora),
                horaEntrada          = TimeOnly.FromDateTime(ahora),
                idUsuario            = idUsuario,
                idEstado             = EstadosPesaje.Activo,
                observaciones        = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones,
            };
            var r = await client.From<EntradaProducto>().Insert(nueva);
            return r.Models.First().idPesaje;
        }, "Registrar pesaje");

    public Task<Result> AnularEntradaAsync(int idPesaje, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<EntradaProducto>()
                .Where(e => e.idPesaje == idPesaje)
                .Set(e => e.idEstado, EstadosPesaje.Anulado)
                .Update();
        }, "Anular pesaje");

    // ── Mapeo ────────────────────────────────────────────────────────────────
    private static EntradaDto MapEntrada(EntradaProducto e) => new()
    {
        Id            = e.idPesaje,
        Bruto         = (double)e.pesoBruto,
        TaraInd       = (double)e.pesoTaraIndividual,
        TaraExtra     = (double)e.pesoTaraExtra,
        TaraTotal     = (double)e.pesoTaraTotal,
        Neto          = (double)e.pesoNeto,
        Bultos        = e.numeroBultosRecibido ?? 0,
        Fecha         = e.fechaEntrada.ToString("dd/MM/yyyy"),
        Hora          = e.horaEntrada.ToString("hh:mm tt"),
        Observaciones = e.observaciones ?? "",
    };
}
