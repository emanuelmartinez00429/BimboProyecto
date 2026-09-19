using System.Security.Cryptography;
using System.Text;
using CapaDatos.Preferencias;
using Xunit;

namespace BimboProyecto.Tests.Auth;

/// <summary>
/// «Recordar usuario» guarda el correo cifrado con DPAPI en %LOCALAPPDATA%. El correo es
/// también el usuario de login: estos tests fijan que nunca quede en claro en disco.
/// Trabajan en una carpeta temporal propia — nunca sobre el perfil real del equipo.
/// </summary>
public sealed class PreferenciasInicioSesionServiceTests : IDisposable
{
    private const string Correo = "operario.anden@bimbo.com";

    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "BimboPrefsTests_" + Guid.NewGuid().ToString("N"));
    private string Carpeta    => Path.Combine(_raiz, "Local");
    private string RutaLegada => Path.Combine(_raiz, "Roaming", "ultimo_usuario.txt");
    private string RutaBlob   => Path.Combine(Carpeta, "ultimo_usuario.bin");

    private PreferenciasInicioSesionService Nuevo() => new(Carpeta, RutaLegada);

    public void Dispose()
    {
        try { Directory.Delete(_raiz, recursive: true); } catch { }
    }

    [Fact]
    public void Guardar_y_leer_devuelve_el_mismo_correo()
    {
        if (!OperatingSystem.IsWindows()) return;

        Nuevo().GuardarUltimoUsuario("  " + Correo + " ");

        Assert.Equal(Correo, Nuevo().ObtenerUltimoUsuario());
    }

    [Fact]
    public void El_archivo_no_contiene_el_correo_en_claro()
    {
        if (!OperatingSystem.IsWindows()) return;

        Nuevo().GuardarUltimoUsuario(Correo);

        var bytes = File.ReadAllBytes(RutaBlob);
        Assert.DoesNotContain(Correo, Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain(Correo, Encoding.Unicode.GetString(bytes));
        Assert.False(File.Exists(RutaBlob + ".tmp"));
    }

    [Fact]
    public void Guardar_vacio_olvida_el_usuario()
    {
        if (!OperatingSystem.IsWindows()) return;

        var svc = Nuevo();
        svc.GuardarUltimoUsuario(Correo);
        svc.GuardarUltimoUsuario(null);

        Assert.False(File.Exists(RutaBlob));
        Assert.Null(svc.ObtenerUltimoUsuario());
    }

    [Fact]
    public void Sin_archivo_devuelve_null()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Null(Nuevo().ObtenerUltimoUsuario());
    }

    [Fact]
    public void Migra_el_texto_plano_viejo_y_lo_borra()
    {
        if (!OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(Path.GetDirectoryName(RutaLegada)!);
        File.WriteAllText(RutaLegada, Correo, Encoding.UTF8);

        var leido = Nuevo().ObtenerUltimoUsuario();

        Assert.Equal(Correo, leido);
        Assert.False(File.Exists(RutaLegada));   // el correo en claro ya no está en disco
        Assert.True(File.Exists(RutaBlob));
    }

    [Fact]
    public void Olvidar_tambien_borra_el_texto_plano_viejo()
    {
        if (!OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(Path.GetDirectoryName(RutaLegada)!);
        File.WriteAllText(RutaLegada, Correo, Encoding.UTF8);

        Nuevo().GuardarUltimoUsuario(null);

        Assert.False(File.Exists(RutaLegada));
    }

    [Fact]
    public void Si_no_se_puede_cifrar_la_migracion_conserva_el_texto_plano()
    {
        if (!OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(Path.GetDirectoryName(RutaLegada)!);
        File.WriteAllText(RutaLegada, Correo, Encoding.UTF8);
        // Un archivo con el nombre de la carpeta de destino: CreateDirectory falla y no hay dónde cifrar.
        Directory.CreateDirectory(_raiz);
        File.WriteAllText(Carpeta, "bloqueo");

        var leido = Nuevo().ObtenerUltimoUsuario();

        Assert.Equal(Correo, leido);             // el login igual lo muestra
        Assert.True(File.Exists(RutaLegada));    // no se pierde: se reintenta en el próximo arranque
    }

    [Fact]
    public void Olvidar_borra_el_temporal_de_una_escritura_cortada()
    {
        if (!OperatingSystem.IsWindows()) return;

        var svc = Nuevo();
        svc.GuardarUltimoUsuario(Correo);
        // Simula un corte entre la escritura del temporal y el reemplazo.
        File.WriteAllBytes(RutaBlob + ".tmp", File.ReadAllBytes(RutaBlob));

        svc.GuardarUltimoUsuario(null);

        Assert.False(File.Exists(RutaBlob));
        Assert.False(File.Exists(RutaBlob + ".tmp"));
    }

    [Fact]
    public void Un_blob_que_no_se_puede_descifrar_se_descarta()
    {
        if (!OperatingSystem.IsWindows()) return;

        // Simula un archivo copiado de otra cuenta/equipo: bytes que DPAPI no reconoce.
        Directory.CreateDirectory(Carpeta);
        File.WriteAllBytes(RutaBlob, RandomNumberGenerator.GetBytes(64));

        Assert.Null(Nuevo().ObtenerUltimoUsuario());
        Assert.False(File.Exists(RutaBlob));
    }
}
