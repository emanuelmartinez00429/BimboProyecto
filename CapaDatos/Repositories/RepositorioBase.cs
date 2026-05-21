using CapaAplicacion.Common;
using System.Diagnostics;

namespace CapaDatos.Repositories;

public abstract class RepositorioBase
{
    protected static async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> operacion,
        string contexto = "Operación")
    {
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

    protected static async Task<Result> TryAsync(
        Func<Task> operacion,
        string contexto = "Operación")
    {
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
