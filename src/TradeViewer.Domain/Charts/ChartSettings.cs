using TradeViewer.Domain.Indicators;

namespace TradeViewer.Domain.Charts;

/// <summary>
/// The settings of the charts of one asset, saved per asset (CP2): all drawings hidden (DR6) and the
/// indicators in the order they were added (IN1). The selected EWTS instance and hidden groups follow with
/// their epics.
/// </summary>
public sealed record ChartSettings(bool DrawingsHidden, IReadOnlyList<IndicatorInstance> Indicators)
{
    public ChartSettings(bool drawingsHidden)
        : this(drawingsHidden, Array.Empty<IndicatorInstance>())
    {
    }

    /// <summary>The settings of an asset the user has not changed anything for.</summary>
    public static ChartSettings Default { get; } = new ChartSettings(false);
}
