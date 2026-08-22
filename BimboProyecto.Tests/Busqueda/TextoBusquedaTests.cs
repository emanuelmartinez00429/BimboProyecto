using CapaAplicacion.Common;
using Xunit;

namespace BimboProyecto.Tests.Busqueda;

/// <summary>
/// <see cref="TextoBusqueda.Normalizar"/> tiene que hacer lo MISMO que la función
/// <c>public.sin_tildes()</c> de Postgres, que es la que alimenta las columnas
/// generadas de búsqueda (<c>busqueda_producto</c> y compañía).
/// <para/>
/// Si las dos divergen, el término que manda el cliente deja de corresponderse con
/// lo que hay guardado y la búsqueda devuelve de menos <b>sin dar ningún error</b> —
/// no hay excepción ni log que lo delate, solo resultados que faltan. Por eso el
/// invariante se fija acá: es la red que avisa si alguien toca un lado sin el otro.
/// </summary>
public sealed class TextoBusquedaTests
{
    [Theory]
    [InlineData("AZÚCAR", "azucar")]
    [InlineData("azúcar", "azucar")]
    [InlineData("AZUCAR", "azucar")]
    [InlineData("Mantequilla", "mantequilla")]
    [InlineData("PIÑA", "pina")]
    [InlineData("Café Molido", "cafe molido")]
    public void Normalizar_QuitaTildesYBajaAMinusculas(string entrada, string esperado) =>
        Assert.Equal(esperado, TextoBusqueda.Normalizar(entrada));

    /// <summary>
    /// El caso que motivó migrar el buscador universal a la columna generada:
    /// escrito de cualquier forma, el término normalizado es el mismo, así que el
    /// ILIKE contra la columna encuentra el producto.
    /// </summary>
    [Fact]
    public void Normalizar_ConvergeEscribaComoEscribaElUsuario()
    {
        var formas = new[] { "AZÚCAR", "azúcar", "Azucar", "AZUCAR", "azucar" };
        Assert.All(formas, f => Assert.Equal("azucar", TextoBusqueda.Normalizar(f)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Normalizar_NuncaDevuelveNull(string? entrada) =>
        Assert.Equal(string.Empty, TextoBusqueda.Normalizar(entrada));

    [Theory]
    [InlineData("AZÚCAR MORENA", "azucar")]
    [InlineData("Café", "CAFE")]
    [InlineData("piña colada", "PIÑA")]
    public void Contiene_IgnoraTildesYMayusculas(string texto, string termino) =>
        Assert.True(TextoBusqueda.Contiene(texto, termino));

    [Fact]
    public void Contiene_TerminoVacioMatcheaTodo() =>
        Assert.True(TextoBusqueda.Contiene("cualquier cosa", ""));

    [Fact]
    public void Contiene_NoMatcheaLoQueNoEsta() =>
        Assert.False(TextoBusqueda.Contiene("AZÚCAR", "harina"));
}
