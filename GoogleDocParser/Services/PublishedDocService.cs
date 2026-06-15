using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using GoogleDocParser.Models;

namespace GoogleDocParser.Services;

public class PublishedDocService : IDocService
{
    private readonly HttpClient _httpClient;
    private readonly string _documentUrl;

    public PublishedDocService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _documentUrl = configuration["GoogleDoc:DocumentUrl"]
            ?? throw new InvalidOperationException("GoogleDoc:DocumentUrl is not configured in appsettings.json.");
    }

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

    public async Task<List<string>> GetSecondColumnOnlyAsync(bool skipHeader = true)
    {
        var rows = await GetTableDataAsync(skipHeader);
        return rows.Select(r => r.Column2).ToList();
    }

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

    private static string GetCellText(HtmlNode cell) =>
        HtmlEntity.DeEntitize(cell.InnerText).Trim();
}
