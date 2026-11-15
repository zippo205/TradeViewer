namespace TradeViewer.Application.Abstractions.MarketData;

/// <summary>The folders of the Downloader's output root, read only.</summary>
public interface IAssetFolders
{
    /// <summary>True when the output root has a folder for the symbol of the source. A missing folder means "no data".</summary>
    bool HasFolder(string source, string symbol);
}
