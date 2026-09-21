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

        Assert.Equal("Bimbo Honduras", sheet.Cell(1, 1).GetString());
        Assert.Equal("Reporte de Bitácora", sheet.Cell(2, 1).GetString());
        var correoLabel = sheet.CellsUsed().Single(c => c.GetString() == "Correo usuario");
        Assert.Equal("usuario@bimbo.test", sheet.Cell(correoLabel.Address.RowNumber, 2).GetString());
        var rolLabel = sheet.CellsUsed().Single(c => c.GetString() == "Rol");
        Assert.Equal("Administrador", sheet.Cell(rolLabel.Address.RowNumber, 2).GetString());
        Assert.DoesNotContain(sheet.CellsUsed(), c => c.GetString() is "Emanuel" or "Martínez");
        var header = sheet.CellsUsed().Single(c => c.GetString() == "FECHA / HORA");
        Assert.Equal("usuario.registrado", sheet.Cell(header.Address.RowNumber + 1, 2).GetString());
        Assert.Equal("Detalle de prueba", sheet.Cell(header.Address.RowNumber + 1, 6).GetString());
        Assert.Equal(XLDataType.DateTime, sheet.Cell(header.Address.RowNumber + 1, 1).DataType);
        var desdeLabel = sheet.CellsUsed().Single(c => c.GetString() == "Desde");
        Assert.True(desdeLabel.Address.RowNumber > header.Address.RowNumber + 1);
        Assert.Equal("01/08/2026", sheet.Cell(desdeLabel.Address.RowNumber, desdeLabel.Address.ColumnNumber + 1).GetString());
        var hastaLabel = sheet.CellsUsed().Single(c => c.GetString() == "Hasta");
        Assert.Equal(desdeLabel.Address.RowNumber, hastaLabel.Address.RowNumber);
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

    [Fact]
    public async Task Excel_RespetaCantidadSemanticaCuandoUnRegistroUsaVariasFilas()
    {
        var doc = new TabularReportDto
        {
            Title = "Detalle del registro de bitácora #2",
            SheetName = "Detalle bitácora",
            GeneratedAt = new DateTime(2026, 9, 3, 10, 30, 0),
            RecordCount = 1,
            Columns = ["CAMPO", "VALOR"],
            Rows =
            [
                new object?[] { "USUARIO", "Fernando José" },
                new object?[] { "ACCIÓN", "Modificación de fabricante" },
                new object?[] { "DETALLE", "Nombre: Actual" },
            ],
        };

        var result = await new ExcelReportStrategy().GenerateAsync(doc);

        Assert.True(result.Success, result.Error);
        using var stream = new MemoryStream(result.Value!);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Detalle bitácora");
        var registros = sheet.CellsUsed().Single(c => c.GetString() == "Registros");
        Assert.Equal("1", sheet.Cell(registros.Address.RowNumber, 2).GetString());
        Assert.Contains(sheet.CellsUsed(), c => c.GetString() == "Fernando José");
    }

    private static TabularReportDto CrearReporte() => new()
    {
        Title = "Reporte de Bitácora",
        SheetName = "Bitácora",
        Branding = new ReportBrandingDto { CompanyName = "Bimbo Honduras" },
        GeneratedAt = new DateTime(2026, 8, 16, 10, 30, 0),
        Author = new ReportAuthorDto
        {
            Email = "usuario@bimbo.test",
            Rol = "Administrador",
        },
        Columns = [new("FECHA / HORA", "dd/MM/yyyy HH:mm"), "USUARIO", "MÓDULO", "ACCIÓN", "CAMPO AFECTADO", "DETALLE"],
        Rows =
        [
            new object?[] { new DateTime(2026,8,16,10,0,0), "usuario.registrado", "Productos", "Crear Producto", "Nombre", "Detalle de prueba" },
        ],
        FooterMetadata = [new("Desde", "01/08/2026"), new("Hasta", "16/08/2026")],
    };

    [Fact]
    public async Task Pesaje_GeneraReportePesadoInsumosBes_EnPdfYExcel()
    {
        var doc = new TabularReportDto
        {
            Title = "Pesado de Insumos BES",
            SheetName = "Pesaje de Insumos",
            Branding = new ReportBrandingDto { CompanyName = "Bimbo Honduras" },
            GeneratedAt = new DateTime(2026, 8, 21, 14, 0, 0),
            Author = new ReportAuthorDto
            {
                Email = "operario@bimbo.test",
                Rol = "Pesaje",
            },
            Filters = [new("Placa del camión", "HAD-1234"), new("Proveedor", "HARINERA S.A.")],
            Columns =
            [
                new("FECHA ASIG.", "dd/MM/yyyy", 2.2),
                new("PLACA", null, 1.8),
                new("PRODUCTO", null, 4.2),
                new("PROVEEDOR", null, 3.2),
                new("BULTOS (APROX)", "N2", 2.2),
                new("PESO MANIFESTADO", "N2", 2.5),
                new("PESO BRUTO", "N2", 2.2),
                new("PESO TARA", "N2", 2.0),
                new("PESO RECIBIDO", "N2", 2.4),
                new("DIF. (KG)", "N2", 2.0),
                new("DIF. (%)", "P2", 1.8),
            ],
            Rows =
            [
                new object?[]
                {
                    "21/08/2026", "HAD-1234", "HAR-001 - HARINA DE TRIGO ESPECIAL", "HARINERA S.A.",
                    50.0, 2500.0, 2620.0, 110.0, 2510.0, 10.0, 0.004
                }
            ],
            Totals =
            [
                new("Total Bultos Recibidos", 50.0, "N2"),
                new("Total Peso Manifestado", 2500.0, "N2"),
                new("Total Peso Bruto", 2620.0, "N2"),
                new("Total Peso Tara", 110.0, "N2"),
                new("Total Peso Recibido (Neto)", 2510.0, "N2"),
                new("Diferencia Total (KG)", 10.0, "N2"),
                new("Diferencia Total (%)", 0.004, "P2"),
            ]
        };

        var service = new ReportGeneratorService([new PdfReportStrategy(), new ExcelReportStrategy()]);

        var resPdf = await service.GenerateAsync(doc, ReportFormat.Pdf);
        Assert.True(resPdf.Success, resPdf.Error);
        Assert.NotNull(resPdf.Value);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(resPdf.Value, 0, 4));

        var resXls = await service.GenerateAsync(doc, ReportFormat.Excel);
        Assert.True(resXls.Success, resXls.Error);
        Assert.NotNull(resXls.Value);
        Assert.NotEmpty(resXls.Value);
    }
}

