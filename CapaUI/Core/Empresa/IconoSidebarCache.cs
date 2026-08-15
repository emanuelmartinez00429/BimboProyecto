using System.IO;
using System.Linq;
using CapaAplicacion.Empresa.Interfaces;

namespace CapaUI.Core.Empresa;

public sealed class IconoSidebarCache
{
    private readonly IEmpresaRepository _repo;
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BimboPesaje", "IconoSidebar");

    public IconoSidebarCache(IEmpresaRepository repo) => _repo = repo;

    public string? ObtenerRutaCacheadaSinRed()
    {
        try
        {
            return Directory.Exists(CacheDir)
                ? Directory.EnumerateFiles(CacheDir).FirstOrDefault(x => !x.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                : null;
        }
        catch { return null; }
    }

    public async Task<string?> ObtenerRutaLocalAsync(string? rutaStorage)
    {
        if (string.IsNullOrWhiteSpace(rutaStorage)) return null;
        var nombre = Path.GetFileName(rutaStorage);
        if (string.IsNullOrWhiteSpace(nombre)) return null;

        Directory.CreateDirectory(CacheDir);
        var destino = Path.Combine(CacheDir, nombre);
        if (File.Exists(destino) && new FileInfo(destino).Length > 0)
        {
            Limpiar(nombre);
            return destino;
        }

        var temporal = destino + ".tmp";
        try
        {
            var resultado = await _repo.DescargarLogoAsync(rutaStorage, temporal);
            if (!resultado.Success) throw new InvalidOperationException(resultado.Error);
            File.Move(temporal, destino, true);
            Limpiar(nombre);
            return destino;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "No se pudo descargar el icono del sidebar {Ruta}", rutaStorage);
            try { if (File.Exists(temporal)) File.Delete(temporal); } catch { }
            return null;
        }
    }

    public string ActualizarDesdeArchivoLocal(string rutaStorage, string rutaLocal)
    {
        var nombre = Path.GetFileName(rutaStorage);
        if (string.IsNullOrWhiteSpace(nombre))
            throw new InvalidOperationException("La ruta del icono en Storage no es válida.");
        Directory.CreateDirectory(CacheDir);
        var destino = Path.Combine(CacheDir, nombre);
        File.Copy(rutaLocal, destino, true);
        Limpiar(nombre);
        return destino;
    }

    private static void Limpiar(string vigente)
    {
        try
        {
            foreach (var archivo in Directory.EnumerateFiles(CacheDir))
                if (!string.Equals(Path.GetFileName(archivo), vigente, StringComparison.OrdinalIgnoreCase))
                    File.Delete(archivo);
        }
        catch { }
    }
}
