using ClosedXML.Excel;

namespace ImeCrawler.Api.Services;

/// <summary>Renders parsed offerings to a styled, RTL .xlsx workbook.</summary>
public sealed class ExcelReportExporter
{
    private static readonly string[] Headers = { "نام کالا", "نماد", "تالار", "کارگزار", "کد عرضه" };

    public byte[] Build(string title, IEnumerable<ParsedOffer> offers)
    {
        var rows = offers.ToList();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("عرضه‌ها");
        ws.RightToLeft = true;

        // Title row (merged across all columns).
        ws.Cell(1, 1).Value = title;
        ws.Range(1, 1, 1, Headers.Length).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"تعداد ردیف‌ها: {rows.Count}";

        // Header row.
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = ws.Cell(3, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1479B8");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data rows.
        var r = 4;
        foreach (var o in rows)
        {
            ws.Cell(r, 1).Value = o.ProductName ?? "";
            ws.Cell(r, 2).Value = o.Symbol ?? "";
            ws.Cell(r, 3).Value = o.Talar ?? "";
            ws.Cell(r, 4).Value = o.Broker ?? "";
            ws.Cell(r, 5).Value = o.SourcePk?.ToString() ?? "";
            r++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
