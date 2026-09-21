using CapaDominio;

namespace CapaAplicacion.Perfil;

public interface IPerfilUsuarioService
{
    PerfilUsuario? PerfilActual { get; }

    Task CargarAsync(int idUsuario);
    void Limpiar();

    /// <summary>
    /// Se dispara cuando <see cref="CargarAsync"/> termina (con o sin datos) y al
    /// llamar <see cref="Limpiar"/>. Los consumidores que cachean el nombre/iniciales
    /// (pantalla de bienvenida, tarjeta del sidebar) se refrescan al ritmo del perfil,
    /// sin esperar al próximo login.
    /// </summary>
    event Action? PerfilActualizado;
}
