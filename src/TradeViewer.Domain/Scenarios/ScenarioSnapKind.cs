namespace TradeViewer.Domain.Scenarios;

/// <summary>How a point is put on its candle (SC10, the snap rules of EWTS's format).</summary>
public enum ScenarioSnapKind
{
    /// <summary>A wave point at a low: the open time of its candle and the candle's low.</summary>
    Low,

    /// <summary>A wave point at a high: the open time of its candle and the candle's high.</summary>
    High,

    /// <summary>Entry and add: the open time and the close of the candle that closes beyond the level.</summary>
    Close,

    /// <summary>An exit or a stop move: the open time of the candle at the price the user gives.</summary>
    GivenPrice,

    /// <summary>The stop: a price only; the candle gives nothing.</summary>
    PriceOnly
}
