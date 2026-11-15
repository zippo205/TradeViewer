using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>
/// One candle file as it goes into the candle store: its candles replace everything the store holds for the
/// period of the file. With an <see cref="Error"/> the file broke the format rules: the period is emptied and
/// the error is kept in the import log (MD6).
/// </summary>
public sealed class FileImport
{
    public FileImport(CandleFile file, long sizeBytes, long modified, IReadOnlyList<Candle> candles, string? error)
    {
        File = file;
        SizeBytes = sizeBytes;
        Modified = modified;
        Candles = candles;
        Error = error;
    }

    public CandleFile File { get; }

    public long SizeBytes { get; }

    /// <summary>The last write time of the file in Unix milliseconds; with the size it tells whether a file changed.</summary>
    public long Modified { get; }

    public IReadOnlyList<Candle> Candles { get; }

    public string? Error { get; }
}
