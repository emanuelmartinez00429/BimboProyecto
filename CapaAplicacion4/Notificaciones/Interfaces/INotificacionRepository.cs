using CapaAplicacion.Common;
using CapaAplicacion.Notificaciones.Dtos;

namespace CapaAplicacion.Notificaciones.Interfaces;

public interface INotificacionRepository
{
    Task<Result<IReadOnlyList<NotificacionDto>>> ListarAsync(FiltroNotificacionesDto filtro, CancellationToken ct = default);
    Task<Result<NotificacionDto?>> ObtenerAsync(long idNotificacion, CancellationToken ct = default);
    Task<Result<long>> ContarNoLeidasAsync(CancellationToken ct = default);
    Task<Result> MarcarLeidaAsync(long idNotificacion, CancellationToken ct = default);
    Task<Result<int>> MarcarTodasLeidasYArchivarAsync(CancellationToken ct = default);
    Task<Result> ArchivarAsync(long idNotificacion, CancellationToken ct = default);
    Task<Result> RestaurarAsync(long idNotificacion, CancellationToken ct = default);
}
