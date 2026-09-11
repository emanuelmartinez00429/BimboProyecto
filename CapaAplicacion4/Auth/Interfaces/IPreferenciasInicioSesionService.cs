namespace CapaAplicacion.Auth.Interfaces;

/// <summary>
/// Contrato para la persistencia local de preferencias del formulario de inicio de sesión.
/// Solo almacena el identificador/correo del usuario, nunca contraseñas ni tokens.
/// </summary>
public interface IPreferenciasInicioSesionService
{
    /// <summary>
    /// Obtiene el último correo guardado localmente en esta máquina, o <c>null</c> si no existe o falló la lectura.
    /// </summary>
    string? ObtenerUltimoUsuario();

    /// <summary>
    /// Guarda el correo para recordar en el próximo inicio de sesión.
    /// Si <paramref name="email"/> es <c>null</c>, vacío o espacios en blanco, limpia el archivo para olvidar el usuario.
    /// </summary>
    void GuardarUltimoUsuario(string? email);
}