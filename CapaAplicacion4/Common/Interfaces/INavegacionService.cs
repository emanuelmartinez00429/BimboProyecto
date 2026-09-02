namespace CapaAplicacion.Common.Interfaces;

public interface INavegacionService
{
    bool TryNavigate(string routeId, out string? motivo);
}
