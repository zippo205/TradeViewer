namespace TradeViewer.IntegrationTests;

/// <summary>tests/fixtures: small slices of the Downloader's output root, in its layout.</summary>
public static class Fixtures
{
    /// <summary>The output root of the fixtures (MarketData:RootPath of a test).</summary>
    public static string MarketDataRoot { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures", "marketdata");

    public static string CandleFile(string source, string symbol, string timeframe, string name)
    {
        return Path.Combine(MarketDataRoot, source, symbol, timeframe, name);
    }
}
