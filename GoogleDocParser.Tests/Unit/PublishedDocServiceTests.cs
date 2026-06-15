using GoogleDocParser.Services;
using GoogleDocParser.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GoogleDocParser.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="PublishedDocService"/>. HTTP is replaced by
/// <see cref="FakeHttpMessageHandler"/>; no real network calls are made.
/// </summary>
public class PublishedDocServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private const string DefaultUrl = "http://fake.test/doc";

    private static IConfiguration BuildConfig(string? url = DefaultUrl) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(url is null
                ? []
                : [new KeyValuePair<string, string?>("GoogleDoc:DocumentUrl", url)])
            .Build();

    private static PublishedDocService BuildService(string responseHtml) =>
        new(new HttpClient(FakeHttpMessageHandler.Returning(responseHtml)), BuildConfig());

    // A 4-row table: 1 header row + 3 data rows
    private const string ThreeDataRowHtml = """
        <html><body>
        <table>
          <tr><th>x</th><th>char</th><th>y</th></tr>
          <tr><td>0</td><td>█</td><td>2</td></tr>
          <tr><td>1</td><td>░</td><td>3</td></tr>
          <tr><td>2</td><td>X</td><td>4</td></tr>
        </table>
        </body></html>
        """;

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenDocumentUrlIsMissing()
    {
        var config = BuildConfig(url: null);
        Assert.Throws<InvalidOperationException>(
            () => new PublishedDocService(new HttpClient(), config));
    }

    // ── GetTableDataAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTableDataAsync_SkipsFirstRow_WhenSkipHeaderIsTrue()
    {
        var service = BuildService(ThreeDataRowHtml);

        var rows = await service.GetTableDataAsync(skipHeader: true);

        Assert.Equal(3, rows.Count);
        Assert.Equal("0", rows[0].Column1); // first data row, not the header
    }

    [Fact]
    public async Task GetTableDataAsync_IncludesAllRows_WhenSkipHeaderIsFalse()
    {
        var service = BuildService(ThreeDataRowHtml);

        var rows = await service.GetTableDataAsync(skipHeader: false);

        Assert.Equal(4, rows.Count);
        Assert.Equal("x", rows[0].Column1); // header row included
    }

    [Fact]
    public async Task GetTableDataAsync_MapsCellValuesToCorrectColumns()
    {
        var service = BuildService(ThreeDataRowHtml);

        var rows = await service.GetTableDataAsync();

        Assert.Equal("0", rows[0].Column1);
        Assert.Equal("█", rows[0].Column2);
        Assert.Equal("2", rows[0].Column3);
        Assert.Equal("1", rows[1].Column1);
        Assert.Equal("░", rows[1].Column2);
        Assert.Equal("3", rows[1].Column3);
    }

    [Fact]
    public async Task GetTableDataAsync_DecodesHtmlEntities()
    {
        const string html = """
            <html><body>
            <table>
              <tr><td>0</td><td>&amp;</td><td>1</td></tr>
            </table>
            </body></html>
            """;
        var service = BuildService(html);

        var rows = await service.GetTableDataAsync(skipHeader: false);

        Assert.Single(rows);
        Assert.Equal("&", rows[0].Column2);
    }

    [Fact]
    public async Task GetTableDataAsync_TrimsWhitespaceFromCellText()
    {
        const string html = """
            <html><body>
            <table>
              <tr><td>  hello  </td><td>  world  </td><td>  ! </td></tr>
            </table>
            </body></html>
            """;
        var service = BuildService(html);

        var rows = await service.GetTableDataAsync(skipHeader: false);

        Assert.Equal("hello", rows[0].Column1);
        Assert.Equal("world", rows[0].Column2);
        Assert.Equal("!", rows[0].Column3);
    }

    [Fact]
    public async Task GetTableDataAsync_SetsEmptyString_ForMissingCells()
    {
        const string html = """
            <html><body>
            <table>
              <tr><td>only-one</td></tr>
            </table>
            </body></html>
            """;
        var service = BuildService(html);

        var rows = await service.GetTableDataAsync(skipHeader: false);

        Assert.Single(rows);
        Assert.Equal("only-one", rows[0].Column1);
        Assert.Equal(string.Empty, rows[0].Column2);
        Assert.Equal(string.Empty, rows[0].Column3);
    }

    [Fact]
    public async Task GetTableDataAsync_ReturnsEmptyList_WhenTableHasNoRows()
    {
        const string html = "<html><body><table></table></body></html>";
        var service = BuildService(html);

        var rows = await service.GetTableDataAsync();

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetTableDataAsync_ThrowsInvalidOperationException_WhenNoTableInHtml()
    {
        const string html = "<html><body><p>No table here.</p></body></html>";
        var service = BuildService(html);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTableDataAsync());

        Assert.Contains("No table found", ex.Message);
    }

    [Fact]
    public async Task GetTableDataAsync_ThrowsInvalidOperationException_WhenHttpRequestFails()
    {
        var handler = FakeHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));
        var service = new PublishedDocService(new HttpClient(handler), BuildConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTableDataAsync());

        Assert.Contains("Failed to fetch document", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task GetTableDataAsync_ThrowsInvalidOperationException_WhenRequestTimesOut()
    {
        var handler = FakeHttpMessageHandler.Throwing(new TaskCanceledException("simulated timeout"));
        var service = new PublishedDocService(new HttpClient(handler), BuildConfig());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTableDataAsync());

        Assert.Contains("timed out", ex.Message);
        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    // ── GetSecondColumnOnlyAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetSecondColumnOnlyAsync_ReturnsOnlyColumn2Values()
    {
        var service = BuildService(ThreeDataRowHtml);

        var values = await service.GetSecondColumnOnlyAsync();

        Assert.Equal(["█", "░", "X"], values);
    }

    [Fact]
    public async Task GetSecondColumnOnlyAsync_IncludesHeaderColumn2_WhenSkipHeaderIsFalse()
    {
        var service = BuildService(ThreeDataRowHtml);

        var values = await service.GetSecondColumnOnlyAsync(skipHeader: false);

        Assert.Equal(4, values.Count);
        Assert.Equal("char", values[0]); // header row's second column
    }

    [Fact]
    public async Task GetSecondColumnOnlyAsync_ReturnsEmptyList_WhenTableHasNoRows()
    {
        const string html = "<html><body><table></table></body></html>";
        var service = BuildService(html);

        var values = await service.GetSecondColumnOnlyAsync();

        Assert.Empty(values);
    }
}
