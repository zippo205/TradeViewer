using System.Text.Json;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Handlers;
using TradeViewer.Application.Features.ChartSettings.Queries;
using TradeViewer.Application.Features.Drawings.Commands;
using TradeViewer.Application.Features.Drawings.Handlers;
using TradeViewer.Application.Features.Drawings.Queries;
using TradeViewer.Contracts.Charts;
using TradeViewer.Contracts.Drawings;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.Drawings;
using TradeViewer.Domain.MarketData;
using TradeViewer.UnitTests.Application.Assets;

namespace TradeViewer.UnitTests.Application.Drawings;

/// <summary>DR1-DR6 and CP2 at the level of the handlers, with in-memory ports.</summary>
public class DrawingHandlersTests
{
    private static readonly DateTimeOffset Time = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeDrawingRepository _drawings = new FakeDrawingRepository();
    private readonly FakeFibonacciPresetRepository _presets = new FakeFibonacciPresetRepository();
    private readonly FakeChartSettingsRepository _chartSettings = new FakeChartSettingsRepository();
    private readonly FakeNotifier _notifier = new FakeNotifier();

    public DrawingHandlersTests()
    {
        AddAsset("BTCUSDT");
        AddAsset("ETHUSDT");
    }

    [Fact]
    public async Task A_saved_drawing_is_read_back_as_it_was_sent_and_the_other_windows_hear_of_it()
    {
        var record = TrendLine("BTCUSDT");

        var saved = await SaveAsync(record, "window-1");
        var read = await new GetDrawingsQueryHandler(_assets, _drawings).HandleAsync(new GetDrawingsQuery("binance", "BTCUSDT"), CancellationToken.None);

        Assert.True(saved.IsSuccess);
        Assert.Equal(Json(new[] { record }), Json(read.Value));
        Assert.Equal(new[] { "DrawingsChanged binance/BTCUSDT window-1" }, _notifier.Events);
    }

    [Fact]
    public async Task Every_part_of_the_record_survives_the_way_through_the_domain()
    {
        var polyline = new DrawingRecord(
            Guid.NewGuid(),
            "binance",
            "BTCUSDT",
            "polyline",
            1,
            true,
            new[] { Anchor(0, 100), Anchor(1, 120), Anchor(2, 110) },
            new DrawingStyleRecord("#2962FF", 2, "dotted"),
            "count",
            null,
            null,
            new[] { new PointLabelRecord(1, new WaveMarkRecord("minuette", "1", "above")), new PointLabelRecord(2, new WaveMarkRecord("minuette", "2", "below")) },
            true);
        var label = new DrawingRecord(
            Guid.NewGuid(), "binance", "BTCUSDT", "waveLabel", 1, false, new[] { new AnchorRecord(Time, null) },
            new DrawingStyleRecord("#2962FF", 1, "solid"), Wave: new WaveMarkRecord("grandSupercycle", "W", "below"));
        var fibonacci = new DrawingRecord(
            Guid.NewGuid(), "binance", "BTCUSDT", "fibonacciExtension", 1, false, new[] { Anchor(0, 100), Anchor(5, 200), Anchor(8, 150) },
            new DrawingStyleRecord("#64B5F6", 1, "dashed"), Levels: Levels(1, 1.618, 2.618));
        var channel = new DrawingRecord(
            Guid.NewGuid(), "binance", "BTCUSDT", "parallelChannel", 1, false, new[] { Anchor(0, 100), Anchor(5, 200), Anchor(2, 90) },
            new DrawingStyleRecord("#2962FF", 1, "solid", "#2962FF1A"));

        foreach (var record in new[] { polyline, label, fibonacci, channel })
        {
            Assert.True((await SaveAsync(record, null)).IsSuccess);
        }

        var read = (await new GetDrawingsQueryHandler(_assets, _drawings).HandleAsync(new GetDrawingsQuery("binance", "BTCUSDT"), CancellationToken.None)).Value;
        Assert.Equal(Json(new[] { polyline, label, fibonacci, channel }), Json(read));
        Assert.Equal("DrawingsChanged binance/BTCUSDT ", _notifier.Events[0]); // no window named
    }

    [Theory]
    [InlineData("tool")]
    [InlineData("style.lineStyle")]
    [InlineData("anchors")]
    [InlineData("style.width")]
    [InlineData("version")]
    [InlineData("wave")]
    public async Task An_invalid_drawing_is_refused_with_the_field_and_nothing_is_stored(string field)
    {
        var record = TrendLine("BTCUSDT");
        record = field switch
        {
            "tool" => record with { Tool = "pitchfork" },
            "style.lineStyle" => record with { Style = record.Style with { LineStyle = "wavy" } },
            "anchors" => record with { Anchors = new[] { Anchor(0, 100) } },
            "style.width" => record with { Style = record.Style with { Width = 9 } },
            "version" => record with { Version = 2 },
            _ => record with { Wave = new WaveMarkRecord("minor", "1", "above") }
        };

        var result = await SaveAsync(record, "window-1");

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(field, result.Error.Fields.Keys);
        Assert.Empty(_drawings.Drawings);
        Assert.Empty(_notifier.Events);
    }

