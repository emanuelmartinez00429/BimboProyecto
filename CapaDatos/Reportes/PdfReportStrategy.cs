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
            section.PageSetup.Orientation = Orientation.Landscape;
            section.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);

            var title = section.AddParagraph(report.Title);
            title.Format.Font.Name = "Arial";
            title.Format.Font.Size = 18;
            title.Format.Font.Bold = true;
            title.Format.Font.Color = Color.FromRgb(30, 64, 175);
            title.Format.SpaceAfter = Unit.FromPoint(5);

            var generated = section.AddParagraph($"Generado: {report.GeneratedAt:dd/MM/yyyy HH:mm:ss}   |   Registros: {report.Rows.Count}");
            generated.Format.Font.Size = 9;
            generated.Format.SpaceAfter = Unit.FromPoint(6);

            AddAuthorLine(section, "Correo usuario", report.Author.Email);
            AddAuthorLine(section, "Nombre empleado", report.Author.NombreEmpleado);
            AddAuthorLine(section, "Apellido empleado", report.Author.ApellidoEmpleado);
            AddAuthorLine(section, "Rol", report.Author.Rol);
            section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(3);

            var table = section.AddTable();
            table.Borders.Color = Color.FromRgb(203, 213, 225);
            table.Borders.Width = 0.5;
            table.Rows.LeftIndent = 0;

            double[] widths = report.Columns.Count == 6
                ? [3.2, 4.0, 3.2, 3.5, 3.5, 8.3]
                : Enumerable.Repeat(25.7 / Math.Max(1, report.Columns.Count), report.Columns.Count).ToArray();
            foreach (double width in widths)
                table.AddColumn(Unit.FromCentimeter(width));

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.Format.Font.Bold = true;
            header.Format.Font.Color = Colors.White;
            header.Shading.Color = Color.FromRgb(30, 64, 175);
            header.VerticalAlignment = VerticalAlignment.Center;
            for (int i = 0; i < report.Columns.Count; i++)
            {
                header.Cells[i].AddParagraph(report.Columns[i]);
                header.Cells[i].Format.Alignment = ParagraphAlignment.Center;
            }

            foreach (var values in report.Rows)
            {
                ct.ThrowIfCancellationRequested();
                var row = table.AddRow();
                row.VerticalAlignment = VerticalAlignment.Center;
                for (int i = 0; i < report.Columns.Count; i++)
                {
                    string value = i < values.Count ? values[i] : string.Empty;
                    row.Cells[i].AddParagraph(value);
                    row.Cells[i].Format.Alignment = i is 0 or 2
                        ? ParagraphAlignment.Center
                        : ParagraphAlignment.Left;
                }
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

    private static void AddAuthorLine(Section section, string label, string value)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.Font.Size = 9;
        paragraph.AddFormattedText($"{label}: ", TextFormat.Bold);
        paragraph.AddText(value);
    }
}
