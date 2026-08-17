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
            var sheet = workbook.AddWorksheet(SafeSheetName(report.SheetName));

            MemoryStream? logoStream = null;
            if (report.Branding.LogoBytes is { Length: > 0 })
            {
                logoStream = new MemoryStream(report.Branding.LogoBytes);
                var picture = sheet.AddPicture(logoStream).MoveTo(sheet.Cell(1, 1));
                picture.Height = 36;
            }

            sheet.Cell(1, 1).Value = report.Branding.CompanyName;
            sheet.Range(1, 1, 1, Math.Max(1, report.Columns.Count)).Merge();
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 16;
            sheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
            sheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF");
            sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            sheet.Cell(2, 1).Value = report.Title;
            sheet.Cell(2, 1).Style.Font.Bold = true;
            sheet.Cell(2, 1).Style.Font.FontSize = 14;
            int metadataRow = 4;
            WriteMetadata(sheet, metadataRow++, "Generado", report.GeneratedAt.ToString("dd/MM/yyyy HH:mm:ss"));
            WriteMetadata(sheet, metadataRow++, "Registros", report.Rows.Count.ToString());
            WriteMetadata(sheet, metadataRow++, "Usuario", $"{report.Author.NombreEmpleado} {report.Author.ApellidoEmpleado}".Trim());
            foreach (var filter in report.Filters) WriteMetadata(sheet, metadataRow++, filter.Label, filter.Value);

            int headerRow = metadataRow + 1;
            for (int column = 0; column < report.Columns.Count; column++)
                sheet.Cell(headerRow, column + 1).Value = report.Columns[column].Header;

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
                {
                    var cell = sheet.Cell(headerRow + row + 1, column + 1);
                    SetValue(cell, column < values.Count ? values[column] : null);
                    if (!string.IsNullOrWhiteSpace(report.Columns[column].NumberFormat))
                        cell.Style.NumberFormat.Format = ExcelFormat(report.Columns[column].NumberFormat!);
                }
            }

            int totalsRow = headerRow + report.Rows.Count + 2;
            foreach (var total in report.Totals)
            {
                sheet.Cell(totalsRow, 1).Value = total.Label;
                sheet.Cell(totalsRow, 1).Style.Font.Bold = true;
                SetValue(sheet.Cell(totalsRow, 2), total.Value);
                if (!string.IsNullOrWhiteSpace(total.NumberFormat)) sheet.Cell(totalsRow, 2).Style.NumberFormat.Format = ExcelFormat(total.NumberFormat!);
                totalsRow++;
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
            logoStream?.Dispose();
            return Task.FromResult(Result<byte[]>.Ok(stream.ToArray()));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Task.FromResult(Result<byte[]>.Fail($"Generar Excel: {ex.Message}"));
        }
    }

    private static string SafeSheetName(string value)
    {
        string safe = string.Concat((string.IsNullOrWhiteSpace(value) ? "Reporte" : value).Take(31));
        foreach (char invalid in new[] { '/', '\\', '?', '*', '[', ']', ':' }) safe = safe.Replace(invalid, '-');
        return safe;
    }

    private static string ExcelFormat(string format) => format switch
    {
        "N3" => "#,##0.000", "N2" => "#,##0.00", "P2" => "0.00%",
        "dd/MM/yyyy" => "dd/mm/yyyy", "dd/MM/yyyy HH:mm" => "dd/mm/yyyy hh:mm",
        _ => format,
    };

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Value = string.Empty; break;
            case DateTime v: cell.Value = v; break;
            case decimal v: cell.Value = v; break;
            case double v: cell.Value = v; break;
            case int v: cell.Value = v; break;
            case long v: cell.Value = v; break;
            case bool v: cell.Value = v; break;
            default: cell.Value = value.ToString() ?? string.Empty; break;
        }
    }

    private static void WriteMetadata(IXLWorksheet sheet, int row, string label, string value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = value;
    }
}
