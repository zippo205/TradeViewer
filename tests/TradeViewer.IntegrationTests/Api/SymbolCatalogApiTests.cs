using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TradeViewer.Contracts.Assets;
using TradeViewer.IntegrationTests.MarketData;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>
/// AS2 through the real application with the Downloader switched on, answered by the stub with recorded answers:
/// the catalog is searched, a symbol picked is looked up by its ticker. No real Downloader is called.
/// </summary>
public class SymbolCatalogApiTests : IDisposable
{
    private readonly StubDownloader _downloader = new StubDownloader();
    private readonly TradeViewerFactory _factory;
    private readonly HttpClient _client;

    public SymbolCatalogApiTests()
    {
        _factory = TradeViewerFactory.WithDownloader(_downloader);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task The_catalog_of_the_Downloader_is_searched_and_says_when_more_matched()
    {
        _downloader.Answer("GET /api/v1/sources/binance/symbols?q=btc&limit=50", HttpStatusCode.OK, "catalog-binance-btc.json");

        var result = await _client.GetFromJsonAsync<SymbolSearchResponse>("/api/symbols/binance?q=btc");

        Assert.True(result!.FromCatalog);
        Assert.True(result.Truncated);
        Assert.Equal(112, result.Total);
        Assert.Equal(new[] { "BTCUSDT", "BTCFDUSD" }, result.Symbols.Select(symbol => symbol.Symbol));
    }

    [Fact]
    public async Task A_ticker_with_a_colon_reaches_the_lookup_URL_encoded()
    {
        // tradingview is not offered (not chartable yet), so the route is tried with binance and a made-up ticker.
        _downloader.Answer("GET /api/v1/sources/binance/symbols/BTC%3AUSDT", HttpStatusCode.OK, "lookup-BTCUSDT.json");

        var response = await _client.GetAsync("/api/symbols/binance/" + Uri.EscapeDataString("BTC:USDT"));
        var details = await response.Content.ReadFromJsonAsync<SymbolDetailsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BTCUSDT", details!.Symbol);
        Assert.Equal("2017-08-17", details.Earliest["1d"]);
    }

    [Fact]
    public async Task The_catalog_unreachable_is_a_503_with_the_message_to_show()
    {
        _downloader.Fail("GET /api/v1/sources/binance/symbols?q=eth&limit=50", new HttpRequestException("connection refused"));

        var response = await _client.GetAsync("/api/symbols/binance?q=eth");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("cannot be reached", problem.RootElement.GetProperty("detail").GetString());
    }
}
