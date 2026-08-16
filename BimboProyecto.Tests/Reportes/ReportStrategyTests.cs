using System.Text;
using CapaAplicacion.Reportes;
using CapaAplicacion.Reportes.Dtos;
using CapaDatos.Reportes;
using CapaDominio.Reportes;
using ClosedXML.Excel;
using Xunit;

namespace BimboProyecto.Tests.Reportes;

public sealed class ReportStrategyTests
{
    [Fact]
    public async Task Pdf_GeneraDocumentoConFirmaValida()
    {
        var result = await new PdfReportStrategy().GenerateAsync(CrearReporte());

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.Length > 4);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(result.Value, 0, 4));
    }

    [Fact]
    public async Task Excel_GeneraMetadatosYFilasSeleccionadas()
    {
        var result = await new ExcelReportStrategy().GenerateAsync(CrearReporte());

        Assert.True(result.Success, result.Error);
        using var stream = new MemoryStream(result.Value!);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Bitácora");

        Assert.Equal("Reporte de Bitácora", sheet.Cell(1, 1).GetString());
        Assert.Equal("Correo usuario", sheet.Cell(5, 1).GetString());
        Assert.Equal("usuario@bimbo.test", sheet.Cell(5, 2).GetString());
        Assert.Equal("FECHA / HORA", sheet.Cell(10, 1).GetString());
        Assert.Equal("usuario.registrado", sheet.Cell(11, 2).GetString());
        Assert.Equal("Detalle de prueba", sheet.Cell(11, 6).GetString());
    }

    [Fact]
    public async Task Orquestador_ResuelveLaEstrategiaSolicitada()
    {
        var service = new ReportGeneratorService(
            [new PdfReportStrategy(), new ExcelReportStrategy()]);

        var result = await service.GenerateAsync(CrearReporte(), ReportFormat.Excel);

        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(result.Value!);
    }

    private static TabularReportDto CrearReporte() => new()
    {
        Title = "Reporte de Bitácora",
        GeneratedAt = new DateTime(2026, 8, 16, 10, 30, 0),
        Author = new ReportAuthorDto
        {
            Email = "usuario@bimbo.test",
            NombreEmpleado = "Emanuel",
            ApellidoEmpleado = "Martínez",
            Rol = "Administrador",
        },
        Columns = ["FECHA / HORA", "USUARIO", "MÓDULO", "ACCIÓN", "CAMPO AFECTADO", "DETALLE"],
        Rows =
        [
            new[] { "16/08/2026 10:00", "usuario.registrado", "Productos", "Crear Producto", "Nombre", "Detalle de prueba" },
        ],
    };
}
