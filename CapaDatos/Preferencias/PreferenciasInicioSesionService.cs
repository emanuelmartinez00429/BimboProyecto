using System.IO;
using System.Text;
using CapaAplicacion.Auth.Interfaces;

namespace CapaDatos.Preferencias;

/// <summary>
/// Persistencia local en disco del correo del último usuario que inició sesión.
/// Nunca guarda contraseñas ni tokens, 100% aislado en el equipo local (%APPDATA%\BimboPesaje\Preferencias).
/// </summary>
public sealed class PreferenciasInicioSesionService : IPreferenciasInicioSesionService
{
    private static readonly string CarpetaPreferencias = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BimboPesaje",
        "Preferencias");

    private static readonly string RutaArchivo = Path.Combine(CarpetaPreferencias, "ultimo_usuario.txt");

    public string? ObtenerUltimoUsuario()
    {
        try
        {
            if (!File.Exists(RutaArchivo))
                return null;

            var email = File.ReadAllText(RutaArchivo, Encoding.UTF8)?.Trim();
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }
        catch
        {
            return null;
        }
    }

    public void GuardarUltimoUsuario(string? email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                if (File.Exists(RutaArchivo))
                {
                    File.Delete(RutaArchivo);
                }
                return;
            }

            Directory.CreateDirectory(CarpetaPreferencias);
            File.WriteAllText(RutaArchivo, email.Trim(), Encoding.UTF8);
        }
        catch
        {
            // Falla de forma silenciosa para no interrumpir el flujo del usuario si hay problemas de disco o permisos
        }
    }
}