using CapaAplicacion.Auth.Dtos;
using CapaAplicacion.Common;

namespace CapaAplicacion.Auth.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResultDto>> LoginAsync(string email, string password, CancellationToken ct = default);
}
