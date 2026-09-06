using CapaAplicacion.Common;
using CapaAplicacion.Pesaje.Dtos;

namespace CapaAplicacion.Pesaje.Interfaces;

/// <summary>
/// Resultado del alta atómica de un lote de camiones en el servidor.
/// </summary>
public record ResultadoAltaLoteCamiones(int Creados, int PrimerIdMovimiento, IReadOnlyList<int> IdsMovimiento);

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
    Task<Result<int>> AgregarProductoAsync(int idMovimiento, int idProducto, double pesoManifestado, int bultosDeclarados, string observaciones, CancellationToken ct = default);
    Task<Result>      ActualizarProductoAsync(int idMovProducto, double pesoManifestado, int bultosDeclarados, string observaciones, CancellationToken ct = default);
    Task<Result>      AnularProductoAsync(int idMovProducto, CancellationToken ct = default);
    Task<Result>      SetEstadoProductoAsync(int idMovProducto, bool cerrado, CancellationToken ct = default);

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
    Task<Result>      AnularEntradaAsync(int idPesaje, CancellationToken ct = default);
}
