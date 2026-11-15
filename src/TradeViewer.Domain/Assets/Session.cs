namespace TradeViewer.Domain.Assets;

public enum SessionType
{
    /// <summary>Trades around the clock (crypto): days and weeks are cut in UTC.</summary>
    Continuous,

    /// <summary>An exchange with an open and a close in its time zone (stocks, ETFs).</summary>
    Exchange,

    /// <summary>The trading day runs from 17:00 to 17:00 New York time (Forex).</summary>
    NewYorkClose
}

/// <summary>How the candles of an asset are aligned. Open and close are local times (HH:mm) of an exchange session.</summary>
public sealed record Session(SessionType Type, string TimeZone, string? Open, string? Close)
{
    public static Session ContinuousUtc { get; } = new Session(SessionType.Continuous, "UTC", null, null);
}
