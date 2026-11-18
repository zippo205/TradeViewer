using TradeViewer.Domain.Assets;
using TradeViewer.Domain.Charts;
using TradeViewer.Domain.Indicators;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.Workspace;

namespace TradeViewer.IntegrationTests.Workspace;

/// <summary>
/// IN1, IN2, CP2, WS1: the indicators of the chart settings and the templates in workspace.db. What the user set
/// is read back exactly, survives a restart and an upgrade, and the templates belong to no asset.
/// </summary>
public class IndicatorStorageTests : IDisposable
{
    private static readonly AssetHistory History = HistorySettings.Initial.StartingAt(new DateOnly(2026, 11, 15), Timeframes.All);

    private readonly WorkspaceFixture _workspace = new WorkspaceFixture();

    public IndicatorStorageTests()
    {
        _workspace.Start();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task Every_kind_of_indicator_is_read_back_exactly_also_after_a_restart()
    {
        await AddAsset("BTCUSDT");
        var indicators = Sample();

        Assert.True(await _workspace.ChartSettings.SetAsync("binance", "BTCUSDT", new ChartSettings(true, indicators), CancellationToken.None));
        _workspace.Start();
        var read = await _workspace.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None);

        Assert.True(read!.DrawingsHidden);
        AssertSame(indicators, read.Indicators);
    }

    [Fact]
    public async Task Settings_saved_before_this_epic_have_no_indicators()
    {
        await AddAsset("BTCUSDT");
        Execute("INSERT INTO chart_settings (source, symbol, payload_version, payload, updated_at) "
            + "VALUES ('binance', 'BTCUSDT', 1, '{\"drawingsHidden\":true}', '2026-11-17T00:00:00Z');");

        var read = await _workspace.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None);