    [Fact]
    public async Task The_id_in_the_address_must_be_the_id_of_the_drawing()
    {
        var record = TrendLine("BTCUSDT");

        var result = await new SaveDrawingCommandHandler(_drawings, _notifier).HandleAsync(new SaveDrawingCommand(Guid.NewGuid(), record, null), CancellationToken.None);
        var missing = await new SaveDrawingCommandHandler(_drawings, _notifier).HandleAsync(new SaveDrawingCommand(record.Id, null, null), CancellationToken.None);

        Assert.Contains("id", result.Error.Fields.Keys);
        Assert.Contains("drawing", missing.Error.Fields.Keys);
    }

    [Fact]
    public async Task A_drawing_of_an_unknown_asset_or_with_the_id_of_another_assets_drawing_is_refused()
    {
        var record = TrendLine("BTCUSDT");
        await SaveAsync(record, null);
        _notifier.Events.Clear();

        var unknown = await SaveAsync(TrendLine("NOPEUSDT"), null);
        var moved = await SaveAsync(record with { Symbol = "ETHUSDT" }, null);

        Assert.Equal(ErrorType.NotFound, unknown.Error.Type);
        Assert.Equal(ErrorType.Conflict, moved.Error.Type);
        Assert.Empty(_notifier.Events);
    }

    [Fact]
    public async Task A_locked_drawing_can_be_saved_unlocked_and_changed_in_one_go()
    {
        var record = TrendLine("BTCUSDT") with { Locked = true };
        await SaveAsync(record, null);

        // The client sends only the last of several changes: "unlock, then move" arrives as one save.
        var result = await SaveAsync(record with { Locked = false, Anchors = new[] { Anchor(3, 105), Anchor(9, 125) } }, null);

        Assert.True(result.IsSuccess);
        Assert.False(_drawings.Drawings.Single().Locked);
    }

