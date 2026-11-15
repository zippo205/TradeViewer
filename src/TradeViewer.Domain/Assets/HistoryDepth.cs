namespace TradeViewer.Domain.Assets;

/// <summary>How much history a timeframe gets by default: everything the source has, or a number of days back from the day an asset is added.</summary>
public readonly record struct HistoryDepth
{
    private HistoryDepth(int? days)
    {
        Days = days;
    }

    /// <summary>Null means the complete history.</summary>
    public int? Days { get; }

    public bool IsComplete => Days == null;

    public static HistoryDepth Complete { get; } = new HistoryDepth(null);

    public static HistoryDepth OfDays(int days)
    {
        if (days < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "The history must be at least one day.");
        }

        return new HistoryDepth(days);
    }
}
