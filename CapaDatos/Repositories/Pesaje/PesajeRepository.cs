using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Pesaje.Dtos;
using CapaAplicacion.Pesaje.Interfaces;
using CapaDatos.Modelados.Pesajes;
using CapaDatos.Repositories;
using Newtonsoft.Json.Linq;
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
            // Solo camiones ABIERTOS: los cerrados salen de la pantalla hasta que exista la
            // sección de históricos. El reporte que se abre al cerrar usa el objeto en memoria,
            // así que sigue funcionando aunque el camión ya no vuelva en esta consulta.
            var res = await client.From<Movimiento>()
                .Select("*, proveedores(*)")
                .Filter("id_estado", Op.Equals, EstadosPesaje.Abierto.ToString())
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
                TaraExtraLegado = (double)m.pesoTaraExtra,
            }).ToList();
            return lista;
        }, "Cargar camiones");

    public Task<Result<int>> CrearCamionAsync(int idProveedor, string placa, string observaciones, int idUsuario, CancellationToken ct = default)
    {
        // Se genera una sola vez por intención del usuario y fuera del delegado que ejecuta
        // la llamada. Si posteriormente se agrega una política de reintentos, debe reutilizarse.
        var idSolicitud = Guid.NewGuid();

        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_proveedor"]    = idProveedor,
                ["p_placa_vehiculo"] = placa,
                ["p_observaciones"]  = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones,
                ["p_id_solicitud"]   = idSolicitud,
            };

            // La firma se conserva por compatibilidad; la RPC obtiene el usuario desde auth.uid().
            _ = idUsuario;
            var response = await client.Rpc("ingresar_movimiento_pesaje_tabla_bitacora", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerResultadoRpc(response?.Content, "registrar la recepción")
                ["id_movimiento"]?.Value<int>()
                ?? throw new InvalidOperationException("La RPC no devolvió id_movimiento.");
        }, "Registrar camión");
    }

    /// <summary>
    /// Registra de forma atómica (en una sola transacción en el servidor) un lote de camiones.
    /// Si cualquier fila falla, la transacción se aborta completamente y ningún camión es persistido.
    /// </summary>
    public Task<Result<ResultadoAltaLoteCamiones>> RegistrarCamionesLoteAsync(
        IReadOnlyList<(string Placa, int IdProveedor, string? Observaciones)> camiones,
        Guid idSolicitud,
        CancellationToken ct = default)
    {
        if (camiones == null || camiones.Count == 0)
            return Task.FromResult(Result<ResultadoAltaLoteCamiones>.Ok(new ResultadoAltaLoteCamiones(0, 0, Array.Empty<int>())));

        var solicitudEfectiva = idSolicitud == Guid.Empty ? Guid.NewGuid() : idSolicitud;

        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();

            var loteJson = camiones.Select(c => new Dictionary<string, object?>
            {
                ["id_proveedor"]   = c.IdProveedor,
                ["placa"]          = c.Placa?.Trim().ToUpperInvariant(),
                ["placa_vehiculo"] = c.Placa?.Trim().ToUpperInvariant(),
                ["observaciones"]  = string.IsNullOrWhiteSpace(c.Observaciones) ? null : c.Observaciones.Trim(),
            }).ToArray();

            var parametros = new Dictionary<string, object?>
            {
                ["p_camiones"]     = loteJson,
                ["p_id_solicitud"] = solicitudEfectiva,
            };

            var response = await client.Rpc("registrar_camiones_lote_seguro", parametros);
            ct.ThrowIfCancellationRequested();

            var res = ObtenerResultadoRpc(response?.Content, "registrar el lote de camiones");
            int creados = res["creados"]?.Value<int>() ?? 0;

            var idsTokens = res["ids_movimiento"] as JArray
                            ?? res["ids"] as JArray
                            ?? res["ids_movimientos"] as JArray;

            IReadOnlyList<int> ids = idsTokens != null
                ? idsTokens.Select(t => t.Value<int>()).ToList()
                : Array.Empty<int>();

            int primerId = res["primer_id"]?.Value<int>()
                           ?? res["primer_id_movimiento"]?.Value<int>()
                           ?? (ids.Count > 0 ? ids[0] : 0);

            if (creados == 0 && ids.Count > 0)
                creados = ids.Count;

            return new ResultadoAltaLoteCamiones(creados, primerId, ids);
        }, "Registrar camiones en lote");
    }

    /// <summary>
    /// NO toca <c>peso_tara_extra</c> a propósito: es una columna del flujo anterior y editar un
    /// camión legado le borraría su tara histórica. La tara extra vigente se guarda por entrada.
    /// </summary>
    public Task<Result> ActualizarCamionAsync(int idMovimiento, int idProveedor, string placa, string observaciones, CancellationToken ct = default)
    {
        var idSolicitud = Guid.NewGuid();

        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.Rpc("actualizar_movimiento_pesaje_tabla_bitacora",
                new Dictionary<string, object?>
                {
                    ["p_id_movimiento"]   = idMovimiento,
                    ["p_id_proveedor"]    = idProveedor,
                    ["p_placa_vehiculo"] = placa,
                    ["p_observaciones"]  = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones,
                    ["p_id_solicitud"]   = idSolicitud,
                });
            ct.ThrowIfCancellationRequested();
            ValidarIdResultado(response?.Content, "id_movimiento", idMovimiento, "actualizar la recepción");
        }, "Actualizar camión");
    }

    public Task<Result> CerrarCamionAsync(int idMovimiento, CancellationToken ct = default) =>
        SetEstadoMovimiento(idMovimiento, EstadosPesaje.Cerrado, "Cerrar camión", ct);

    public Task<Result> AnularCamionAsync(int idMovimiento, CancellationToken ct = default) =>
        SetEstadoMovimiento(idMovimiento, EstadosPesaje.Anulado, "Anular camión", ct);

    private Task<Result> SetEstadoMovimiento(int idMovimiento, int estado, string ctx, CancellationToken ct)
    {
        var idSolicitud = Guid.NewGuid();

        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.Rpc("cambiar_estado_movimiento_pesaje_tabla_bitacora",
                new Dictionary<string, object?>
                {
                    ["p_id_movimiento"] = idMovimiento,
                    ["p_id_estado"]     = estado,
                    ["p_id_solicitud"] = idSolicitud,
                });
            ct.ThrowIfCancellationRequested();
            ValidarIdResultado(response?.Content, "id_movimiento", idMovimiento, ctx.ToLowerInvariant());
        }, ctx);
    }

    private static JObject ObtenerResultadoRpc(string? json, string operacion)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"La RPC no devolvió resultado al {operacion}.");

        var token = JToken.Parse(json);
        if (token.Type == JTokenType.String)
            token = JToken.Parse(token.Value<string>() ?? "{}");

        return token as JObject
            ?? throw new InvalidOperationException($"La RPC devolvió un resultado inválido al {operacion}.");
    }

    private static void ValidarIdResultado(string? json, string propiedad, int esperado, string operacion)
    {
        var recibido = ObtenerResultadoRpc(json, operacion)[propiedad]?.Value<int>();
        if (recibido != esperado)
            throw new InvalidOperationException($"La RPC no confirmó el registro esperado al {operacion}.");
    }

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
                .Where(e => e.idMovProducto.HasValue)
                .GroupBy(e => e.idMovProducto!.Value)
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

    // AgregarProductoAsync, ActualizarProductoAsync y AnularProductoAsync se
    // eliminaron: eran Insert/Update crudos de PostgREST —sin RPC, sin bitácora,
    // sin p_id_solicitud y sin validación en servidor—, el último camino de
    // escritura del módulo en esas condiciones. Los reemplaza por completo
    // GuardarProductosLoteAsync, que hace altas, cambios y bajas en una sola
    // transacción auditada.

    /// <summary>
    /// Toda la carga en un solo viaje y una sola transacción — ver el contrato en
    /// <see cref="IPesajeRepository.GuardarProductosLoteAsync"/>.
    /// <para/>
    /// Las bajas viajan como arreglo plano de <c>id_mov_producto</c>; altas y cambios
    /// como arreglos de objetos. El RPC valida todo del lado del servidor (pesos,
    /// pertenencia del producto al proveedor, pesajes vivos), así que acá no se
    /// duplica ninguna de esas reglas: duplicarlas es lo que las deja divergir.
    /// </summary>
    public Task<Result<ResultadoLoteProductos>> GuardarProductosLoteAsync(
        int idMovimiento,
        IReadOnlyList<ProductoCargaAlta> altas,
        IReadOnlyList<ProductoCargaCambio> cambios,
        IReadOnlyList<int> bajas,
        Guid idSolicitud,
        CancellationToken ct = default)
    {
        var altasLista   = altas   ?? Array.Empty<ProductoCargaAlta>();
        var cambiosLista = cambios ?? Array.Empty<ProductoCargaCambio>();
        var bajasLista   = bajas   ?? Array.Empty<int>();

        if (altasLista.Count == 0 && cambiosLista.Count == 0 && bajasLista.Count == 0)
            return Task.FromResult(Result<ResultadoLoteProductos>.Ok(
                new ResultadoLoteProductos(0, 0, 0, Array.Empty<int>())));

        var solicitudEfectiva = idSolicitud == Guid.Empty ? Guid.NewGuid() : idSolicitud;

        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();

            var altasJson = altasLista.Select(a => new Dictionary<string, object?>
            {
                ["id_producto"]      = a.IdProducto,
                ["peso_manifestado"] = (decimal)a.PesoManifestado,
                ["bultos_teoricos"]  = a.BultosDeclarados,
                ["observaciones"]    = string.IsNullOrWhiteSpace(a.Observaciones) ? null : a.Observaciones.Trim(),
            }).ToArray();

            var cambiosJson = cambiosLista.Select(c => new Dictionary<string, object?>
            {
                ["id_mov_producto"]  = c.IdMovProducto,
                ["peso_manifestado"] = (decimal)c.PesoManifestado,
                ["bultos_teoricos"]  = c.BultosDeclarados,
                ["observaciones"]    = string.IsNullOrWhiteSpace(c.Observaciones) ? null : c.Observaciones.Trim(),
            }).ToArray();

            var parametros = new Dictionary<string, object?>
            {
                ["p_id_movimiento"] = idMovimiento,
                ["p_altas"]         = altasJson,
                ["p_cambios"]       = cambiosJson,
                ["p_bajas"]         = bajasLista.ToArray(),
                ["p_id_solicitud"]  = solicitudEfectiva,
            };

            var response = await client.Rpc("registrar_productos_lote_seguro", parametros);
            ct.ThrowIfCancellationRequested();

            var res = ObtenerResultadoRpc(response?.Content, "guardar los productos de la carga");

            int creados      = res["creados"]?.Value<int>()      ?? 0;
            int actualizados = res["actualizados"]?.Value<int>() ?? 0;
            int anulados     = res["anulados"]?.Value<int>()     ?? 0;

            var idsToken = res["ids_creados"] as JArray;
            IReadOnlyList<int> idsCreados = idsToken != null
                ? idsToken.Select(t => t.Value<int>()).ToList()
                : Array.Empty<int>();

            return new ResultadoLoteProductos(creados, actualizados, anulados, idsCreados);
        }, "Guardar los productos de la carga");
    }

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
    public Task<Result<EntradaDto>> CrearEntradaAsync(int idMovProducto, int idProducto, double bruto, double taraExtra, string observaciones, int idUsuario, CancellationToken ct = default)
    {
        var idSolicitud = Guid.NewGuid();

        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.Rpc("ingresar_entrada_producto_pesaje_tabla_bitacora",
                new Dictionary<string, object?>
                {
                    ["p_id_mov_producto"] = idMovProducto,
                    ["p_id_producto"]     = idProducto,
                    ["p_peso_bruto"]     = (decimal)bruto,
                    ["p_peso_tara_extra"] = (decimal)taraExtra,
                    ["p_observaciones"]  = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones,
                    ["p_id_solicitud"]   = idSolicitud,
                });
            ct.ThrowIfCancellationRequested();

            // La identidad efectiva proviene de auth.uid(); se conserva el argumento legado
            // para no romper la interfaz mientras se completa la migración del módulo.
            _ = idUsuario;
            var resultado = ObtenerResultadoRpc(response?.Content, "registrar el pesaje");
            return new EntradaDto
            {
                Id                = resultado["id_pesaje"]?.Value<int>()
                                    ?? throw new InvalidOperationException("La RPC no devolvió id_pesaje."),
                Bruto             = resultado["peso_bruto"]?.Value<double>() ?? 0,
                TaraInd           = resultado["peso_tara_individual"]?.Value<double>() ?? 0,
                TaraExtra         = resultado["peso_tara_extra"]?.Value<double>() ?? 0,
                TaraTotal         = resultado["peso_tara_total"]?.Value<double>() ?? 0,
                Neto              = resultado["peso_neto"]?.Value<double>() ?? 0,
                BultosCapturados  = resultado["numero_bultos_recibido"]?.Type == JTokenType.Null
                                        ? null : resultado["numero_bultos_recibido"]?.Value<int?>(),
                Fecha             = FormatearFechaRpc(resultado["fecha_entrada"]?.Value<string>()),
                Hora              = FormatearHoraRpc(resultado["hora_entrada"]?.Value<string>()),
                Observaciones     = resultado["observaciones"]?.Value<string>() ?? "",
            };
        }, "Registrar pesaje");
    }

    private static string FormatearFechaRpc(string? valor) =>
        DateOnly.TryParse(valor, out var fecha) ? fecha.ToString("dd/MM/yyyy") : valor ?? "";

    private static string FormatearHoraRpc(string? valor) =>
        TimeOnly.TryParse(valor, out var hora) ? hora.ToString("hh:mm tt") : valor ?? "";

    /// <summary>
    /// El trigger de BD puede o no cubrir UPDATE. Escribir también los derivados deja la fila
    /// consistente en los dos casos: si el trigger corre, los recalcula con la misma fórmula y
    /// da igual; si no corre, valen los que mandamos.
    /// <para/>
    /// Poner en <c>false</c> SOLO si <c>peso_tara_total</c> / <c>peso_neto</c> resultan ser
    /// columnas GENERATED ALWAYS (PostgREST devolvería error al intentar escribirlas).
    /// </summary>
    private const bool EscribirDerivados = true;

    public Task<Result> ActualizarTaraExtraEntradaAsync(int idPesaje, double taraExtra, double taraTotal, double neto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var q = client.From<EntradaProducto>()
                .Where(e => e.idPesaje == idPesaje)
                .Set(e => e.pesoTaraExtra!, (decimal)taraExtra);

            // peso_tara_individual NO se toca: la calcula el trigger desde el catálogo y no
            // cambia al repartir la tara extra.
            if (EscribirDerivados)
                q = q.Set(e => e.pesoTaraTotal!, (decimal)taraTotal)
                     .Set(e => e.pesoNeto,       (decimal)neto);

            await q.Update();
        }, "Actualizar tara extra del pesaje");

    /// <summary>
    /// Reparte la tara extra total entre las pesadas indicadas en una sola transacción
    /// del lado del servidor (P-032). Usa la RPC <c>repartir_tara_extra_pesaje_tabla_bitacora</c>
    /// ya desplegada en la migración 202608240001_rpc_pesajes_idempotentes.sql.
    /// Si cualquier pesada viola restricciones, la transacción se aborta completamente.
    /// </summary>
    public Task<Result<ResultadoRepartoTaraExtra>> RepartirTaraExtraLoteAsync(
        IReadOnlyList<int> idsPesaje, double totalKg, Guid idSolicitud, CancellationToken ct = default)
    {
        if (idsPesaje is null || idsPesaje.Count == 0)
            return Task.FromResult(Result<ResultadoRepartoTaraExtra>.Ok(
                new ResultadoRepartoTaraExtra(0, Array.Empty<int>(), totalKg)));

        var solicitudEfectiva = idSolicitud == Guid.Empty ? Guid.NewGuid() : idSolicitud;

        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();

            var parametros = new Dictionary<string, object?>
            {
                ["p_ids_pesaje"]           = idsPesaje.ToArray(),
                ["p_peso_tara_extra_total"] = (decimal)totalKg,
                ["p_id_solicitud"]          = solicitudEfectiva,
            };

            var response = await client.Rpc("repartir_tara_extra_pesaje_tabla_bitacora", parametros);
            ct.ThrowIfCancellationRequested();

            var res = ObtenerResultadoRpc(response?.Content, "repartir la tara extra");

            int cantidad = res["cantidad_actualizada"]?.Value<int>() ?? 0;

            var idsToken = res["ids_pesaje"] as JArray;
            IReadOnlyList<int> ids = idsToken != null
                ? idsToken.Select(t => t.Value<int>()).ToList()
                : idsPesaje;

            double pesoTotal = res["peso_tara_extra_total"]?.Value<double>() ?? totalKg;

            return new ResultadoRepartoTaraExtra(cantidad, ids, pesoTotal);
        }, "Repartir tara extra en lote");
    }

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
        TaraInd       = (double)(e.pesoTaraIndividual ?? 0),
        TaraExtra     = (double)(e.pesoTaraExtra ?? 0),
        TaraTotal     = (double)(e.pesoTaraTotal ?? 0),
        Neto          = (double)e.pesoNeto,
        // Sin `?? 0`: null distingue "entrada nueva, no se capturan bultos" de
        // "entrada vieja que capturó 0".
        BultosCapturados = e.numeroBultosRecibido,
        Fecha         = e.fechaEntrada.ToString("dd/MM/yyyy"),
        Hora          = e.horaEntrada.ToString("hh:mm tt"),
        Observaciones = e.observaciones ?? "",
    };
}