    [Fact]
    public async Task A_deleted_drawing_tells_the_windows_of_its_asset_and_a_second_delete_finds_nothing()
    {
        var record = TrendLine("ETHUSDT");
        await SaveAsync(record, null);
        _notifier.Events.Clear();
        var handler = new DeleteDrawingCommandHandler(_drawings, _notifier);

        var first = await handler.HandleAsync(new DeleteDrawingCommand(record.Id, "window-2"), CancellationToken.None);
        var second = await handler.HandleAsync(new DeleteDrawingCommand(record.Id, "window-2"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(ErrorType.NotFound, second.Error.Type);
        Assert.Equal(new[] { "DrawingsChanged binance/ETHUSDT window-2" }, _notifier.Events);
    }

    [Fact]
    public async Task The_drawings_of_an_unknown_asset_are_not_found()
    {
        var result = await new GetDrawingsQueryHandler(_assets, _drawings).HandleAsync(new GetDrawingsQuery("binance", "NOPEUSDT"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task A_preset_is_saved_with_its_name_trimmed_and_its_levels_in_order()
    {
        var id = Guid.NewGuid();
        var handler = new SaveFibonacciPresetCommandHandler(_presets);

        var result = await handler.HandleAsync(new SaveFibonacciPresetCommand(id, new FibonacciPresetRecord(id, "  Deep ", "retracement", Levels(1, 0.886, 0))), CancellationToken.None);
        var all = await new GetFibonacciPresetsQueryHandler(_presets).HandleAsync(new GetFibonacciPresetsQuery(), CancellationToken.None);

        Assert.Equal(new FibonacciPresetRecord(id, "Deep", "retracement", Levels(0, 0.886, 1)).Levels, result.Value.Levels);
        Assert.Equal("Deep", result.Value.Name);
        Assert.Equal(new[] { "Retracement", "Extension", "Deep" }, all.Value.Select(preset => preset.Name));
    }

    [Fact]
    public async Task An_invalid_preset_is_refused_with_the_field()
    {
        var id = Guid.NewGuid();
        var handler = new SaveFibonacciPresetCommandHandler(_presets);

        var kind = await handler.HandleAsync(new SaveFibonacciPresetCommand(id, new FibonacciPresetRecord(id, "X", "fan", Levels(0.5))), CancellationToken.None);
        var levels = await handler.HandleAsync(new SaveFibonacciPresetCommand(id, new FibonacciPresetRecord(id, "X", "extension", Array.Empty<FibonacciLevelRecord>())), CancellationToken.None);
        var otherId = await handler.HandleAsync(new SaveFibonacciPresetCommand(Guid.NewGuid(), new FibonacciPresetRecord(id, "X", "extension", Levels(1.0))), CancellationToken.None);

        Assert.Contains("kind", kind.Error.Fields.Keys);
        Assert.Contains("levels", levels.Error.Fields.Keys);
        Assert.Contains("id", otherId.Error.Fields.Keys);
        Assert.Equal(2, _presets.Presets.Count);
    }

    [Fact]
    public async Task The_last_preset_of_a_kind_stays()
    {
        var delete = new DeleteFibonacciPresetCommandHandler(_presets);
        var save = new SaveFibonacciPresetCommandHandler(_presets);
        var extension = FibonacciPresets.Extension;

        var deleted = await delete.HandleAsync(new DeleteFibonacciPresetCommand(extension.Id), CancellationToken.None);
        var turned = await save.HandleAsync(
            new SaveFibonacciPresetCommand(extension.Id, new FibonacciPresetRecord(extension.Id, "Extension", "retracement", Levels(0.5))),
            CancellationToken.None);
        var unknown = await delete.HandleAsync(new DeleteFibonacciPresetCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, deleted.Error.Type);
        Assert.Equal(ErrorType.Conflict, turned.Error.Type);
        Assert.Equal(ErrorType.NotFound, unknown.Error.Type);

        // With a second extension preset the first may go.
        var id = Guid.NewGuid();
        await save.HandleAsync(new SaveFibonacciPresetCommand(id, new FibonacciPresetRecord(id, "Short", "extension", Levels(1, 1.618))), CancellationToken.None);
        Assert.True((await delete.HandleAsync(new DeleteFibonacciPresetCommand(extension.Id), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Chart_settings_start_with_the_defaults_and_are_saved_per_asset()
    {
        var get = new GetChartSettingsQueryHandler(_assets, _chartSettings);
        var save = new SaveChartSettingsCommandHandler(_chartSettings, new FakeNotifier());

        var before = await get.HandleAsync(new GetChartSettingsQuery("binance", "BTCUSDT"), CancellationToken.None);
        var saved = await save.HandleAsync(new SaveChartSettingsCommand("binance", "BTCUSDT", new ChartSettingsResponse(true)), CancellationToken.None);
        var after = await get.HandleAsync(new GetChartSettingsQuery("binance", "BTCUSDT"), CancellationToken.None);
        var other = await get.HandleAsync(new GetChartSettingsQuery("binance", "ETHUSDT"), CancellationToken.None);

        Assert.False(before.Value.DrawingsHidden);
        Assert.True(saved.Value.DrawingsHidden);
        Assert.True(after.Value.DrawingsHidden);
        Assert.False(other.Value.DrawingsHidden);
    }

    [Fact]
    public async Task Chart_settings_need_a_known_asset_and_a_body()
    {
        var get = await new GetChartSettingsQueryHandler(_assets, _chartSettings).HandleAsync(new GetChartSettingsQuery("binance", "NOPEUSDT"), CancellationToken.None);
        var save = await new SaveChartSettingsCommandHandler(_chartSettings, new FakeNotifier()).HandleAsync(new SaveChartSettingsCommand("binance", "NOPEUSDT", new ChartSettingsResponse(true)), CancellationToken.None);
        var empty = await new SaveChartSettingsCommandHandler(_chartSettings, new FakeNotifier()).HandleAsync(new SaveChartSettingsCommand("binance", "BTCUSDT", null), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, get.Error.Type);
        Assert.Equal(ErrorType.NotFound, save.Error.Type);
        Assert.Equal(ErrorType.Validation, empty.Error.Type);
    }

    /// <summary>Levels that are shown, from their ratios.</summary>
    private static FibonacciLevelRecord[] Levels(params double[] ratios)
    {
        return ratios.Select(ratio => new FibonacciLevelRecord(ratio)).ToArray();
    }

    private static AnchorRecord Anchor(int hours, double price)
    {
        return new AnchorRecord(Time.AddHours(hours), price);
    }

    private static DrawingRecord TrendLine(string symbol)
    {
        return new DrawingRecord(Guid.NewGuid(), "binance", symbol, "trendLine", 1, false, new[] { Anchor(0, 100), Anchor(6, 120) }, new DrawingStyleRecord("#2962FF", 2, "solid"));
    }

    private static string Json<T>(T value)
    {
        return JsonSerializer.Serialize(value);
    }

    private Task<Result<Unit>> SaveAsync(DrawingRecord record, string? window)
    {
        return new SaveDrawingCommandHandler(_drawings, _notifier).HandleAsync(new SaveDrawingCommand(record.Id, record, window), CancellationToken.None);
    }

    private void AddAsset(string symbol)
    {
        var info = new SymbolInfo("binance", symbol, symbol, AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        _assets.Assets.Add(new Asset(info, new AssetHistory(new Dictionary<Timeframe, DateOnly?>()), _assets.Assets.Count + 1, true));
        _drawings.Assets.Add(("binance", symbol));
        _chartSettings.Assets.Add(("binance", symbol));
    }
}
