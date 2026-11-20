using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TradeViewer.Infrastructure.Storage;

/// <summary>
/// Start-up checks of the folders. The output root is never created (a missing one usually means a missing mount);
/// the data path is created; a failure stops the start with a clear message.
/// </summary>
public sealed class StorageInitializer : IHostedService
{
    private readonly StorageLocations _locations;
    private readonly ILogger<StorageInitializer> _logger;

    public StorageInitializer(StorageLocations locations, ILogger<StorageInitializer> logger)
    {
        _locations = locations;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>(StorageRules.Check(_locations));

        if (!Directory.Exists(_locations.MarketDataRoot))
        {
            errors.Add("MarketData:RootPath does not exist: " + _locations.MarketDataRoot + " (is the Downloader's volume mounted?)");
        }

        if (_locations.ScenariosRoot != null && !Directory.Exists(_locations.ScenariosRoot))
        {
            errors.Add("Scenarios:RootPath does not exist: " + _locations.ScenariosRoot);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid folder configuration:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        Directory.CreateDirectory(_locations.DataPath);
        if (!CanWrite(_locations.DataPath))
        {
            throw new InvalidOperationException("Storage:DataPath is not writable: " + _locations.DataPath);
        }

        if (_locations.ScenariosRoot != null)
        {
            _locations.ScenariosWritable = CanWrite(_locations.ScenariosRoot);
            if (!_locations.ScenariosWritable)
            {
                _logger.LogWarning(
                    "The scenario folder {Path} is not writable: the scenario editor cannot save (SC9 to SC16, SC20).",
                    _locations.ScenariosRoot);
            }
        }

        _logger.LogInformation(
            "Folders: market data {MarketData} (read only), data {Data}, scenarios {Scenarios}.",
            _locations.MarketDataRoot,
            _locations.DataPath,
            _locations.ScenariosRoot ?? "off");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static bool CanWrite(string folder)
    {
        var probe = Path.Combine(folder, ".write-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return false;
        }
    }
}
