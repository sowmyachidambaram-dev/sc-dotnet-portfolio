using System.Net;
using System.Text;

namespace GoogleDocParser.Tests.Helpers;

/// <summary>
/// A test-only <see cref="HttpMessageHandler"/> that returns pre-configured responses
/// without making real network calls.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    private FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        => _responseFactory = responseFactory;

    /// <summary>Returns a handler that always responds with HTTP 200 and the given HTML body.</summary>
    internal static FakeHttpMessageHandler Returning(string htmlContent) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
        });

    /// <summary>Returns a handler that faults every request with <paramref name="ex"/>.</summary>
    internal static FakeHttpMessageHandler Throwing(Exception ex) =>
        new(_ => throw ex);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(_responseFactory(request));
        }
        catch (Exception ex)
        {
            return Task.FromException<HttpResponseMessage>(ex);
        }
    }
}
