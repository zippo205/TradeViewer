using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>Reads one candle file of the Downloader. Nothing is repaired or skipped (MD1, MD6).</summary>
public interface ICandleFileReader
{
    /// <summary>
    /// The candles of the file in the order of the file. A file that breaks the format rules is an
    /// InvalidData error naming the file and the line; a file that is gone is a NotFound error.
    /// </summary>
    Result<IReadOnlyList<Candle>> Read(string path, CandleFile file);
}
