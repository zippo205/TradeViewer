namespace TradeViewer.Contracts.MarketData;

/// <summary>
/// A time span without candles that the source confirmed (MD4): From inclusive, To exclusive, in Unix
/// seconds (UTC). Reason: the reason the Downloader gave, today always missingUpstream.
/// </summary>
public sealed record GapResponse(long From, long To, string Reason);
