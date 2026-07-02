namespace ImeCrawler.Api.Data.Entities;

public sealed class ImeSnapshot
{
    public long Id { get; set; }

    public DateOnly Day { get; set; }
    public int MainGroupId { get; set; }
    public string MainGroupName { get; set; } = "";

    // All stored under wwwroot and served by app.UseStaticFiles().
    public string ImageUrl { get; set; } = "";   // PNG
    public string? PdfUrl { get; set; }           // PDF report
    public string? ExcelUrl { get; set; }         // .xlsx export

    /// <summary>Number of offerings captured for this snapshot (after hall filtering).</summary>
    public int OfferCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
