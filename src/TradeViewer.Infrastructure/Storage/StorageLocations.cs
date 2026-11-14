using TradeViewer.Infrastructure.Configuration;

namespace TradeViewer.Infrastructure.Storage;

/// <summary>The folders of the application as absolute paths. Relative values are resolved against the content root of the Api.</summary>
public sealed class StorageLocations
{
    public StorageLocations(string marketDataRoot, string dataPath, string? scenariosRoot)
    {
        MarketDataRoot = marketDataRoot;
        DataPath = dataPath;
        ScenariosRoot = scenariosRoot;
    }

    /// <summary>The Downloader's output root, read only.</summary>
    public string MarketDataRoot { get; }

    /// <summary>marketdata.db, workspace.db and backups/.</summary>
    public string DataPath { get; }

    /// <summary>The folder of EWTS's scenario files, or null when the feature is off.</summary>
    public string? ScenariosRoot { get; }

    /// <summary>
    /// True when TradeViewer can write its *.expert.json files into <see cref="ScenariosRoot"/>.
    /// Set by the start-up checks; false switches off only SC10 to SC13.
    /// </summary>
    public bool ScenariosWritable { get; internal set; }

    public static StorageLocations Resolve(MarketDataOptions marketData, StorageOptions storage, ScenariosOptions scenarios, string contentRoot)
    {
        var scenariosRoot = string.IsNullOrWhiteSpace(scenarios.RootPath)
            ? null
            : PathResolver.Resolve(scenarios.RootPath, contentRoot);

        return new StorageLocations(
            PathResolver.Resolve(marketData.RootPath!, contentRoot),
            PathResolver.Resolve(storage.DataPath!, contentRoot),
            scenariosRoot);
    }
}
