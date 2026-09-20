using CapaDatos.Preferencias;
using Xunit;

namespace BimboProyecto.Tests.Preferencias;

/// <summary>
/// La caché local de escala es lo que permite que la app arranque con el tamaño correcto
/// sin esperar a la red. Su contrato es que <b>nunca lanza</b>: un archivo corrupto, un
/// disco sin permisos o un usuario sin preferencias degradan a "sin escala propia", no
/// tumban el arranque. Estos tests trabajan en una carpeta temporal, nunca sobre el
/// perfil real del equipo.
/// </summary>
public sealed class CacheEscalaLocalTests : IDisposable
{
    private const int Usuario = 42;
    private const int OtroUsuario = 43;

    private readonly string _carpeta =
        Path.Combine(Path.GetTempPath(), "BimboEscalaTests_" + Guid.NewGuid().ToString("N"));

    private CacheEscalaLocal Nueva() => new(_carpeta);

    private string RutaDe(int idUsuario) => Path.Combine(_carpeta, $"escala-{idUsuario}.json");

    public void Dispose()
    {
        try { Directory.Delete(_carpeta, recursive: true); } catch { }
    }

    [Fact]
    public void Leer_sin_archivo_devuelve_vacio_y_no_lanza() =>
        Assert.Empty(Nueva().Leer(Usuario));

    [Fact]
    public void Guardar_y_leer_conserva_los_factores_por_ambito()
    {
        var cache = Nueva();
        var factores = new Dictionary<string, double>
        {
            ["1920x1080@1.75"] = 0.80,
            ["1366x768@1"]     = 1.00,
            ["global"]         = 0.90,
        };

        cache.Guardar(Usuario, factores);
        var leido = cache.Leer(Usuario);

        Assert.Equal(3, leido.Count);
        Assert.Equal(0.80, leido["1920x1080@1.75"]);
        Assert.Equal(1.00, leido["1366x768@1"]);
        Assert.Equal(0.90, leido["global"]);
    }

    [Fact]
    public void Cada_usuario_tiene_su_propio_archivo()
    {
        var cache = Nueva();

        cache.Guardar(Usuario,     new Dictionary<string, double> { ["global"] = 0.80 });
        cache.Guardar(OtroUsuario, new Dictionary<string, double> { ["global"] = 1.20 });

        // Es el caso de la terminal de planta: dos operarios sobre la misma cuenta de
        // Windows no deben pisarse la escala.
        Assert.Equal(0.80, cache.Leer(Usuario)["global"]);
        Assert.Equal(1.20, cache.Leer(OtroUsuario)["global"]);
    }

    [Fact]
    public void Guardar_reemplaza_el_contenido_anterior()
    {
        var cache = Nueva();

        cache.Guardar(Usuario, new Dictionary<string, double> { ["global"] = 0.80, ["1920x1080@1"] = 0.95 });
        cache.Guardar(Usuario, new Dictionary<string, double> { ["global"] = 1.10 });

        var leido = cache.Leer(Usuario);
        Assert.Single(leido);
        Assert.Equal(1.10, leido["global"]);
    }

    [Fact]
    public void Leer_un_archivo_corrupto_devuelve_vacio_y_no_lanza()
    {
        Directory.CreateDirectory(_carpeta);
        File.WriteAllText(RutaDe(Usuario), "{ esto no es json");

        Assert.Empty(Nueva().Leer(Usuario));
    }

    [Fact]
    public void Leer_un_json_valido_del_tipo_equivocado_devuelve_vacio_y_no_lanza()
    {
        Directory.CreateDirectory(_carpeta);
        File.WriteAllText(RutaDe(Usuario), "[1, 2, 3]");

        Assert.Empty(Nueva().Leer(Usuario));
    }

    [Fact]
    public void Guardar_crea_la_carpeta_si_no_existe()
    {
        Assert.False(Directory.Exists(_carpeta));

        Nueva().Guardar(Usuario, new Dictionary<string, double> { ["global"] = 0.85 });

        Assert.True(File.Exists(RutaDe(Usuario)));
    }

    [Fact]
    public void El_factor_se_escribe_con_punto_decimal()
    {
        // El archivo es JSON: si la cultura del equipo metiera una coma, volver a leerlo
        // fallaría en silencio y el usuario perdería su escala.
        Nueva().Guardar(Usuario, new Dictionary<string, double> { ["global"] = 0.85 });

        Assert.Contains("0.85", File.ReadAllText(RutaDe(Usuario)));
    }
}
