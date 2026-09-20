using System.IO;
using System.Text.Json;
using CapaAplicacion.Preferencias.Interfaces;

namespace CapaDatos.Preferencias;

/// <summary>
/// Copia local del factor de escala, en JSON plano bajo <c>%LOCALAPPDATA%</c>.
/// <para/>
/// Vive junto a <see cref="PreferenciasInicioSesionService"/> y usa la misma carpeta,
/// pero <b>no</b> cifra con DPAPI: aquel guarda el correo de login —un dato de cuenta—
/// y este un número de escala. Cifrar acá daría una sensación de protección que no
/// aporta nada, y obligaría a la guarda de plataforma (<c>CA1416</c>) que necesita DPAPI.
/// </summary>
public sealed class CacheEscalaLocal : ICacheEscalaLocal
{
    private const string NombreApp = "BimboPesaje";

    private readonly string _carpeta;

    public CacheEscalaLocal()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            NombreApp,
            "Preferencias"))
    {
    }

    /// <summary>Carpeta explícita: para los tests, que no deben tocar el perfil real del equipo.</summary>
    internal CacheEscalaLocal(string carpeta) => _carpeta = carpeta;

    private string Ruta(int idUsuario) => Path.Combine(_carpeta, $"escala-{idUsuario}.json");

    public IReadOnlyDictionary<string, double> Leer(int idUsuario)
    {
        try
        {
            var ruta = Ruta(idUsuario);
            if (!File.Exists(ruta)) return ReadOnlyEmpty;

            var contenido = File.ReadAllText(ruta);
            var leido = JsonSerializer.Deserialize<Dictionary<string, double>>(contenido);

            return leido is null ? ReadOnlyEmpty : leido;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // La caché es aceleración: si no se puede leer, la app arranca sin escala
            // propia y la consulta a Supabase la repuebla.
            Serilog.Log.Warning(ex, "[CacheEscalaLocal] No se pudo leer la caché de escala del usuario {IdUsuario}", idUsuario);
            return ReadOnlyEmpty;
        }
    }

    public void Guardar(int idUsuario, IReadOnlyDictionary<string, double> factoresPorAmbito)
    {
        try
        {
            Directory.CreateDirectory(_carpeta);
            File.WriteAllText(
                Ruta(idUsuario),
                JsonSerializer.Serialize(factoresPorAmbito));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Serilog.Log.Warning(ex, "[CacheEscalaLocal] No se pudo escribir la caché de escala del usuario {IdUsuario}", idUsuario);
        }
    }

    private static readonly IReadOnlyDictionary<string, double> ReadOnlyEmpty =
        new Dictionary<string, double>();
}
