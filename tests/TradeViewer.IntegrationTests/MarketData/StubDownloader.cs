using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using TradeViewer.Infrastructure.Configuration;
using TradeViewer.Infrastructure.MarketData.Downloader;

namespace TradeViewer.IntegrationTests.MarketData;

/// <summary>
/// Stands in for the Downloader: answers a request (method and path) with a recorded response of
/// tests/fixtures/downloader and keeps what was asked. No test contacts the real Downloader.
/// </summary>
public sealed class StubDownloader : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _answers = new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.Ordinal);

    /// <summary>The requests in the order they came, as "METHOD /path?query".</summary>
    public List<string> Requests { get; } = new List<string>();

    /// <summary>
    /// True: a request no answer was given for gets 404 instead of failing the test, for the application's own
    /// calls in the background (the sync at start-up) when the stub stands in inside the running application.
    /// </summary>
    public bool AnswerOthersWithNotFound { get; set; }

    /// <summary>The body of the last request that had one.</summary>
    public string? LastBody { get; private set; }

    public static string Fixture(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "downloader", name));
    }

    public void Answer(string request, HttpStatusCode status, string? fixture)
    {
        _answers[request] = () => new HttpResponseMessage(status)
        {
            Content = fixture == null
                ? new ByteArrayContent(Array.Empty<byte>())
                : new StringContent(Fixture(fixture), Encoding.UTF8, status == HttpStatusCode.OK ? "application/json" : "application/problem+json")
        };
    }

    public void AnswerText(string request, HttpStatusCode status, string text)
    {
        _answers[request] = () => new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    }

    public void Fail(string request, Exception exception)
    {
        _answers[request] = () => throw exception;
    }

    public DownloaderClient NewClient(bool enabled = true, string client = "tradeviewer-test")
    {
        var options = new MarketDataOptions { RootPath = "/unused" };
        options.Downloader.Enabled = enabled;
        options.Downloader.Client = client;

        var http = new HttpClient(this) { BaseAddress = new Uri("http://downloader.test:8081/"), Timeout = TimeSpan.FromSeconds(5) };
        return new DownloaderClient(http, Options.Create(options));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = request.Method.Method + " " + request.RequestUri!.PathAndQuery;
        Requests.Add(key);
        if (request.Content != null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        if (!_answers.TryGetValue(key, out var answer))
        {
            if (AnswerOthersWithNotFound)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/problem+json") };
            }

            throw new InvalidOperationException("The test did not expect the request " + key);
        }

        return answer();
    }
}
