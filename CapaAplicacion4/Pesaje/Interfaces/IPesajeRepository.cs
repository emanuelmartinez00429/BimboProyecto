using CapaAplicacion.Common;
using CapaAplicacion.Pesaje.Dtos;

namespace CapaAplicacion.Pesaje.Interfaces;

/// <summary>
/// Resultado del alta atómica de un lote de camiones en el servidor.
/// </summary>
public record ResultadoAltaLoteCamiones(int Creados, int PrimerIdMovimiento, IReadOnlyList<int> IdsMovimiento);

/// <summary>
/// Resultado del reparto atómico de tara extra entre varias pesadas (P-032).
/// </summary>
public record ResultadoRepartoTaraExtra(int CantidadActualizada, IReadOnlyList<int> IdsPesaje, double PesoTaraExtraTotal);

/// <summary>Producto nuevo que se suma a la carga de una recepción.</summary>
public record ProductoCargaAlta(int IdProducto, double PesoManifestado, int BultosDeclarados, string Observaciones);

/// <summary>
/// Producto que ya estaba en la carga y al que se le corrigió el manifiesto.
/// Se identifica por <c>id_mov_producto</c>, no por producto: la misma recepción
/// no repite producto, pero la clave real de la fila es esa.
/// </summary>
public record ProductoCargaCambio(int IdMovProducto, double PesoManifestado, int BultosDeclarados, string Observaciones);

/// <summary>Resultado del guardado atómico de la carga completa de una recepción.</summary>
public record ResultadoLoteProductos(int Creados, int Actualizados, int Anulados, IReadOnlyList<int> IdsCreados);

/// <summary>
/// Persistencia del módulo de Recepción de Materia Prima (Fase 2).
/// Mapea a las tablas reales `movimientos` / `movimiento_productos` / `entradas_producto`.
/// "Quitar" = anular por estado (id_estado = 9). El trigger de BD calcula neto/tara al insertar pesajes.
/// </summary>
public interface IPesajeRepository
{
    // ── Camiones (movimientos) ──────────────────────────────────────────────
    Task<Result<IReadOnlyList<CamionDto>>> GetCamionesActivosAsync(CancellationToken ct = default);
    Task<Result<int>> CrearCamionAsync(int idProveedor, string placa, string observaciones, int idUsuario, CancellationToken ct = default);

    /// <summary>
    /// Registra de forma atómica (en una sola transacción en el servidor) un lote de camiones.
    /// Si cualquier fila falla, la transacción se aborta completamente y ningún camión es persistido.
    /// </summary>
    Task<Result<ResultadoAltaLoteCamiones>> RegistrarCamionesLoteAsync(
        IReadOnlyList<(string Placa, int IdProveedor, string? Observaciones)> camiones,
        Guid idSolicitud,
        CancellationToken ct = default);
    Task<Result>      ActualizarCamionAsync(int idMovimiento, int idProveedor, string placa, string observaciones, CancellationToken ct = default);
    Task<Result>      CerrarCamionAsync(int idMovimiento, CancellationToken ct = default);
    Task<Result>      AnularCamionAsync(int idMovimiento, CancellationToken ct = default);

    // ── Productos del camión (movimiento_productos) ─────────────────────────
    Task<Result<IReadOnlyList<MovProductoDto>>> GetProductosAsync(int idMovimiento, CancellationToken ct = default);

    /// <summary>Productos vivos y total manifestado (KG) por recepción, en una sola consulta para toda la lista.</summary>
    Task<Result<IReadOnlyDictionary<int, (int Conteo, double TotalKg)>>> ContarProductosPorCamionAsync(
        IReadOnlyList<int> idsMovimiento, CancellationToken ct = default);
    Task<Result>      SetEstadoProductoAsync(int idMovProducto, bool cerrado, CancellationToken ct = default);

    /// <summary>
    /// Quita un producto de la carga (anular por estado, no DELETE). El servidor rechaza
    /// hacerlo si el producto tiene pesajes activos.
    /// </summary>
    Task<Result>      AnularProductoAsync(int idMovProducto, CancellationToken ct = default);

    /// <summary>
    /// Guarda de una sola vez toda la carga de una recepción — altas, correcciones
    /// de manifiesto y bajas — en UNA transacción del servidor
    /// (RPC <c>registrar_productos_lote_seguro</c>).
    /// <para/>
    /// Si cualquier fila viola una restricción (peso o bultos no positivos, producto
    /// ajeno al proveedor de la recepción, producto con pesajes activos que se intenta
    /// quitar), la transacción se aborta entera y no persiste NADA — ni siquiera las
    /// filas que sí eran válidas. Es el mismo criterio de P-032 y P-053: el manifiesto
    /// a medio guardar es peor que el manifiesto sin guardar.
    /// </summary>
    Task<Result<ResultadoLoteProductos>> GuardarProductosLoteAsync(
        int idMovimiento,
        IReadOnlyList<ProductoCargaAlta> altas,
        IReadOnlyList<ProductoCargaCambio> cambios,
        IReadOnlyList<int> bajas,
        Guid idSolicitud,
        CancellationToken ct = default);

    // ── Pesajes (entradas_producto) ─────────────────────────────────────────

    /// <summary>
    /// Registra un pesaje y devuelve la fila YA CALCULADA por el trigger de BD
    /// (tara individual, tara total y neto).
    /// <para/>
    /// Devuelve el <see cref="EntradaDto"/> completo y no solo el id a propósito: la respuesta
    /// del INSERT ya trae esos derivados, así que quien llama puede reflejar la pesada nueva
    /// sin volver a consultar el camión entero. Ese refetch costaba tres round trips extra por
    /// cada pesada — a la latencia de la red de planta, segundos de pantalla congelada.
    /// </summary>
    Task<Result<EntradaDto>> CrearEntradaAsync(int idMovProducto, int idProducto, double bruto, double taraExtra, string observaciones, int idUsuario, CancellationToken ct = default);

    /// <summary>
    /// Escribe la tara extra de una pesada ya registrada (UPDATE en el lugar, NO anular+insertar:
    /// preserva id, fecha, hora y usuario del pesaje). Se usa al repartir un total entre las
    /// pesadas de un producto o de un camión. Recibe también los derivados ya calculados para
    /// que la fila quede consistente aunque el trigger de BD no cubra UPDATE.
    /// </summary>
    Task<Result>      ActualizarTaraExtraEntradaAsync(int idPesaje, double taraExtra, double taraTotal, double neto, CancellationToken ct = default);

    /// <summary>
    /// Reparte una tara extra total entre las pesadas indicadas en una sola transacción
    /// del lado del servidor (RPC <c>repartir_tara_extra_pesaje_tabla_bitacora</c>).
    /// Si alguna pesada viola restricciones, la transacción se aborta y ningún UPDATE persiste.
    /// </summary>
    Task<Result<ResultadoRepartoTaraExtra>> RepartirTaraExtraLoteAsync(
        IReadOnlyList<int> idsPesaje, double totalKg, Guid idSolicitud, CancellationToken ct = default);

    Task<Result> AnularEntradaAsync(int idPesaje, CancellationToken ct = default);
}
