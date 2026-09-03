namespace CapaAplicacion.Common.Interfaces;

/// <summary>
/// Navega desde una notificación a un registro concreto. La implementación debe
/// validar la ruta antes de conservar una solicitud pendiente para la vista.
/// </summary>
public interface INotificacionNavigationService
{
    bool PuedeNavegar(string? tablaOrigen, int? idRegistroOrigen);
    bool TryNavegar(string tablaOrigen, int idRegistroOrigen, out string? motivo);
}
