namespace ImeCrawler.Api.Data.Entities;

public sealed class ImeOffer
{
	public long Id { get; set; }

	public DateOnly Day { get; set; }

	public int MainGroupId { get; set; }
	public string MainGroupName { get; set; } = "";

	public long? SourcePk { get; set; }          // bArzehRadifPK when available
	public string? ProductName { get; set; }     // xKalaNamadKala
	public string? Symbol { get; set; }          // bArzehRadifNamadKala
	public string? Talar { get; set; }           // Talar
	public string? Broker { get; set; }          // cBrokerSpcName

	// Keep raw for traceability and re-parsing
	public string RawPayload { get; set; } = "";

	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
