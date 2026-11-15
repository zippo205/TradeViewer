using System.Text;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Symbols;

namespace TradeViewer.UnitTests.Infrastructure.MarketData;

public class EmbeddedSymbolCatalogTests
{
    private readonly EmbeddedSymbolCatalog _catalog = new EmbeddedSymbolCatalog();

    [Fact]
    public void Binance_is_the_one_source_with_its_13_symbols()
    {
        Assert.Equal(new[] { "binance" }, _catalog.Sources);
        Assert.Equal(13, _catalog.Search("binance", null).Count);
        Assert.Empty(_catalog.Search("yahoo", null));
    }

    [Fact]
    public void A_symbol_carries_the_metadata_of_the_source()
    {
        var symbol = _catalog.Find("binance", "BTCUSDT");

        Assert.Equal(
            new SymbolInfo("binance", "BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, symbol!.SourceTimeframes),
            symbol);
        Assert.Equal(Timeframes.All, symbol.SourceTimeframes);
        Assert.Null(_catalog.Find("binance", "btcusdt"));
        Assert.Null(_catalog.Find("yahoo", "BTCUSDT"));
    }

    [Theory]
    [InlineData("btc", new[] { "BTCUSDT" })]
    [InlineData(" BTC ", new[] { "BTCUSDT" })]
    [InlineData("sol/", new[] { "SOLUSDT" })]
    [InlineData("xyz", new string[0])]
    public void Symbols_are_found_by_typing_a_part_of_symbol_or_name(string text, string[] expected)
    {
        Assert.Equal(expected, _catalog.Search("binance", text).Select(symbol => symbol.Symbol));
    }

    [Fact]
    public void The_symbols_are_ordered()
    {
        var symbols = _catalog.Search("binance", "usdt").Select(symbol => symbol.Symbol).ToList();

        Assert.Equal(13, symbols.Count);
        Assert.Equal(symbols.OrderBy(symbol => symbol, StringComparer.Ordinal), symbols);
    }

    [Fact]
    public void A_file_of_an_unknown_version_is_refused()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Parse("{ \"version\": 2, \"symbols\": {} }"));

        Assert.Contains("version 2", exception.Message);
    }

    [Fact]
    public void Unknown_fields_and_unknown_timeframes_are_ignored()
    {
        var symbols = Parse(
            "{ \"version\": 1, \"new\": true, \"symbols\": { \"TSLA\": { \"assetClass\": \"stock\", \"displayName\": \"Tesla\", \"tickSize\": 0.01, "
            + "\"source\": \"yahoo\", \"extra\": 1, \"session\": { \"type\": \"exchange\", \"timeZone\": \"America/New_York\", \"open\": \"09:30\", \"close\": \"16:00\" }, "
            + "\"sourceTimeframes\": [\"1M\", \"1d\", \"1m\", \"1h\"] } } }");

        var tesla = Assert.Single(symbols);
        Assert.Equal(AssetClass.Stock, tesla.AssetClass);
        Assert.Equal(new Session(SessionType.Exchange, "America/New_York", "09:30", "16:00"), tesla.Session);
        Assert.Equal(new[] { Timeframe.H1, Timeframe.D1 }, tesla.SourceTimeframes);
    }

    [Fact]
    public void An_unknown_asset_class_is_refused()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Parse(
            "{ \"version\": 1, \"symbols\": { \"X\": { \"assetClass\": \"bond\", \"displayName\": \"X\", \"tickSize\": 1, "
            + "\"session\": { \"type\": \"continuous\", \"timeZone\": \"UTC\" }, \"sourceTimeframes\": [] } } }"));

        Assert.Contains("assetClass", exception.Message);
    }

    private static List<SymbolInfo> Parse(string json)
    {
        return EmbeddedSymbolCatalog.Parse("test", new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
