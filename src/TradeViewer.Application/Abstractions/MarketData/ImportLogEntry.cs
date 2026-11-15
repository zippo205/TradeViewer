namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>What the candle store knows about a file it imported, or failed to import.</summary>
public sealed record ImportLogEntry(string File, long SizeBytes, long Modified, int Candles, string? Error);
