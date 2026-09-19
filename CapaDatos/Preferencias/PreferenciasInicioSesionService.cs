using System.IO;
using System.Security.Cryptography;
using System.Text;
using CapaAplicacion.Auth.Interfaces;

namespace CapaDatos.Preferencias;

/// <summary>
/// Persistencia local en disco del correo del último usuario que inició sesión («Recordar usuario»).
/// Nunca guarda contraseñas ni tokens.
/// </summary>
/// <remarks>
/// <para>
/// El correo es también el usuario de login, así que se guarda <b>cifrado con DPAPI</b>
/// (<see cref="ProtectedData"/>, alcance <see cref="DataProtectionScope.CurrentUser"/>): solo se
/// descifra con la misma cuenta de Windows en el mismo equipo. No hay clave en el código —
/// Windows la deriva de la cuenta—, así que descompilar la app no sirve para leerlo.
/// </para>
/// <para>
/// Vive en <c>%LOCALAPPDATA%</c> y no en <c>%APPDATA%</c> (Roaming): en un dominio, Roaming se
/// sincroniza entre equipos y el archivo viajaría a cualquier PC donde esa persona inicie sesión.
/// </para>
/// <para>
/// Límite conocido: no protege contra quien usa esa misma sesión de Windows — la app lo
/// descifra y lo muestra prellenado. En terminales compartidas lo que corresponde es no marcar
/// «Recordar usuario».
/// </para>
/// </remarks>
public sealed class PreferenciasInicioSesionService : IPreferenciasInicioSesionService
{
    private const string NombreApp = "BimboPesaje";

    // "Entropía" adicional de DPAPI: ata el blob a este uso. Otra app de la misma cuenta que
    // también use DPAPI no lo descifra sin este valor. No es un secreto; el "v1" permite
    // cambiar el formato más adelante sin confundir blobs viejos.
    private static readonly byte[] Proposito = Encoding.UTF8.GetBytes("BimboPesaje.UltimoUsuario.v1");

    private readonly string _carpeta;
    private readonly string _rutaCifrada;
    private readonly string _rutaLegada;

    public PreferenciasInicioSesionService()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), NombreApp, "Preferencias"),
            // Versión anterior: texto plano en Roaming. Se migra una vez y se borra.
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), NombreApp, "Preferencias", "ultimo_usuario.txt"))
    {
    }

    /// <summary>Rutas explícitas: para los tests, que no deben tocar el perfil real del equipo.</summary>
    internal PreferenciasInicioSesionService(string carpeta, string rutaLegada)
    {
        _carpeta     = carpeta;
        _rutaCifrada = Path.Combine(carpeta, "ultimo_usuario.bin");
        _rutaLegada  = rutaLegada;
    }

    public string? ObtenerUltimoUsuario()
    {
        // DPAPI es de Windows. CapaDatos compila para net10.0 (no -windows): sin esta guarda
        // el analizador de plataforma (CA1416) marca cada llamada a ProtectedData.
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            MigrarLegadoSiExiste();
            if (!File.Exists(_rutaCifrada)) return null;

            var claro = ProtectedData.Unprotect(File.ReadAllBytes(_rutaCifrada), Proposito, DataProtectionScope.CurrentUser);
            var email = Encoding.UTF8.GetString(claro).Trim();
            return email.Length == 0 ? null : email;
        }
        catch (CryptographicException)
        {
            // Copiado de otro equipo/cuenta, o corrupto: no se puede leer y no se va a poder
            // nunca. Se descarta y el login arranca vacío.
            BorrarSilencioso(_rutaCifrada);
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void GuardarUltimoUsuario(string? email)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                BorrarSilencioso(_rutaCifrada);
                BorrarSilencioso(_rutaLegada);
                return;
            }

            Directory.CreateDirectory(_carpeta);
            var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(email.Trim()), Proposito, DataProtectionScope.CurrentUser);

            // Temporal + reemplazo: un corte a mitad de escritura no deja un blob truncado.
            var temporal = _rutaCifrada + ".tmp";
            File.WriteAllBytes(temporal, cifrado);
            File.Move(temporal, _rutaCifrada, overwrite: true);
        }
        catch
        {
            // Falla de forma silenciosa para no interrumpir el flujo del usuario si hay problemas de disco o permisos
        }
    }

    /// <summary>
    /// Convierte el archivo en texto plano de la versión anterior y lo borra, para que quien
    /// ya tenía «Recordar usuario» no lo pierda y el correo en claro no quede en disco.
    /// </summary>
    private void MigrarLegadoSiExiste()
    {
        if (!File.Exists(_rutaLegada)) return;

        var viejo = File.ReadAllText(_rutaLegada, Encoding.UTF8).Trim();
        if (viejo.Length > 0 && !File.Exists(_rutaCifrada))
            GuardarUltimoUsuario(viejo);

        BorrarSilencioso(_rutaLegada);
    }

    private static void BorrarSilencioso(string ruta)
    {
        try
        {
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch
        {
            // Mismo criterio que el resto del servicio: nunca bloquea el login.
        }
    }
}
