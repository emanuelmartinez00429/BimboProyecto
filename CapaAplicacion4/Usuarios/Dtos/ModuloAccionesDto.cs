namespace CapaAplicacion.Usuarios.Dtos;

public sealed class ModuloAccionesDto
{
    public int IdModulo { get; init; }
    public string NombreModulo { get; init; } = string.Empty;
    public string? DescripcionModulo { get; init; }
    public IReadOnlyList<AccionDto> Acciones { get; init; } = Array.Empty<AccionDto>();
}

public sealed class AccionDto
{
    public int IdAccion { get; init; }
    public string NombreAccion { get; init; } = string.Empty;
    public string? DescripcionAccion { get; init; }
}
