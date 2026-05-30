using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using System.Diagnostics;

namespace CapaDatos.Repositories;

/// <summary>
/// Base de los repositorios CRUD. Centraliza el manejo de errores (TryAsync) y el
/// fail-fast de conectividad: si el monitor reporta <see cref="EstadoConexion.SinConexion"/>,
/// la operación se corta de inmediato en vez de esperar el timeout HTTP.
/// </summary>
public abstract class RepositorioBase
{
    private readonly IConexionMonitor _conexion;

    protected RepositorioBase(IConexionMonitor conexion) => _conexion = conexion;

    private const string MsgSinConexion = "Sin conexión a internet.";

    protected async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> operacion,
        string contexto = "Operación")
    {
        // Fail-fast: sin red física no tiene sentido intentar (evita el hang del timeout).
        if (_conexion.Estado == EstadoConexion.SinConexion)
            return Result<T>.Fail(MsgSinConexion);

        try
        {
            return Result<T>.Ok(await operacion());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[{contexto}] {ex}");
            return Result<T>.Fail($"{contexto}: {ex.Message}");
        }
    }

    protected async Task<Result> TryAsync(
        Func<Task> operacion,
        string contexto = "Operación")
    {
        if (_conexion.Estado == EstadoConexion.SinConexion)
            return Result.Fail(MsgSinConexion);

        try
        {
            await operacion();
            return Result.Ok();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[{contexto}] {ex}");
            return Result.Fail($"{contexto}: {ex.Message}");
        }
    }
}
