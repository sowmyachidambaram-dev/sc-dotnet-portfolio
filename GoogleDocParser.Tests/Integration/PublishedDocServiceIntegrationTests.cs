using GoogleDocParser.Services;
using Microsoft.Extensions.Configuration;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace GoogleDocParser.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="PublishedDocService"/> that exercise the full
/// HTTP + HTML-parsing pipeline. A real <see cref="HttpClient"/> is used against a
/// WireMock.Net stub server, so no Google credentials or live internet are required.
/// </summary>
public sealed class PublishedDocServiceIntegrationTests : IDisposable
{
    private readonly WireMockServer _server;

    public PublishedDocServiceIntegrationTests()
    {
        _server = WireMockServer.Start();
    }

    public void Dispose() => _server.Stop();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private PublishedDocService BuildService(string path = "/doc")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new("GoogleDoc:DocumentUrl", $"{_server.Urls[0]}{path}")])
            .Build();
        return new PublishedDocService(new HttpClient(), config);
    }

    private void StubHtmlAt(string path, string htmlBody)
    {
        _server
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(htmlBody));
    }

    // A representative 4-row table matching the real document schema (x, char, y)
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

    // ── GetTableDataAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTableDataAsync_ParsesRowsFromRealHttpResponse()
    {
        StubHtmlAt("/doc", ThreeDataRowHtml);
        var service = BuildService();

        var rows = await service.GetTableDataAsync(skipHeader: true);

        Assert.Equal(3, rows.Count);
        Assert.Equal("0", rows[0].Column1);
        Assert.Equal("█", rows[0].Column2);
        Assert.Equal("2", rows[0].Column3);
    }

    [Fact]
    public async Task GetTableDataAsync_IncludesHeaderRow_WhenSkipHeaderIsFalse()
    {
        StubHtmlAt("/doc", ThreeDataRowHtml);
        var service = BuildService();

        var rows = await service.GetTableDataAsync(skipHeader: false);

        Assert.Equal(4, rows.Count);
        Assert.Equal("x", rows[0].Column1);
    }

    [Fact]
    public async Task GetTableDataAsync_ThrowsInvalidOperationException_WhenServerReturnsNoTable()
    {
        StubHtmlAt("/doc", "<html><body><p>No table here.</p></body></html>");
        var service = BuildService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTableDataAsync());
    }

    [Fact]
    public async Task GetTableDataAsync_ThrowsInvalidOperationException_WhenUrlDoesNotExist()
    {
        // No stub registered — WireMock returns 404; GetStringAsync throws HttpRequestException
        // for non-2xx responses, which FetchHtmlAsync converts to InvalidOperationException.
        var service = BuildService("/nonexistent");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetTableDataAsync());
    }

    [Fact]
    public async Task GetTableDataAsync_HandlesGoogleDocStyleHtml()
    {
        // Mimics the extra nesting and attributes present in real Google Doc published HTML.
        const string googleStyleHtml = """
            <html>
            <head><meta charset="UTF-8"></head>
            <body class="c8">
            <div id="contents">
              <table class="c3">
                <tbody>
                  <tr class="c1">
                    <td class="c5"><p class="c2"><span class="c0">0</span></p></td>
                    <td class="c5"><p class="c2"><span class="c0">█</span></p></td>
                    <td class="c5"><p class="c2"><span class="c0">0</span></p></td>
                  </tr>
                  <tr class="c1">
                    <td class="c5"><p class="c2"><span class="c0">1</span></p></td>
                    <td class="c5"><p class="c2"><span class="c0">░</span></p></td>
                    <td class="c5"><p class="c2"><span class="c0">1</span></p></td>
                  </tr>
                </tbody>
              </table>
            </div>
            </body></html>
            """;

        StubHtmlAt("/gdoc", googleStyleHtml);
        var service = BuildService("/gdoc");

        var rows = await service.GetTableDataAsync(skipHeader: false);

        Assert.Equal(2, rows.Count);
        Assert.Equal("0", rows[0].Column1);
        Assert.Equal("█", rows[0].Column2);
        Assert.Equal("0", rows[0].Column3);
    }

    // ── GetSecondColumnOnlyAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetSecondColumnOnlyAsync_ReturnsOnlyColumn2Values()
    {
        StubHtmlAt("/doc", ThreeDataRowHtml);
        var service = BuildService();

        var values = await service.GetSecondColumnOnlyAsync();

        Assert.Equal(["█", "░", "X"], values);
    }

    [Fact]
    public async Task GetSecondColumnOnlyAsync_ReturnsEmptyList_WhenTableHasOnlyHeader()
    {
        const string headerOnlyHtml = """
            <html><body>
            <table>
              <tr><th>x</th><th>char</th><th>y</th></tr>
            </table>
            </body></html>
            """;
        StubHtmlAt("/doc", headerOnlyHtml);
        var service = BuildService();

        var values = await service.GetSecondColumnOnlyAsync(skipHeader: true);

        Assert.Empty(values);
    }

    // ── Resilience ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTableDataAsync_ReturnsConsistentResults_AcrossMultipleCalls()
    {
        StubHtmlAt("/doc", ThreeDataRowHtml);
        var service = BuildService();

        var rows1 = await service.GetTableDataAsync();
        var rows2 = await service.GetTableDataAsync();

        Assert.Equal(rows1.Count, rows2.Count);
        for (int i = 0; i < rows1.Count; i++)
        {
            Assert.Equal(rows1[i].Column1, rows2[i].Column1);
            Assert.Equal(rows1[i].Column2, rows2[i].Column2);
            Assert.Equal(rows1[i].Column3, rows2[i].Column3);
        }
    }
}
