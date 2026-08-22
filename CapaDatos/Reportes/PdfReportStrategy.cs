using CapaAplicacion.Common;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using CapaDominio.Reportes;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace CapaDatos.Reportes;

public sealed class PdfReportStrategy : IReportStrategy
{
    public ReportFormat SupportedFormat => ReportFormat.Pdf;

    public Task<Result<byte[]>> GenerateAsync(TabularReportDto report, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            // PDFsharp Core solo consulta las fuentes instaladas cuando se habilita
            // explícitamente su resolvedor de Windows y antes del primer uso.
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;

            var document = new Document();
            document.Info.Title = report.Title;
            document.Info.Author = report.Author.Email;

            var normal = document.Styles[StyleNames.Normal]!;
            normal.Font.Name = "Arial";
            normal.Font.Size = 8;

            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A4;
            section.PageSetup.Orientation = report.Landscape ? Orientation.Landscape : Orientation.Portrait;
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);

            if (report.Branding.LogoBytes is { Length: > 0 })
            {
                var logo = section.AddImage("base64:" + Convert.ToBase64String(report.Branding.LogoBytes));
                logo.LockAspectRatio = true;
                logo.Height = Unit.FromCentimeter(1.2);
                logo.Left = MigraDoc.DocumentObjectModel.Shapes.ShapePosition.Center;
            }

            var company = section.AddParagraph(report.Branding.CompanyName);
            company.Format.Font.Name = "Arial";
            company.Format.Font.Size = 10;
            company.Format.Font.Bold = true;
            company.Format.Alignment = ParagraphAlignment.Center;

            var title = section.AddParagraph(report.Title);
            title.Format.Font.Name = "Arial";
            title.Format.Font.Size = 18;
            title.Format.Font.Bold = true;
            title.Format.Font.Color = Color.FromRgb(30, 64, 175);
            title.Format.SpaceAfter = Unit.FromPoint(5);
            title.Format.Alignment = ParagraphAlignment.Center;

            var generated = section.AddParagraph($"Generado: {report.GeneratedAt:dd/MM/yyyy HH:mm:ss}   |   Registros: {report.Rows.Count}");
            generated.Format.Font.Size = 9;
            generated.Format.SpaceAfter = Unit.FromPoint(6);

            AddAuthorLine(section, "Correo usuario", report.Author.Email);
            AddAuthorLine(section, "Nombre empleado", report.Author.NombreEmpleado);
            AddAuthorLine(section, "Apellido empleado", report.Author.ApellidoEmpleado);
            AddAuthorLine(section, "Rol", report.Author.Rol);
            foreach (var filter in report.Filters) AddAuthorLine(section, filter.Label, filter.Value);
            section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(3);

            var table = section.AddTable();
            table.Borders.Color = Color.FromRgb(203, 213, 225);
            table.Borders.Width = 0.5;
            table.Rows.LeftIndent = 0;

            double available = report.Landscape ? 25.7 : 18.6;
            double[] widths = report.Columns.Any(c => c.WidthCm.HasValue)
                ? report.Columns.Select(c => c.WidthCm ?? available / Math.Max(1, report.Columns.Count)).ToArray()
                : Enumerable.Repeat(available / Math.Max(1, report.Columns.Count), report.Columns.Count).ToArray();
            foreach (double width in widths)
                table.AddColumn(Unit.FromCentimeter(width));

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.Format.Font.Bold = true;
            header.Format.Font.Color = Colors.White;
            header.Shading.Color = Color.FromRgb(30, 64, 175);
            header.VerticalAlignment = VerticalAlignment.Center;
            header.TopPadding = Unit.FromPoint(5);
            header.BottomPadding = Unit.FromPoint(5);
            for (int i = 0; i < report.Columns.Count; i++)
            {
                var p = header.Cells[i].AddParagraph(report.Columns[i].Header);
                p.Format.Alignment = ParagraphAlignment.Center;
            }

            foreach (var values in report.Rows)
            {
                ct.ThrowIfCancellationRequested();
                var row = table.AddRow();
                row.VerticalAlignment = VerticalAlignment.Center;
                row.TopPadding = Unit.FromPoint(4);
                row.BottomPadding = Unit.FromPoint(4);
                for (int i = 0; i < report.Columns.Count; i++)
                {
                    string value = i < values.Count ? FormatValue(values[i], report.Columns[i].NumberFormat) : string.Empty;
                    var p = row.Cells[i].AddParagraph(value);

                    bool isDate = report.Columns[i].NumberFormat?.Contains("dd") == true;
                    bool isNumber = !string.IsNullOrWhiteSpace(report.Columns[i].NumberFormat) && !isDate;

                    p.Format.Alignment = isNumber
                        ? ParagraphAlignment.Right
                        : (isDate || i == 1 ? ParagraphAlignment.Center : ParagraphAlignment.Left);
                }
            }

            section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);

            foreach (var total in report.Totals)
            {
                var paragraph = section.AddParagraph();
                paragraph.Format.Alignment = ParagraphAlignment.Right;
                paragraph.Format.SpaceAfter = Unit.FromPoint(2);
                paragraph.AddFormattedText($"{total.Label}: ", TextFormat.Bold);
                paragraph.AddText(FormatValue(total.Value, total.NumberFormat));
            }

            var footer = section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = 8;
            footer.AddText("Página ");
            footer.AddPageField();
            footer.AddText(" de ");
            footer.AddNumPagesField();

            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();
            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return Task.FromResult(Result<byte[]>.Ok(stream.ToArray()));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Task.FromResult(Result<byte[]>.Fail($"Generar PDF: {ex.Message}"));
        }
    }

    private static string FormatValue(object? value, string? format) => value switch
    {
        null => "—",
        DateTime date => date.ToString(string.IsNullOrWhiteSpace(format) ? "dd/MM/yyyy HH:mm" : format),
        decimal number when format == "P2" => $"{number * 100:N2}%",
        IFormattable item => item.ToString(format, System.Globalization.CultureInfo.GetCultureInfo("es-HN")),
        _ => value.ToString() ?? string.Empty,
    };

    private static void AddAuthorLine(Section section, string label, string value)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.Font.Size = 9;
        paragraph.Format.SpaceAfter = Unit.FromPoint(2);
        paragraph.AddFormattedText($"{label}: ", TextFormat.Bold);
        paragraph.AddText(value);
    }
}
