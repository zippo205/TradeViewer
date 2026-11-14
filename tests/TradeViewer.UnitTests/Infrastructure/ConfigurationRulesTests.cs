using TradeViewer.Infrastructure.Configuration;

namespace TradeViewer.UnitTests.Infrastructure;

public class ConfigurationRulesTests
{
    [Fact]
    public void The_defaults_need_only_the_two_required_paths()
    {
        Assert.Empty(ConfigurationRules.Check(new MarketDataOptions { RootPath = "/data/marketdata" }));
        Assert.Empty(ConfigurationRules.Check(new StorageOptions { DataPath = "/data/app" }));
        Assert.Empty(ConfigurationRules.Check(new EwtsOptions()));
    }

    [Fact]
    public void The_defaults_are_those_of_the_architecture()
    {
        var downloader = new MarketDataOptions().Downloader;

        Assert.False(downloader.Enabled);
        Assert.Equal("http://localhost:8081", downloader.BaseUrl);
        Assert.Equal("tradeviewer", downloader.Client);
        Assert.Equal(20, downloader.PollSeconds);
        Assert.Equal(10, downloader.PollWindowMinutes);
        Assert.Equal(30, downloader.RequestTimeoutSeconds);
        Assert.False(new EwtsOptions().Enabled);
        Assert.Equal(30, new EwtsOptions().PollSeconds);
        Assert.Equal(14, new StorageOptions().Backup.KeepCount);
        Assert.Equal("03:00", new StorageOptions().Backup.Time);
    }

    [Fact]
    public void A_missing_root_path_is_reported_by_name()
    {
        var errors = ConfigurationRules.Check(new MarketDataOptions());

        Assert.Contains(errors, e => e.Contains("MarketData:RootPath"));
    }

    [Fact]
    public void A_missing_data_path_is_reported_by_name()
    {
        var errors = ConfigurationRules.Check(new StorageOptions());

        Assert.Contains(errors, e => e.Contains("Storage:DataPath"));
    }

    [Theory]
    [InlineData("ftp://host")]
    [InlineData("not a url")]
    [InlineData("")]
    public void An_enabled_Downloader_needs_an_http_address(string baseUrl)
    {
        var options = new MarketDataOptions { RootPath = "/x" };
        options.Downloader.Enabled = true;
        options.Downloader.BaseUrl = baseUrl;

        Assert.Contains(ConfigurationRules.Check(options), e => e.Contains("BaseUrl"));
    }

    [Fact]
    public void A_disabled_Downloader_is_not_checked_for_its_address()
    {
        var options = new MarketDataOptions { RootPath = "/x" };
        options.Downloader.BaseUrl = "nonsense";

        Assert.Empty(ConfigurationRules.Check(options));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("trade/viewer")]
    public void An_enabled_Downloader_needs_a_valid_client_name(string client)
    {
        var options = new MarketDataOptions { RootPath = "/x" };
        options.Downloader.Enabled = true;
        options.Downloader.Client = client;

        Assert.Contains(ConfigurationRules.Check(options), e => e.Contains("Client"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Intervals_and_counts_must_be_positive(int value)
    {
        var market = new MarketDataOptions { RootPath = "/x" };
        market.Downloader.PollSeconds = value;
        market.Downloader.PollWindowMinutes = value;
        market.Downloader.RequestTimeoutSeconds = value;
        var storage = new StorageOptions { DataPath = "/x" };
        storage.Backup.KeepCount = value;

        Assert.Equal(3, ConfigurationRules.Check(market).Count);
        Assert.Single(ConfigurationRules.Check(storage));
        Assert.Equal(2, ConfigurationRules.Check(new EwtsOptions { PollSeconds = value, RequestTimeoutSeconds = value }).Count);
    }

    [Theory]
    [InlineData("03:00", true)]
    [InlineData("23:59", true)]
    [InlineData("3:00", false)]
    [InlineData("24:00", false)]
    [InlineData("noon", false)]
    public void The_backup_time_is_hours_and_minutes(string value, bool valid)
    {
        var errors = ConfigurationRules.Check(new StorageOptions { DataPath = "/x", Backup = { Time = value } });

        Assert.Equal(valid, errors.Count == 0);
    }
}
