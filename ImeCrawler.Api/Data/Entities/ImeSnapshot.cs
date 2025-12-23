namespace ImeCrawler.Api.Data.Entities;

public sealed class ImeSnapshot
{
    public long Id { get; set; }

    public DateOnly Day { get; set; }
    public int MainGroupId { get; set; }
    public string MainGroupName { get; set; } = "";

    // Stored in wwwroot, served by app.UseStaticFiles()
    public string ImageUrl { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
