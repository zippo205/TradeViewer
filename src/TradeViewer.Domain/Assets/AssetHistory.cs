using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Assets;

/// <summary>
/// The history TradeViewer wants of an asset: per timeframe the first day (UTC), or null for the complete
/// history. Only files from that day on are imported, although the folder can hold more.
/// </summary>
public sealed class AssetHistory
{
    private readonly IReadOnlyDictionary<Timeframe, DateOnly?> _starts;

    public AssetHistory(IReadOnlyDictionary<Timeframe, DateOnly?> starts)
    {
        _starts = starts;
    }

    /// <summary>The timeframes of the asset, smallest first.</summary>
    public IReadOnlyList<Timeframe> Timeframes => _starts.Keys.OrderBy(MarketData.Timeframes.Seconds).ToList();

    public bool Has(Timeframe timeframe)
    {
        return _starts.ContainsKey(timeframe);
    }

    /// <summary>The first day of the timeframe; null for the complete history.</summary>
    public DateOnly? StartOf(Timeframe timeframe)
    {
        return _starts[timeframe];
    }

    /// <summary>The first day as Unix seconds; null for the complete history.</summary>
    public long? StartSecondsOf(Timeframe timeframe)
    {
        var start = _starts[timeframe];
        return start == null
            ? null
            : new DateTimeOffset(start.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).ToUnixTimeSeconds();
    }

    /// <summary>
    /// True when a file with this period is wanted: it ends after the start. A file that reaches over the
    /// start (a weekly month file) is wanted as a whole.
    /// </summary>
    public bool Wants(CandleFile file)
    {
        if (!_starts.TryGetValue(file.Timeframe, out var start))
        {
            return false;
        }

        return start == null || file.PeriodEnd > StartSecondsOf(file.Timeframe)!.Value;
    }

    /// <summary>The same history with another start for one timeframe.</summary>
    public AssetHistory With(Timeframe timeframe, DateOnly? start)
    {
        var starts = new Dictionary<Timeframe, DateOnly?>(_starts) { [timeframe] = start };
        return new AssetHistory(starts);
    }
}
