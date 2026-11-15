using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace TradeViewer.IntegrationTests;

/// <summary>Starts the application with temporary folders; nothing of the real Downloader, EWTS or output root is touched.</summary>
public sealed class TradeViewerFactory : WebApplicationFactory<Program>
{
    private readonly string _root;
    private readonly bool _ownsRoot;

    public TradeViewerFactory()
    {
        _root = Path.Combine(Path.GetTempPath(), "tradeviewer-tests", Guid.NewGuid().ToString("N"));
        _ownsRoot = true;
    }

    private TradeViewerFactory(string existingRoot)
    {
        _root = existingRoot;
    }

    /// <summary>A second application on the folders of this one, like a restart; it leaves the folders in place when disposed.</summary>
    public TradeViewerFactory Restart()
    {
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
