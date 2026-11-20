namespace TradeViewer.IntegrationTests;

/// <summary>tests/fixtures: small slices of the Downloader's output root, in its layout, and EWTS's scenario files.</summary>
public static class Fixtures
{
    /// <summary>The output root of the fixtures (MarketData:RootPath of a test).</summary>
    public static string MarketDataRoot { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures", "marketdata");

    /// <summary>The 13 expert scenarios EWTS converted from the earlier implementation (2026-11-20), all drafts.</summary>
    public static string ExpertScenarios { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures", "scenarios", "expert");

    public static string CandleFile(string source, string symbol, string timeframe, string name)
    {
        return Path.Combine(MarketDataRoot, source, symbol, timeframe, name);
    }
}
