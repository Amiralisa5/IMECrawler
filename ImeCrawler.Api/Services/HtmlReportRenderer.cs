using System.Text;

namespace ImeCrawler.Api.Services;

public sealed class HtmlReportRenderer
{
    public string Render(string title, IEnumerable<ParsedOffer> offers)
    {
        var rows = offers.ToList();

        var sb = new StringBuilder();
        sb.Append("""
<!doctype html>
<html lang="fa" dir="rtl">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<style>
  body{font-family:Tahoma,Arial,sans-serif;background:#f6f7fb;margin:24px;}
  .card{background:white;border:1px solid #e6e8ef;border-radius:12px;padding:16px;box-shadow:0 2px 10px rgba(0,0,0,.05);}
  h1{font-size:18px;margin:0 0 12px 0;color:#1f2a44;}
  .meta{color:#6b7280;font-size:12px;margin-bottom:12px;}
  table{width:100%;border-collapse:collapse;font-size:12px;}
  th,td{border:1px solid #e6e8ef;padding:8px;vertical-align:top;}
  th{background:#1479b8;color:#fff;position:sticky;top:0;}
  tr:nth-child(even){background:#fafbff;}
  .small{color:#6b7280;font-size:11px;}
</style>
</head>
<body>
""");

        sb.Append($"""
<div class="card">
  <h1>{Escape(title)}</h1>
  <div class="meta">تعداد ردیف‌ها: {rows.Count}</div>
  <table>
    <thead>
      <tr>
        <th>نام کالا</th>
        <th>نماد</th>
        <th>تالار</th>
        <th>کارگزار</th>
        <th>کد عرضه</th>
      </tr>
    </thead>
    <tbody>
""");

        foreach (var r in rows)
        {
            sb.Append("<tr>");
            sb.Append($"<td>{Escape(r.ProductName)}</td>");
            sb.Append($"<td>{Escape(r.Symbol)}</td>");
            sb.Append($"<td>{Escape(r.Talar)}</td>");
            sb.Append($"<td>{Escape(r.Broker)}</td>");
            sb.Append($"<td class='small'>{(r.SourcePk?.ToString() ?? "")}</td>");
            sb.Append("</tr>");
        }

        sb.Append("""
    </tbody>
  </table>
</div>
</body>
</html>
""");

        return sb.ToString();
    }

    private static string Escape(string? s)
        => System.Net.WebUtility.HtmlEncode(s ?? "");
}
