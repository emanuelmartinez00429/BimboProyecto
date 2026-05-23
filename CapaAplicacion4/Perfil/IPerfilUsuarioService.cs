using CapaDominio;

namespace CapaAplicacion.Perfil;

public interface IPerfilUsuarioService
{
    PerfilUsuario? PerfilActual { get; }

    Task CargarAsync(int idUsuario);
    void Limpiar();
}
