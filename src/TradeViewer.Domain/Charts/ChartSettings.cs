namespace TradeViewer.Domain.Charts;

/// <summary>
/// The settings of the charts of one asset, saved per asset (CP2). So far: all drawings hidden (DR6).
/// Indicators, the selected EWTS instance and hidden groups follow with their epics.
/// </summary>
public sealed record ChartSettings(bool DrawingsHidden)
{
    /// <summary>The settings of an asset the user has not changed anything for.</summary>
    public static ChartSettings Default { get; } = new ChartSettings(false);
}
