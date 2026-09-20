using CapaAplicacion.Preferencias.Interfaces;
using CapaDatos;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BimboProyecto.Tests.Preferencias;

/// <summary>
/// Una dependencia sin registrar no rompe la compilación: revienta en runtime, al
/// resolver la ventana principal, o sea en el primer login después del deploy. Estos
/// tests cierran ese hueco para las piezas de preferencias.
/// </summary>
public sealed class RegistroDePreferenciasTests
{
    private static ServiceProvider Contenedor() =>
        new ServiceCollection().AddDataLayer().BuildServiceProvider();

    [Fact]
    public void El_repositorio_de_preferencias_esta_registrado()
    {
        using var proveedor = Contenedor();
        Assert.NotNull(proveedor.GetService<IPreferenciasUsuarioRepository>());
    }

    [Fact]
    public void La_cache_local_de_escala_esta_registrada()
    {
        using var proveedor = Contenedor();
        Assert.NotNull(proveedor.GetService<ICacheEscalaLocal>());
    }

    [Fact]
    public void La_cache_local_de_escala_es_una_sola_instancia()
    {
        // Singleton a propósito: no tiene estado propio y evita rearmar rutas en cada
        // lectura. Si alguien la pasara a Transient, este test lo avisa.
        using var proveedor = Contenedor();

        Assert.Same(
            proveedor.GetService<ICacheEscalaLocal>(),
            proveedor.GetService<ICacheEscalaLocal>());
    }
}
