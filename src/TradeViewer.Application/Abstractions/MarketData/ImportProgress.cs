using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>How far the import of one timeframe is: the wanted files and how many of them are in the candle store.</summary>
public sealed record TimeframeProgress(Timeframe Timeframe, int FilesImported, int FilesWanted);

/// <summary>
/// The state of the import of an asset (AS4). Pending: work for the asset is queued or running. The
/// timeframes are those the last or the running import has looked at, largest first.
/// </summary>
public sealed record ImportProgress(bool Pending, IReadOnlyList<TimeframeProgress> Timeframes)
{
    public static ImportProgress None { get; } = new ImportProgress(false, Array.Empty<TimeframeProgress>());
}
