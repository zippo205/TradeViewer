namespace TradeViewer.Infrastructure.Configuration;

/// <summary>Where the Downloader's files are read from and how its API is used (architecture, chapter 12).</summary>
public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    /// <summary>The Downloader's output root. Required; read only, TradeViewer never writes there.</summary>
    public string? RootPath { get; set; }

    public DownloaderOptions Downloader { get; set; } = new DownloaderOptions();
}

public sealed class DownloaderOptions
{
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://localhost:8081";

    public string Client { get; set; } = "tradeviewer";

    /// <summary>The interval of the checks after a due time.</summary>
    public int PollSeconds { get; set; } = 20;

    /// <summary>A due check gives up after this.</summary>
    public int PollWindowMinutes { get; set; } = 10;

    public int RequestTimeoutSeconds { get; set; } = 30;
}

public sealed class EwtsOptions
{
    public const string SectionName = "Ewts";

    public bool Enabled { get; set; }

    public int PollSeconds { get; set; } = 30;

    public int RequestTimeoutSeconds { get; set; } = 30;
}

public sealed class ScenariosOptions
{
    public const string SectionName = "Scenarios";

    /// <summary>The folder of EWTS's scenario drawing files. Optional; TradeViewer writes only its *.expert.json there.</summary>
    public string? RootPath { get; set; }
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Holds marketdata.db, workspace.db and backups/. Required; created when missing.</summary>
    public string? DataPath { get; set; }

    public BackupOptions Backup { get; set; } = new BackupOptions();
}

public sealed class BackupOptions
{
    public int KeepCount { get; set; } = 14;

    /// <summary>Time of the daily backup, HH:mm in UTC.</summary>
    public string Time { get; set; } = "03:00";
}
