using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// A page of candles as the chart shows it, with the candles before it that a calculation needs.
/// <see cref="Candles"/> holds the warm-up candles first, then the page; <see cref="WarmUp"/> says how many of
/// them come before the page (fewer than asked at the start of the history). Partial: the last candle is the
/// current one, still in progress (MD8).
/// </summary>
public sealed record CandleSeriesPage(IReadOnlyList<Candle> Candles, int WarmUp, bool Partial)
{
    /// <summary>The candles of the page itself, without the warm-up.</summary>
    public IEnumerable<Candle> Page => Candles.Skip(WarmUp);
}
