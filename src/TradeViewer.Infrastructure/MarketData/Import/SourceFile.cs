using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.MarketData.Import;

/// <summary>A candle file in the Downloader's output root as the file system shows it.</summary>
public sealed class SourceFile
{
    public SourceFile(CandleFile file, string path, long sizeBytes, long modified)
    {
        File = file;
        Path = path;
        SizeBytes = sizeBytes;
        Modified = modified;
    }

    public CandleFile File { get; }

    public string Path { get; }

    public long SizeBytes { get; }

    /// <summary>The last write time in Unix milliseconds.</summary>
    public long Modified { get; }
}
