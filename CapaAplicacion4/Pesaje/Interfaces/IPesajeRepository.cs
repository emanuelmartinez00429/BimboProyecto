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
    Task<Result<int>> AgregarProductoAsync(int idMovimiento, int idProducto, double pesoManifestado, int bultosTeoricos, string observaciones, CancellationToken ct = default);
    Task<Result>      ActualizarProductoAsync(int idMovProducto, double pesoManifestado, int bultosTeoricos, string observaciones, CancellationToken ct = default);
    Task<Result>      AnularProductoAsync(int idMovProducto, CancellationToken ct = default);
    Task<Result>      SetEstadoProductoAsync(int idMovProducto, bool cerrado, CancellationToken ct = default);

    // ── Pesajes (entradas_producto) ─────────────────────────────────────────
    Task<Result<int>> CrearEntradaAsync(int idMovProducto, int idProducto, double bruto, double taraExtra, int bultos, string observaciones, int idUsuario, CancellationToken ct = default);
    Task<Result>      AnularEntradaAsync(int idPesaje, CancellationToken ct = default);
}
