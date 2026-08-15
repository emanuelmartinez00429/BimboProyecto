namespace CapaAplicacion.Empresa.Dtos;

public sealed class EmpresaDto
{
    public int IdEmpresa { get; init; }
    public string NombreEmpresa { get; init; } = string.Empty;
    public string? RtnEmpresa { get; init; }
    public string? DireccionEmpresa { get; init; }
    public string? TelefonoEmpresa { get; init; }
    public string? CorreoEmpresa { get; init; }
    public string? LogoEmpresa { get; init; }
    public string? IconoSidebar { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? DominioCorreo { get; init; }
    public string? ColorEmpresa { get; init; }
}

public sealed class ActualizarEmpresaDto
{
    public int IdEmpresa { get; init; }
    public string NombreEmpresa { get; init; } = string.Empty;
    public string? RtnEmpresa { get; init; }
    public string? DireccionEmpresa { get; init; }
    public string? TelefonoEmpresa { get; init; }
    public string? CorreoEmpresa { get; init; }
    public string? DominioCorreo { get; init; }
    public string? ColorEmpresa { get; init; }
}

public sealed class EmpresaGuardadaDto
{
    public required EmpresaDto Empresa { get; init; }
    public IReadOnlyList<string> Advertencias { get; init; } = [];
}
