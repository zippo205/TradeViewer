using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TradeViewer.IntegrationTests;

/// <summary>Starts the application with temporary folders; nothing of the real Downloader, EWTS or output root is touched.</summary>
public sealed class TradeViewerFactory : WebApplicationFactory<Program>
{
    private readonly string _root;
    private bool _ownsRoot;

    public TradeViewerFactory()
    {
        _root = Path.Combine(Path.GetTempPath(), "tradeviewer-tests", Guid.NewGuid().ToString("N"));
        _ownsRoot = true;
    }

    private TradeViewerFactory(string existingRoot)
    {
        _root = existingRoot;
        _ownsRoot = true;
    }

    /// <summary>
    /// Stops this application and returns a new one on the same folders: a restart. The new one removes the
    /// folders when it is disposed. beforeStart runs while no application is running.
    /// </summary>
    public TradeViewerFactory Restart(Action? beforeStart = null)
    {
        _ownsRoot = false;
        Dispose();
        beforeStart?.Invoke();
        return new TradeViewerFactory(_root);
    }

    public string MarketDataRoot => Path.Combine(_root, "marketdata");

    public string DataPath => Path.Combine(_root, "app");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(MarketDataRoot);

        builder.UseEnvironment("Testing");
        builder.UseStaticWebAssets();
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MarketData:RootPath"] = MarketDataRoot,
                ["Storage:DataPath"] = DataPath
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && _ownsRoot && Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
