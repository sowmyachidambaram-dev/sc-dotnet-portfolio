using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using GoogleDocParser.Models;

namespace GoogleDocParser.Services;

/// <summary>
/// Fetches a publicly-published Google Doc and parses its first HTML table into
/// <see cref="TableRow"/> objects.
/// </summary>
/// <remarks>
/// Requires the document to be shared via "File → Share → Publish to web" in Google Docs,
/// which exposes a public <c>/pub</c> URL that returns raw HTML without authentication.
/// </remarks>
public class PublishedDocService : IDocService
{
    private readonly HttpClient _httpClient;
    private readonly string _documentUrl;

    /// <summary>Initializes a new instance of <see cref="PublishedDocService"/>.</summary>
    /// <param name="httpClient">The HTTP client used to fetch the document HTML.</param>
    /// <param name="configuration">
    /// Application configuration; must contain the key <c>GoogleDoc:DocumentUrl</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>GoogleDoc:DocumentUrl</c> is absent from configuration.
    /// </exception>
    public PublishedDocService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _documentUrl = configuration["GoogleDoc:DocumentUrl"]
            ?? throw new InvalidOperationException("GoogleDoc:DocumentUrl is not configured in appsettings.json.");
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the HTTP request fails, times out, or the fetched HTML contains no table.
    /// </exception>
    public async Task<List<TableRow>> GetTableDataAsync(bool skipHeader = true)
    {
        var html = await FetchHtmlAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectSingleNode("//table")
            ?? throw new InvalidOperationException("No table found in the fetched document.");

        var trNodes = table.SelectNodes(".//tr")?.Cast<HtmlNode>().ToList() ?? [];
        var rows = new List<TableRow>(trNodes.Count);

        for (int i = 0; i < trNodes.Count; i++)
        {
            if (skipHeader && i == 0)
                continue;

            var cells = trNodes[i].SelectNodes(".//td|.//th")?.Cast<HtmlNode>().ToList() ?? [];

            rows.Add(new TableRow
            {
                Column1 = cells.Count > 0 ? GetCellText(cells[0]) : string.Empty,
                Column2 = cells.Count > 1 ? GetCellText(cells[1]) : string.Empty,
                Column3 = cells.Count > 2 ? GetCellText(cells[2]) : string.Empty,
            });
        }

        return rows;
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetSecondColumnOnlyAsync(bool skipHeader = true)
    {
        var rows = await GetTableDataAsync(skipHeader);
        return rows.Select(r => r.Column2).ToList();
    }

    // Wraps HttpRequestException and TaskCanceledException in InvalidOperationException so
    // callers get a uniform error type without depending on transport-layer details.
    private async Task<string> FetchHtmlAsync()
    {
        try
        {
            return await _httpClient.GetStringAsync(_documentUrl);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Failed to fetch document from '{_documentUrl}': {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException(
                $"Request to '{_documentUrl}' timed out.", ex);
        }
    }

    // Decodes HTML entities (e.g. &amp; → &) and removes leading/trailing whitespace.
    private static string GetCellText(HtmlNode cell) =>
        HtmlEntity.DeEntitize(cell.InnerText).Trim();
}