        Assert.True(read!.DrawingsHidden);
        Assert.Empty(read.Indicators);
    }

    [Fact]
    public async Task An_indicator_this_build_cannot_read_is_left_out_and_the_others_stay()
    {
        await AddAsset("BTCUSDT");
        var sma = Guid.NewGuid();
        Execute("INSERT INTO chart_settings (source, symbol, payload_version, payload, updated_at) VALUES ('binance', 'BTCUSDT', 1, '{\"drawingsHidden\":false,\"indicators\":["
            + "{\"id\":\"" + sma + "\",\"type\":\"sma\",\"length\":50,\"price\":\"close\",\"styles\":[{\"line\":\"value\",\"color\":\"#2962FF\",\"width\":2}]},"
            + "{\"id\":\"" + Guid.NewGuid() + "\",\"type\":\"wma\",\"length\":9,\"price\":\"close\",\"styles\":[]},"
            + "{\"id\":\"" + Guid.NewGuid() + "\",\"type\":\"rsi\",\"price\":\"close\",\"styles\":[{\"line\":\"value\",\"color\":\"#7E57C2\",\"width\":1}]}"
            + "]}', '2026-11-18T00:00:00Z');");

        var read = await _workspace.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None);

        var only = Assert.Single(read!.Indicators);
        Assert.Equal(sma, only.Id);
        Assert.Equal(new SmaParameters(50, PriceSource.Close), only.Parameters);
    }

    [Fact]
    public async Task A_workspace_of_version_6_is_upgraded_with_its_chart_settings_kept()
    {
        _workspace.Dispose();
        using var older = new WorkspaceFixture();
        older.StartUpTo(6);
        var info = new SymbolInfo("binance", "BTCUSDT", "BTCUSDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        await older.Assets.AddAsync(info, History, CancellationToken.None);
        await older.ChartSettings.SetAsync("binance", "BTCUSDT", new ChartSettings(true), CancellationToken.None);

        older.Start();
        var read = await older.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None);
        var template = new IndicatorTemplate(Guid.NewGuid(), "Trend", Sample());
        await older.Templates.SaveAsync(template, CancellationToken.None);

        Assert.True(read!.DrawingsHidden);
        Assert.Single(await older.Templates.GetAllAsync(CancellationToken.None));
        Assert.Contains(older.Store.List(), backup => backup.Name.EndsWith("-before-v7.db", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Templates_are_read_back_by_name_replaced_in_place_and_deleted()
    {
        var trend = new IndicatorTemplate(Guid.NewGuid(), "trend", Sample());
        var oscillators = new IndicatorTemplate(Guid.NewGuid(), "Oscillators", Sample().Where(i => i.Type is IndicatorType.Rsi or IndicatorType.Macd).ToList());

        await _workspace.Templates.SaveAsync(trend, CancellationToken.None);
        await _workspace.Templates.SaveAsync(oscillators, CancellationToken.None);
        _workspace.Start();
        var all = await _workspace.Templates.GetAllAsync(CancellationToken.None);

        Assert.Equal(new[] { "Oscillators", "trend" }, all.Select(t => t.Name)); // by name, whatever the case
        AssertSame(trend.Indicators, all[1].Indicators);

        await _workspace.Templates.SaveAsync(trend with { Name = "Trend 2", Indicators = trend.Indicators.Take(1).ToList() }, CancellationToken.None);
        all = await _workspace.Templates.GetAllAsync(CancellationToken.None);
        Assert.Equal(2, all.Count);
        Assert.Single(all.Single(t => t.Id == trend.Id).Indicators);

        Assert.True(await _workspace.Templates.DeleteAsync(oscillators.Id, CancellationToken.None));
        Assert.False(await _workspace.Templates.DeleteAsync(oscillators.Id, CancellationToken.None));
        Assert.Equal(new[] { "Trend 2" }, (await _workspace.Templates.GetAllAsync(CancellationToken.None)).Select(t => t.Name));
    }

    [Fact]
    public async Task Templates_survive_the_removal_of_every_asset()
    {
        await AddAsset("BTCUSDT");
        await _workspace.ChartSettings.SetAsync("binance", "BTCUSDT", new ChartSettings(false, Sample()), CancellationToken.None);
        await _workspace.Templates.SaveAsync(new IndicatorTemplate(Guid.NewGuid(), "Kept", Sample()), CancellationToken.None);

        Assert.True(await _workspace.Assets.RemoveAsync("binance", "BTCUSDT", false, CancellationToken.None));

        Assert.Single(await _workspace.Templates.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public void Two_templates_cannot_have_the_same_name_in_the_database_either()
    {
        Execute("INSERT INTO indicator_templates (id, name, payload_version, payload, updated_at) VALUES ('a', 'Trend', 1, '[]', '2026-11-18T00:00:00Z');");

        Assert.ThrowsAny<Exception>(() => Execute(
            "INSERT INTO indicator_templates (id, name, payload_version, payload, updated_at) VALUES ('b', 'TREND', 1, '[]', '2026-11-18T00:00:00Z');"));
    }

    /// <summary>One of each kind with parameters and styles away from the defaults, one of them hidden.</summary>
    private static List<IndicatorInstance> Sample()
    {
        var styles = (IndicatorType type) => IndicatorStyles.Default(type).Select((s, i) => s with { Color = "#11223344", Width = 1 + i % 4 }).ToList();
        return new List<IndicatorInstance>
        {
            new(Guid.NewGuid(), new SmaParameters(200, PriceSource.Hlc3), styles(IndicatorType.Sma), false),
            new(Guid.NewGuid(), new EmaParameters(21, PriceSource.Ohlc4), styles(IndicatorType.Ema), true),
            new(Guid.NewGuid(), new BollingerBandsParameters(50, 2.5, PriceSource.Hl2), styles(IndicatorType.BollingerBands), false),
            new(Guid.NewGuid(), new RsiParameters(7, PriceSource.Open), styles(IndicatorType.Rsi), false),
            new(Guid.NewGuid(), new MacdParameters(8, 21, 5, PriceSource.High), styles(IndicatorType.Macd), false),
            new(Guid.NewGuid(), new AtrParameters(10), styles(IndicatorType.Atr), false)
        };
    }

    private static void AssertSame(IReadOnlyList<IndicatorInstance> expected, IReadOnlyList<IndicatorInstance> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Id, actual[i].Id);
            Assert.Equal(expected[i].Parameters, actual[i].Parameters);
            Assert.Equal(expected[i].Hidden, actual[i].Hidden);
            Assert.Equal(expected[i].Styles, actual[i].Styles);
        }
    }

    private async Task AddAsset(string symbol)
    {
        var info = new SymbolInfo("binance", symbol, symbol, AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        Assert.NotNull(await _workspace.Assets.AddAsync(info, History, CancellationToken.None));
    }

    private void Execute(string sql)
    {
        using var connection = WorkspaceDatabase.OpenFile(_workspace.Locations.WorkspaceDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
