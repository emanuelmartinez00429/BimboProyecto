namespace CapaAplicacion.Common;

/// <summary>
/// Representa el resultado de una operación que retorna un valor.
/// Evita usar excepciones como flujo de control para errores esperados.
/// </summary>
public sealed class Result<T>
{
    public bool   Success { get; }
    public T?     Value   { get; }
    public string Error   { get; }

    private Result(bool success, T? value, string error)
        => (Success, Value, Error) = (success, value, error);

    public static Result<T> Ok(T value)      => new(true,  value,   string.Empty);
    public static Result<T> Fail(string msg) => new(false, default, msg);
}

/// <summary>
/// Representa el resultado de una operación sin valor de retorno.
/// </summary>
public sealed class Result
{
    public bool   Success { get; }
    public string Error   { get; }

    private Result(bool success, string error)
        => (Success, Error) = (success, error);

    public static Result Ok()             => new(true,  string.Empty);
    public static Result Fail(string msg) => new(false, msg);
}
