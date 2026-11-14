using Microsoft.Extensions.Logging.Abstractions;
using TradeViewer.Infrastructure.Storage;

namespace TradeViewer.UnitTests.Infrastructure;

public class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tradeviewer-tests", Guid.NewGuid().ToString("N"));

    public StorageTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Fact]
    public void A_relative_path_is_resolved_against_the_content_root()
    {
        Assert.Equal("/srv/app/data", PathResolver.Resolve("data", "/srv/app"));
        Assert.Equal("/srv/data", PathResolver.Resolve("../data/", "/srv/app"));
        Assert.Equal("/abs/path", PathResolver.Resolve("/abs/path", "/srv/app"));
    }

    [Fact]
    public void A_tilde_is_the_home_folder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(Path.Combine(home, "marketdata"), PathResolver.Resolve("~/marketdata", "/srv/app"));
        Assert.Equal(home, PathResolver.Resolve("~", "/srv/app"));
    }

    [Theory]
    [InlineData("/a/b", "/a/b", true)]
    [InlineData("/a/b/c", "/a/b", true)]
    [InlineData("/a/bc", "/a/b", false)]
    [InlineData("/a", "/a/b", false)]
    public void Inside_means_the_folder_itself_or_below_it(string path, string folder, bool expected)
    {
        Assert.Equal(expected, PathResolver.IsSameOrInside(path, folder));
    }

    [Fact]
    public void The_data_path_must_not_lie_in_a_folder_that_is_read_only_for_the_application()
    {
        Assert.NotEmpty(StorageRules.Check(new StorageLocations("/m", "/m/app", null)));
        Assert.NotEmpty(StorageRules.Check(new StorageLocations("/m", "/m", null)));
        Assert.NotEmpty(StorageRules.Check(new StorageLocations("/m", "/s/app", "/s")));
        Assert.NotEmpty(StorageRules.Check(new StorageLocations("/m", "/d", "/m/scenarios")));
        Assert.Empty(StorageRules.Check(new StorageLocations("/m", "/d", "/s")));
    }

    [Fact]
    public async Task The_data_path_is_created_and_the_writable_scenario_folder_is_recognised()
    {
        var locations = Locations(scenarios: true);

        await new StorageInitializer(locations, NullLogger<StorageInitializer>.Instance).StartAsync(CancellationToken.None);

        Assert.True(Directory.Exists(locations.DataPath));
        Assert.True(locations.ScenariosWritable);
        Assert.Empty(Directory.GetFiles(locations.DataPath, ".write-test-*"));
    }

    [Fact]
    public async Task A_missing_output_root_stops_the_start_and_is_not_created()
    {
        var locations = new StorageLocations(Path.Combine(_root, "missing"), Path.Combine(_root, "data"), null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new StorageInitializer(locations, NullLogger<StorageInitializer>.Instance).StartAsync(CancellationToken.None));

        Assert.Contains("MarketData:RootPath", ex.Message);
        Assert.False(Directory.Exists(locations.MarketDataRoot));
        Assert.False(Directory.Exists(locations.DataPath));
    }

    [Fact]
    public async Task A_missing_scenario_folder_stops_the_start()
    {
        var market = Path.Combine(_root, "market");
        Directory.CreateDirectory(market);
        var locations = new StorageLocations(market, Path.Combine(_root, "data"), Path.Combine(_root, "scenarios"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new StorageInitializer(locations, NullLogger<StorageInitializer>.Instance).StartAsync(CancellationToken.None));

        Assert.Contains("Scenarios:RootPath", ex.Message);
    }

    [Fact]
    public async Task Without_a_scenario_folder_the_feature_stays_off()
    {
        var locations = Locations(scenarios: false);

        await new StorageInitializer(locations, NullLogger<StorageInitializer>.Instance).StartAsync(CancellationToken.None);

        Assert.Null(locations.ScenariosRoot);
        Assert.False(locations.ScenariosWritable);
    }

    private StorageLocations Locations(bool scenarios)
    {
        var market = Path.Combine(_root, "market");
        Directory.CreateDirectory(market);
        string? scenarioFolder = null;
        if (scenarios)
        {
            scenarioFolder = Path.Combine(_root, "scenarios");
            Directory.CreateDirectory(scenarioFolder);
        }

        return new StorageLocations(market, Path.Combine(_root, "data"), scenarioFolder);
    }
}
