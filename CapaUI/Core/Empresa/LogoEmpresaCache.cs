using System.IO;
using CapaDatos.Repositorios;

namespace CapaUI.Core.Empresa;

/// <summary>
/// Caché local del logo de empresa que se muestra en el login.
///
/// El nombre del archivo en Storage (columna <c>empresa.logo_empresa</c>) hace de
/// clave de versión: cuando el módulo de configuración (todavía no construido) suba
/// un logo nuevo va a guardar una ruta distinta (así ya funciona
/// <see cref="RepositorioEmpresa.ActualizarLogoAsync"/>). Por eso no hace falta comparar
/// hashes ni fechas para "detectar el cambio" — un archivo local con ese nombre ya
/// garantiza que es la misma versión, y un nombre distinto dispara la descarga solo,
/// la próxima vez que se abra el login.
/// </summary>
public static class LogoEmpresaCache
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BimboPesaje", "LogoEmpresa");

    /// <summary>
    /// Devuelve la ruta local del logo vigente, descargándolo si hace falta.
    /// Devuelve <c>null</c> si no hay logo configurado o si la descarga falla — en
    /// ambos casos el caller debe quedarse con el logo empacado por defecto.
    /// </summary>
    public static async Task<string?> ObtenerRutaLocalAsync(string? rutaStorage)
    {
        if (string.IsNullOrWhiteSpace(rutaStorage)) return null;

        var nombreArchivo = Path.GetFileName(rutaStorage);
        if (string.IsNullOrWhiteSpace(nombreArchivo)) return null;

        Directory.CreateDirectory(CacheDir);
        var rutaLocal = Path.Combine(CacheDir, nombreArchivo);

        if (File.Exists(rutaLocal) && new FileInfo(rutaLocal).Length > 0)
        {
            LimpiarVersionesViejas(nombreArchivo);
            return rutaLocal;
        }

        var rutaTemp = rutaLocal + ".tmp";
        try
        {
            await RepositorioEmpresa.DescargarLogoAsync(rutaStorage, rutaTemp);
            File.Move(rutaTemp, rutaLocal, overwrite: true);
            LimpiarVersionesViejas(nombreArchivo);
            return rutaLocal;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "LogoEmpresaCache: no se pudo descargar el logo '{Ruta}'", rutaStorage);
            try { if (File.Exists(rutaTemp)) File.Delete(rutaTemp); } catch { /* mejor esfuerzo */ }
            return null;
        }
    }

    /// <summary>
    /// Borra del caché las versiones del logo distintas a la vigente — mejor esfuerzo,
    /// no es crítico si falla (el próximo login lo vuelve a intentar).
    /// </summary>
    private static void LimpiarVersionesViejas(string nombreVigente)
    {
        try
        {
            foreach (var archivo in Directory.EnumerateFiles(CacheDir))
            {
                var nombre = Path.GetFileName(archivo);
                if (!string.Equals(nombre, nombreVigente, StringComparison.OrdinalIgnoreCase))
                    File.Delete(archivo);
            }
        }
        catch { /* mejor esfuerzo */ }
    }
}
