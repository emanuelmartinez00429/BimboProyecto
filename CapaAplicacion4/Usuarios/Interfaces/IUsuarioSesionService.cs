using CapaAplicacion.Common;
using CapaDominio.Entities;

namespace CapaAplicacion.Usuarios.Interfaces;

/// <summary>
/// Servicio de sesion del usuario. Fuente unica de verdad del estado
/// de autenticacion y permisos durante la sesion activa.
/// Singleton: una sola instancia compartida por toda la app.
/// </summary>
public interface IUsuarioSesionService
{
    /// <summary>Usuario actual autenticado, o null si no hay sesion.</summary>
    UsuarioSesion? SesionActual { get; }

    /// <summary>True si hay una sesion activa.</summary>
    bool Autenticado { get; }

    /// <summary>
    /// Inicia sesion: carga perfil, permisos reales desde BD y
    /// actualiza ultimo_acceso. Llamar despues de Auth.SignIn exitoso.
    /// </summary>
    Task<Result<UsuarioSesion>> IniciarSesionAsync(int idUsuario, CancellationToken ct = default);

    /// <summary>
    /// Limpia el estado de sesion. Llamar al cerrar sesion.
    /// </summary>
    void CerrarSesion();

    /// <summary>
    /// Verifica si el usuario actual tiene una accion especifica.
    /// Retorna false si no hay sesion activa.
    /// </summary>
    bool TienePermiso(string nombreAccion);
}
