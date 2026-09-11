namespace CapaUI.Core.Validacion;

/// <summary>
/// Determina si un formulario o entidad ha sido modificado respecto a un snapshot inicial (dirty tracking).
/// Diseñado para operar sobre tipos inmutables que implementan igualdad por valor (como C# <c>record</c>).
/// </summary>
/// <typeparam name="T">Tipo inmutable del snapshot de campos editables.</typeparam>
public sealed class ChangeTracker<T>
{
    private readonly T? _snapshotInicial;
    private readonly bool _tieneSnapshot;

    /// <summary>
    /// Inicializa un tracker con el snapshot inicial. Si el snapshot es <c>null</c>,
    /// cualquier llamada posterior a <see cref="IsDirty"/> retornará <c>true</c>.
    /// </summary>
    public ChangeTracker(T? snapshotInicial)
    {
        _snapshotInicial = snapshotInicial;
        _tieneSnapshot = snapshotInicial is not null;
    }

    /// <summary>
    /// Snapshot inicial registrado al instanciar el tracker.
    /// </summary>
    public T? InitialSnapshot => _snapshotInicial;

    /// <summary>
    /// Indica si el tracker cuenta con un snapshot inicial válido.
    /// </summary>
    public bool HasInitialSnapshot => _tieneSnapshot;

    /// <summary>
    /// Retorna <c>true</c> si el estado actual difiere del snapshot inicial.
    /// Si no existe snapshot inicial (ej. entidad nueva o nula), retorna siempre <c>true</c>.
    /// </summary>
    public bool IsDirty(T? actual)
    {
        if (!_tieneSnapshot)
            return true;

        return !EqualityComparer<T>.Default.Equals(_snapshotInicial, actual);
    }
}

/// <summary>
/// Métodos auxiliares para la instanciación de <see cref="ChangeTracker{T}"/>.
/// </summary>
public static class ChangeTracker
{
    /// <summary>
    /// Crea una nueva instancia de <see cref="ChangeTracker{T}"/> infiriendo el tipo genérico.
    /// </summary>
    public static ChangeTracker<T> Create<T>(T? snapshot) => new(snapshot);
}
