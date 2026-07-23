namespace CapaDominio.Entities
{
    /// <summary>
    /// Agrupacion de permisos por modulo.
    /// Contiene los nombres de las acciones asignadas a un rol dentro de un modulo.
    /// </summary>
    public sealed class ModuloPermisos
    {
        public int    IdModulo          { get; init; }
        public string NombreModulo      { get; init; } = string.Empty;
        public string? DescripcionModulo { get; init; }
        public IReadOnlyList<string> Acciones { get; init; } = Array.Empty<string>();
    }
}
