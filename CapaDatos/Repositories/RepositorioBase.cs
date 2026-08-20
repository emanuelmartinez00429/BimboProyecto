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

    /// <summary>
    /// Cronómetro de cada llamada al backend. Todas las operaciones de todos los repositorios
    /// pasan por acá, así que es el único punto donde hay que medir para saber cuánto cuesta
    /// realmente un round trip a Supabase desde la red del cliente.
    /// <para/>
    /// Es a nivel Debug y sin asignaciones cuando el nivel está apagado: se puede dejar
    /// permanentemente. Ante cualquier queja de lentitud, el log dice si el costo está en la
    /// red o en el código.
    /// </summary>
    private static void Medir(string contexto, long ms, string resultado) =>
        Serilog.Log.Debug("[Repo] {Contexto} — {Ms} ms ({Resultado})", contexto, ms, resultado);

    protected async Task<Result<T>> TryAsync<T>(
        Func<Task<T>> operacion,
        string contexto = "Operación")
    {
        // Fail-fast: sin red física no tiene sentido intentar (evita el hang del timeout).
        if (_conexion.Estado == EstadoConexion.SinConexion)
        {
            Medir(contexto, 0, "sin conexión");
            return Result<T>.Fail(MsgSinConexion);
        }

        var cron = Stopwatch.StartNew();
        try
        {
            var valor = await operacion();
            Medir(contexto, cron.ElapsedMilliseconds, "ok");
            return Result<T>.Ok(valor);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Medir(contexto, cron.ElapsedMilliseconds, "error");
            Debug.WriteLine($"[{contexto}] {ex}");
            return Result<T>.Fail($"{contexto}: {ex.Message}");
        }
    }

    protected async Task<Result> TryAsync(
        Func<Task> operacion,
        string contexto = "Operación")
    {
        if (_conexion.Estado == EstadoConexion.SinConexion)
        {
            Medir(contexto, 0, "sin conexión");
            return Result.Fail(MsgSinConexion);
        }

        var cron = Stopwatch.StartNew();
        try
        {
            await operacion();
            Medir(contexto, cron.ElapsedMilliseconds, "ok");
            return Result.Ok();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Medir(contexto, cron.ElapsedMilliseconds, "error");
            Debug.WriteLine($"[{contexto}] {ex}");
            return Result.Fail($"{contexto}: {ex.Message}");
        }
    }
}
