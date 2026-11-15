namespace TradeViewer.Domain.MarketData;

/// <summary>What the name of a candle file says: it holds exactly the candles that open within its period (UTC).</summary>
public sealed class CandleFile
{
    public CandleFile(string name, string symbol, Timeframe timeframe, long periodStart, long periodEnd)
    {
        Name = name;
        Symbol = symbol;
        Timeframe = timeframe;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
    }

    /// <summary>The file name without a folder, e.g. BTCUSDT-15m-2026-11-04.csv.</summary>
    public string Name { get; }

    public string Symbol { get; }

    public Timeframe Timeframe { get; }

    /// <summary>Unix seconds, inclusive.</summary>
    public long PeriodStart { get; }

    /// <summary>Unix seconds, exclusive.</summary>
    public long PeriodEnd { get; }
}
