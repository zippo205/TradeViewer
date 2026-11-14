using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeViewer.Infrastructure.Storage;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>The start-up checks through the real application: a wrong configuration stops the start with a clear message.</summary>
public class StartupTests : IDisposable
{
    private readonly TradeViewerFactory _factory = new TradeViewerFactory();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public void A_missing_output_root_setting_stops_the_start()
    {
        var message = StartupFailure(Setting("MarketData:RootPath", ""));

        Assert.Contains("MarketData:RootPath is required", message);
    }

    [Fact]
    public void A_missing_data_path_setting_stops_the_start()
    {
        var message = StartupFailure(Setting("Storage:DataPath", ""));

        Assert.Contains("Storage:DataPath is required", message);
    }

    [Fact]
    public void An_output_root_that_does_not_exist_stops_the_start_and_is_not_created()
    {
        var missing = Path.Combine(_factory.MarketDataRoot, "not-mounted");

        var message = StartupFailure(Setting("MarketData:RootPath", missing));

        Assert.Contains("does not exist", message);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void A_data_path_inside_the_output_root_stops_the_start()
    {
        var inside = Path.Combine(_factory.MarketDataRoot, "app");

        var message = StartupFailure(Setting("Storage:DataPath", inside));

        Assert.Contains("must not be the Downloader's output root", message);
        Assert.False(Directory.Exists(inside));
    }

    [Fact]
    public void An_enabled_Downloader_with_a_wrong_address_stops_the_start()
    {
        var message = StartupFailure(new Dictionary<string, string?>
        {
            ["MarketData:Downloader:Enabled"] = "true",
            ["MarketData:Downloader:BaseUrl"] = "localhost-8081"
        });

        Assert.Contains("MarketData:Downloader:BaseUrl", message);
    }

    [Fact]
    public void Without_a_scenario_folder_the_feature_is_off()
    {
        _ = _factory.CreateClient();

        var locations = _factory.Services.GetRequiredService<StorageLocations>();

        Assert.Null(locations.ScenariosRoot);
        Assert.False(locations.ScenariosWritable);
    }

    [Fact]
    public void A_writable_scenario_folder_switches_the_own_analysis_on()
    {
        var folder = Path.Combine(_factory.MarketDataRoot, "..", "scenarios");
        Directory.CreateDirectory(folder);

        using var factory = _factory.WithWebHostBuilder(builder => Override(builder, Setting("Scenarios:RootPath", folder)));
        var locations = factory.Services.GetRequiredService<StorageLocations>();

        Assert.True(locations.ScenariosWritable);
    }

    [Fact]
    public void A_scenario_folder_that_is_not_writable_only_switches_the_own_analysis_off()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // the file mode cannot be set there; the application runs on Linux
        }

        var folder = Path.GetFullPath(Path.Combine(_factory.MarketDataRoot, "..", "scenarios-readonly"));
        Directory.CreateDirectory(folder);
        File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            if (CanWriteTo(folder))
            {
                return; // running with rights that ignore the mode (e.g. as root): nothing to prove here
            }

            using var factory = _factory.WithWebHostBuilder(builder => Override(builder, Setting("Scenarios:RootPath", folder)));
            using var client = factory.CreateClient();
            var locations = factory.Services.GetRequiredService<StorageLocations>();

            Assert.False(locations.ScenariosWritable);
            Assert.Equal(folder, locations.ScenariosRoot);
        }
        finally
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private string StartupFailure(Dictionary<string, string?> settings)
    {
        using var factory = _factory.WithWebHostBuilder(builder => Override(builder, settings));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        return exception.ToString();
    }

    private static Dictionary<string, string?> Setting(string key, string value)
    {
        return new Dictionary<string, string?> { [key] = value };
    }

    private static void Override(IWebHostBuilder builder, Dictionary<string, string?> settings)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    }

    private static bool CanWriteTo(string folder)
    {
        try
        {
            var probe = Path.Combine(folder, ".probe");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
