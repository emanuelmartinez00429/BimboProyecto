using CapaAplicacion.Common;
using CapaAplicacion.Empresa.Dtos;

namespace CapaAplicacion.Empresa.Interfaces;

public interface IEmpresaRepository
{
    Task<Result<EmpresaDto?>> ObtenerAsync(CancellationToken ct = default);

    Task<Result<EmpresaGuardadaDto>> GuardarAsync(
        ActualizarEmpresaDto empresa,
        string? rutaLogoLocal,
        string? rutaIconoSidebarLocal,
        CancellationToken ct = default);

    Task<Result> DescargarLogoAsync(
        string rutaStorage,
        string rutaLocalDestino,
        CancellationToken ct = default);
}
