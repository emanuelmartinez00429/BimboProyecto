using CapaAplicacion.Bitacora;
using CapaAplicacion.Bitacora.Dtos;
using Xunit;

namespace BimboProyecto.Tests.Bitacora;

public sealed class BitacoraDetalleTests
{
    [Fact]
    public void CrearCampos_UsaEtiquetasAmigablesYFormatoVisible()
    {
        var registro = new BitacoraDto
        {
            IdBitacora = 2,
            FechaHora = new DateTime(2026, 9, 3, 10, 30, 0),
            AliasUsuario = "Fernando José",
            NombreAccion = "Modificación de fabricante",
            NombreModulo = "Fabricantes",
            CampoAfectado = "Fabricante",
            EstadoAnterior = "Nombre: Anterior",
            EstadoActual = "Nombre: Actual",
            CampoExtra = "Origen: catálogo",
            TablaAfectada = "fabricantes",
            IdRegistroAfectado = 14,
        };

        var campos = BitacoraDetalle.CrearCampos(registro);

        Assert.Equal("2", Valor(campos, "REGISTRO DE BITÁCORA"));
        Assert.Equal("03/09/2026 10:30", Valor(campos, "FECHA / HORA"));
        Assert.Equal("Fernando José", Valor(campos, "USUARIO"));
        Assert.Equal("Nombre: Actual", Valor(campos, "DETALLE"));
        Assert.DoesNotContain(campos, c => c.Etiqueta.Contains('_'));
    }

    [Fact]
    public void CrearCampos_ConvierteNulosYVaciosEnSinInformacion()
    {
        var campos = BitacoraDetalle.CrearCampos(new BitacoraDto
        {
            IdBitacora = 7,
            AliasUsuario = "  ",
            CampoExtra = null,
            IdRegistroAfectado = 0,
        });

        Assert.Equal(BitacoraDetalle.SinInformacion, Valor(campos, "FECHA / HORA"));
        Assert.Equal(BitacoraDetalle.SinInformacion, Valor(campos, "USUARIO"));
        Assert.Equal(BitacoraDetalle.SinInformacion, Valor(campos, "INFORMACIÓN ADICIONAL"));
        Assert.Equal(BitacoraDetalle.SinInformacion, Valor(campos, "REFERENCIA DEL REGISTRO"));
    }

    [Fact]
    public void CrearCamposSinHero_OmiteRegistroYFechaHora()
    {
        var registro = new BitacoraDto
        {
            IdBitacora = 705,
            FechaHora = new DateTime(2026, 9, 20, 22, 56, 0),
            AliasUsuario = "fbarahona280@gmail.com",
            NombreModulo = "Pesaje",
            NombreAccion = "Registrar Entrada",
        };

        var campos = BitacoraDetalle.CrearCamposSinHero(registro);

        Assert.DoesNotContain(campos, c => c.Etiqueta == "REGISTRO DE BITÁCORA");
        Assert.DoesNotContain(campos, c => c.Etiqueta == "FECHA / HORA");
        Assert.Equal("fbarahona280@gmail.com", Valor(campos, "USUARIO"));
        Assert.Equal("Pesaje", Valor(campos, "MÓDULO"));
        Assert.Equal(9, campos.Count);
    }

    private static string Valor(IReadOnlyList<BitacoraDetalleCampo> campos, string etiqueta) =>
        Assert.Single(campos, c => c.Etiqueta == etiqueta).Valor;
}
