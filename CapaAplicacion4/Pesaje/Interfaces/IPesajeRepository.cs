using CapaAplicacion.Common;
using CapaAplicacion.Pesaje.Dtos;

namespace CapaAplicacion.Pesaje.Interfaces;

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
    Task<Result<int>> CrearEntradaAsync(int idMovProducto, int idProducto, double bruto, double taraExtra, string observaciones, int idUsuario, CancellationToken ct = default);

    /// <summary>
    /// Escribe la tara extra de una pesada ya registrada (UPDATE en el lugar, NO anular+insertar:
    /// preserva id, fecha, hora y usuario del pesaje). Se usa al repartir un total entre las
    /// pesadas de un producto o de un camión. Recibe también los derivados ya calculados para
    /// que la fila quede consistente aunque el trigger de BD no cubra UPDATE.
    /// </summary>
    Task<Result>      ActualizarTaraExtraEntradaAsync(int idPesaje, double taraExtra, double taraTotal, double neto, CancellationToken ct = default);
    Task<Result>      AnularEntradaAsync(int idPesaje, CancellationToken ct = default);
}
