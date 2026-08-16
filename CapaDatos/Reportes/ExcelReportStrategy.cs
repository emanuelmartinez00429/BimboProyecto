using CapaAplicacion.Common;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using CapaDominio.Reportes;
using ClosedXML.Excel;

namespace CapaDatos.Reportes;

public sealed class ExcelReportStrategy : IReportStrategy
{
    public ReportFormat SupportedFormat => ReportFormat.Excel;

    public Task<Result<byte[]>> GenerateAsync(TabularReportDto report, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("Bitácora");

            sheet.Cell(1, 1).Value = report.Title;
            sheet.Range(1, 1, 1, Math.Max(1, report.Columns.Count)).Merge();
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 18;
            sheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            sheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF");
            sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            WriteMetadata(sheet, 3, "Generado", report.GeneratedAt.ToString("dd/MM/yyyy HH:mm:ss"));
            WriteMetadata(sheet, 4, "Registros", report.Rows.Count.ToString());
            WriteMetadata(sheet, 5, "Correo usuario", report.Author.Email);
            WriteMetadata(sheet, 6, "Nombre empleado", report.Author.NombreEmpleado);
            WriteMetadata(sheet, 7, "Apellido empleado", report.Author.ApellidoEmpleado);
            WriteMetadata(sheet, 8, "Rol", report.Author.Rol);

            const int headerRow = 10;
            for (int column = 0; column < report.Columns.Count; column++)
                sheet.Cell(headerRow, column + 1).Value = report.Columns[column];

            var header = sheet.Range(headerRow, 1, headerRow, report.Columns.Count);
            header.Style.Font.Bold = true;
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF");
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (int row = 0; row < report.Rows.Count; row++)
            {
                ct.ThrowIfCancellationRequested();
                var values = report.Rows[row];
                for (int column = 0; column < report.Columns.Count; column++)
                    sheet.Cell(headerRow + row + 1, column + 1).Value = column < values.Count ? values[column] : string.Empty;
            }

            int lastRow = Math.Max(headerRow, headerRow + report.Rows.Count);
            var dataRange = sheet.Range(headerRow, 1, lastRow, report.Columns.Count);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            dataRange.Style.Alignment.WrapText = true;
            dataRange.SetAutoFilter();

            sheet.SheetView.FreezeRows(headerRow);
            sheet.Columns().AdjustToContents(1, lastRow);
            foreach (var column in sheet.ColumnsUsed())
                if (column.Width > 45) column.Width = 45;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Task.FromResult(Result<byte[]>.Ok(stream.ToArray()));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Task.FromResult(Result<byte[]>.Fail($"Generar Excel: {ex.Message}"));
        }
    }

    private static void WriteMetadata(IXLWorksheet sheet, int row, string label, string value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = value;
    }
}
