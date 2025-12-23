namespace ImeCrawler.Api.Services;

public interface IHtmlToImage
{
    Task<byte[]> RenderPngAsync(string html, CancellationToken ct);
}
