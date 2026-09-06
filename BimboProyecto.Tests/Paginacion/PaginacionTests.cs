using System.Linq;
using CapaUI.Core.Controls;
using Xunit;

namespace BimboProyecto.Tests.Paginacion;

public class PaginacionTests
{
    [Fact]
    public void TotalMenorOIgualASiete_DevuelveTodasLasPaginasSinElipsis()
    {
        var resultado = CapaUI.Core.Controls.Paginacion.Calcular(1, 5).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, resultado);
        Assert.DoesNotContain(CapaUI.Core.Controls.Paginacion.Elipsis, resultado);
    }

    [Fact]
    public void TotalExactamenteSiete_DevuelveSietePaginas()
    {
        var resultado = CapaUI.Core.Controls.Paginacion.Calcular(4, 7).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, resultado);
    }

    [Fact]
    public void PaginaAlInicioConTotalMayorASiete_ColocaElipsisAlFinal()
    {
        var resultado = CapaUI.Core.Controls.Paginacion.Calcular(2, 10).ToList();
        Assert.Equal(new[] { 1, 2, 3, CapaUI.Core.Controls.Paginacion.Elipsis, 10 }, resultado);
    }

    [Fact]
    public void PaginaEnMedioConTotalMayorASiete_ColocaElipsisEnAmbosLados()
    {
        var resultado = CapaUI.Core.Controls.Paginacion.Calcular(5, 10).ToList();
        Assert.Equal(new[] { 1, CapaUI.Core.Controls.Paginacion.Elipsis, 4, 5, 6, CapaUI.Core.Controls.Paginacion.Elipsis, 10 }, resultado);
    }

    [Fact]
    public void PaginaAlFinalConTotalMayorASiete_ColocaElipsisAlInicio()
    {
        var resultado = CapaUI.Core.Controls.Paginacion.Calcular(9, 10).ToList();
        Assert.Equal(new[] { 1, CapaUI.Core.Controls.Paginacion.Elipsis, 8, 9, 10 }, resultado);
    }
}
