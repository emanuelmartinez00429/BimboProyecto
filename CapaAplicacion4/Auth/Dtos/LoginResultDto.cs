namespace CapaAplicacion.Auth.Dtos;

public sealed class LoginResultDto
{
    public int    IdUsuario { get; init; }
    public string Email     { get; init; } = "";
    public int    IdRol     { get; init; }
}
