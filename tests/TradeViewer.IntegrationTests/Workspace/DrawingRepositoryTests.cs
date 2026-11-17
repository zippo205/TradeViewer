using System.Text.Json;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.Charts;
using TradeViewer.Domain.Drawings;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.Workspace;

namespace TradeViewer.IntegrationTests.Workspace;

/// <summary>
/// DR1-DR6, AS5, WS1-WS4: drawings, Fibonacci presets and chart settings in workspace.db. What the user drew is
/// read back exactly, survives a restart, an upgrade and a restore, and goes only with its asset.
/// </summary>
public class DrawingRepositoryTests : IDisposable
{
    private const long Time = 1_763_164_800; // 2025-11-15 00:00 UTC

    private static readonly AssetHistory History = HistorySettings.Initial.StartingAt(new DateOnly(2026, 11, 15), Timeframes.All);

    private readonly WorkspaceFixture _workspace = new WorkspaceFixture();

    public DrawingRepositoryTests()
    {
        _workspace.Start();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [Fact]
    public async Task Every_tool_is_read_back_exactly_also_after_a_restart()
    {
        await AddAsset("BTCUSDT");
        var drawings = DrawingTools.All.Select(tool => Sample(tool, "BTCUSDT")).ToList();
        foreach (var drawing in drawings)
        {
            Assert.Equal(DrawingSaveResult.Saved, await _workspace.Drawings.SaveAsync(drawing, CancellationToken.None));
        }

        _workspace.Start();
        var read = await _workspace.Drawings.GetByAssetAsync("binance", "BTCUSDT", CancellationToken.None);

        Assert.Equal(drawings.Select(Json), read.Select(Json)); // same content, in the order they were made
        Assert.Equal(Json(drawings[3]), Json((await _workspace.Drawings.GetAsync(drawings[3].Id, CancellationToken.None))!));
    }

    [Fact]
    public async Task Saving_again_replaces_the_drawing_in_its_place_and_the_last_save_wins()
    {
        await AddAsset("BTCUSDT");
        var first = Sample(DrawingTool.TrendLine, "BTCUSDT");
        var second = Sample(DrawingTool.Rectangle, "BTCUSDT");
        await Save(first);
        await Save(second);

        var moved = first with { Anchors = new[] { new Anchor(Time + 7200, 91000), new Anchor(Time + 36000, 95000) }, Locked = true };
        await Save(moved);
        await Save(moved); // a save repeated after a lost answer changes nothing

        var read = await _workspace.Drawings.GetByAssetAsync("binance", "BTCUSDT", CancellationToken.None);
        Assert.Equal(new[] { Json(moved), Json(second) }, read.Select(Json));
    }

    [Fact]
    public async Task A_drawing_needs_its_asset_and_never_moves_to_another()
    {
        await AddAsset("BTCUSDT");
        await AddAsset("ETHUSDT");
        var drawing = Sample(DrawingTool.Ray, "BTCUSDT");
        await Save(drawing);

        var unknown = await _workspace.Drawings.SaveAsync(Sample(DrawingTool.Ray, "NOPEUSDT"), CancellationToken.None);
        var other = await _workspace.Drawings.SaveAsync(drawing with { Symbol = "ETHUSDT" }, CancellationToken.None);

        Assert.Equal(DrawingSaveResult.UnknownAsset, unknown);
        Assert.Equal(DrawingSaveResult.OtherAsset, other);
        Assert.Empty(await _workspace.Drawings.GetByAssetAsync("binance", "ETHUSDT", CancellationToken.None));
        Assert.Equal(Json(drawing), Json((await _workspace.Drawings.GetAsync(drawing.Id, CancellationToken.None))!));
    }

    [Fact]
    public async Task A_deleted_drawing_is_gone_and_a_save_with_the_same_id_brings_it_back()
    {
        await AddAsset("BTCUSDT");
        var drawing = Sample(DrawingTool.Polyline, "BTCUSDT");
        await Save(drawing);

        Assert.True(await _workspace.Drawings.DeleteAsync(drawing.Id, CancellationToken.None));
        Assert.False(await _workspace.Drawings.DeleteAsync(drawing.Id, CancellationToken.None));
        Assert.Null(await _workspace.Drawings.GetAsync(drawing.Id, CancellationToken.None));

        await Save(drawing); // undo of a delete (plan: a PUT of the same record)
        Assert.Equal(Json(drawing), Json((await _workspace.Drawings.GetAsync(drawing.Id, CancellationToken.None))!));
    }

    [Fact]
    public async Task Removing_an_asset_deletes_its_drawings_and_chart_settings_and_nothing_else()
    {
        await AddAsset("BTCUSDT");
        await AddAsset("ETHUSDT");
        await Save(Sample(DrawingTool.TrendLine, "BTCUSDT"));
        await Save(Sample(DrawingTool.WaveLabel, "BTCUSDT"));
        var kept = Sample(DrawingTool.TextNote, "ETHUSDT");
        await Save(kept);
        await _workspace.ChartSettings.SetAsync("binance", "BTCUSDT", new ChartSettings(true), CancellationToken.None);

        await _workspace.Assets.RemoveAsync("binance", "BTCUSDT", false, CancellationToken.None);
        await AddAsset("BTCUSDT"); // added again: it starts empty

        Assert.Empty(await _workspace.Drawings.GetByAssetAsync("binance", "BTCUSDT", CancellationToken.None));
        Assert.Null(await _workspace.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None));
        Assert.Equal(new[] { Json(kept) }, (await _workspace.Drawings.GetByAssetAsync("binance", "ETHUSDT", CancellationToken.None)).Select(Json));
    }

    [Fact]
    public async Task A_row_this_build_cannot_read_is_skipped_and_left_untouched()
    {
        await AddAsset("BTCUSDT");
        var readable = Sample(DrawingTool.TrendLine, "BTCUSDT");
        await Save(readable);
        Execute(
            "INSERT INTO drawings (id, source, symbol, tool, payload_version, payload, seq, updated_at) VALUES "
            + "('11111111-1111-1111-1111-111111111111', 'binance', 'BTCUSDT', 'trendLine', 9, '{\"future\":true}', 100, '2030-01-01T00:00:00Z'), "
            + "('22222222-2222-2222-2222-222222222222', 'binance', 'BTCUSDT', 'pitchfork', 1, '{}', 101, '2030-01-01T00:00:00Z'), "
            + "('33333333-3333-3333-3333-333333333333', 'binance', 'BTCUSDT', 'ray', 1, 'not json', 102, '2030-01-01T00:00:00Z');");

        var read = await _workspace.Drawings.GetByAssetAsync("binance", "BTCUSDT", CancellationToken.None);

        Assert.Equal(new[] { Json(readable) }, read.Select(Json));
        Assert.Equal("4", WorkspaceFixture.Scalar(_workspace.Locations.WorkspaceDatabasePath, "SELECT COUNT(*) FROM drawings;"));
    }

    [Fact]
    public async Task A_new_workspace_has_the_default_presets_of_the_domain()
    {
        var presets = await _workspace.Presets.GetAllAsync(CancellationToken.None);

        Assert.Equal(FibonacciPresets.Defaults.Select(Json), presets.Select(Json));
    }

    [Fact]
    public async Task Presets_are_added_at_the_end_edited_in_place_and_deleted()
    {
        var mine = new FibonacciPreset(Guid.NewGuid(), "Deep", FibonacciKind.Retracement, new[] { 0, 0.786, 0.886, 1 });
        await _workspace.Presets.SaveAsync(mine, CancellationToken.None);
        var edited = FibonacciPresets.Retracement with { Levels = new[] { 0, 0.382, 0.618, 1 } };
        await _workspace.Presets.SaveAsync(edited, CancellationToken.None);

        _workspace.Start();
        var presets = await _workspace.Presets.GetAllAsync(CancellationToken.None);
        Assert.Equal(new[] { Json(edited), Json(FibonacciPresets.Extension), Json(mine) }, presets.Select(Json));

        Assert.True(await _workspace.Presets.DeleteAsync(mine.Id, CancellationToken.None));
        Assert.False(await _workspace.Presets.DeleteAsync(mine.Id, CancellationToken.None));
        Assert.Null(await _workspace.Presets.GetAsync(mine.Id, CancellationToken.None));
        Assert.Equal(2, (await _workspace.Presets.GetAllAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task A_Fibonacci_drawing_keeps_its_levels_when_its_preset_is_edited()
    {
        await AddAsset("BTCUSDT");
        var drawing = Sample(DrawingTool.FibonacciRetracement, "BTCUSDT");
        await Save(drawing);

        await _workspace.Presets.SaveAsync(FibonacciPresets.Retracement with { Levels = new[] { 0.5 } }, CancellationToken.None);

        var read = await _workspace.Drawings.GetAsync(drawing.Id, CancellationToken.None);
        Assert.Equal(FibonacciPresets.Retracement.Levels, read!.Levels);
    }

    [Fact]
    public async Task Chart_settings_are_saved_per_asset_and_need_the_asset()
    {
        await AddAsset("BTCUSDT");

        Assert.Null(await _workspace.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None));
        Assert.True(await _workspace.ChartSettings.SetAsync("binance", "BTCUSDT", new ChartSettings(true), CancellationToken.None));
        Assert.False(await _workspace.ChartSettings.SetAsync("binance", "NOPEUSDT", new ChartSettings(true), CancellationToken.None));

        _workspace.Start();
        Assert.Equal(new ChartSettings(true), await _workspace.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None));

        await _workspace.ChartSettings.SetAsync("binance", "BTCUSDT", ChartSettings.Default, CancellationToken.None);
        Assert.Equal(ChartSettings.Default, await _workspace.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None));
    }

    [Fact]
    public async Task Chart_settings_written_before_this_epic_are_read_with_their_known_fields()
    {
        await AddAsset("BTCUSDT");

        // The price chart epic wrote a payload with the price scale per asset; that field is no longer used.
        Execute("INSERT INTO chart_settings (source, symbol, payload_version, payload, updated_at) "
            + "VALUES ('binance', 'BTCUSDT', 1, '{\"priceScale\":\"log\"}', '2026-11-16T00:00:00Z');");

        Assert.Equal(ChartSettings.Default, await _workspace.ChartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None));
    }

    [Fact]
    public async Task A_workspace_of_version_3_is_upgraded_with_its_assets_kept_and_gets_the_default_presets()
    {
        _workspace.Dispose();
        using var older = new WorkspaceFixture();
        Assert.Equal(3, older.StartUpTo(3));
        await older.Assets.AddAsync(new SymbolInfo("binance", "BTCUSDT", "BTC/USDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All), History, CancellationToken.None);

        var version = older.Start();

        Assert.Equal(older.Schema.LatestVersion, version);
        Assert.Contains(older.Store.List(), backup => backup.Name.EndsWith("-before-v4.db", StringComparison.Ordinal));
        Assert.NotNull(await older.Assets.GetAsync("binance", "BTCUSDT", CancellationToken.None));
        Assert.Equal(2, (await older.Presets.GetAllAsync(CancellationToken.None)).Count);
        Assert.Equal(DrawingSaveResult.Saved, await older.Drawings.SaveAsync(Sample(DrawingTool.TrendLine, "BTCUSDT"), CancellationToken.None));
    }

    [Fact]
    public async Task A_restore_brings_back_the_drawings_and_presets_of_the_backup()
    {
        await AddAsset("BTCUSDT");
        var before = Sample(DrawingTool.ParallelChannel, "BTCUSDT");
        await Save(before);
        var backup = await _workspace.Backups.CreateAsync(CancellationToken.None);

        await Save(before with { Locked = true });
        await Save(Sample(DrawingTool.Arrow, "BTCUSDT"));
        await _workspace.Presets.SaveAsync(new FibonacciPreset(Guid.NewGuid(), "Later", FibonacciKind.Extension, new[] { 1.0 }), CancellationToken.None);

        await _workspace.Backups.RestoreAsync(backup.Name, CancellationToken.None);

        Assert.Equal(new[] { Json(before) }, (await _workspace.Drawings.GetByAssetAsync("binance", "BTCUSDT", CancellationToken.None)).Select(Json));
        Assert.Equal(2, (await _workspace.Presets.GetAllAsync(CancellationToken.None)).Count);
    }

    private static Drawing Sample(DrawingTool tool, string symbol)
    {
        var (min, _) = DrawingTools.AnchorCount(tool);
        var count = tool == DrawingTool.Polyline ? 5 : min;
        var withoutPrice = tool == DrawingTool.WaveLabel;
        var anchors = Enumerable.Range(0, count)
            .Select(index => new Anchor(Time + index * 14_400L, withoutPrice ? null : 90_000.25 + index * 1234.5))
            .ToList();

        var drawing = new Drawing(Guid.NewGuid(), "binance", symbol, tool, DrawingTools.PayloadVersion(tool), false, anchors, DrawingTools.DefaultStyle(tool));
        switch (tool)
        {
            case DrawingTool.TextNote:
                return drawing with { Text = "Läuft das über 100k? \"quotes\" and\nnew lines" };
            case DrawingTool.FibonacciRetracement:
                return drawing with { Levels = FibonacciPresets.Retracement.Levels };
            case DrawingTool.FibonacciExtension:
                return drawing with { Levels = FibonacciPresets.Extension.Levels, Style = new DrawingStyle("#64B5F6", 1, LineStyle.Dashed) };
            case DrawingTool.WaveLabel:
                return drawing with { Wave = new WaveMark(WaveDegree.Minute, "C", WaveSide.Below) };
            case DrawingTool.Polyline:
                return drawing with
                {
                    LineHidden = true,
                    PointLabels = new[]
                    {
                        new PointLabel(1, new WaveMark(WaveDegree.Intermediate, "1", WaveSide.Above)),
                        new PointLabel(4, new WaveMark(WaveDegree.Intermediate, "4", WaveSide.Below))
                    }
                };
            case DrawingTool.TrendLine:
                return drawing with { Text = "neckline", Style = new DrawingStyle("#F23645CC", 3, LineStyle.Dotted) };
            default:
                return drawing;
        }
    }

    /// <summary>A drawing or preset as JSON: records holding lists compare their lists by reference, their JSON by content.</summary>
    private static string Json<T>(T value)
    {
        return JsonSerializer.Serialize(value);
    }

    private async Task AddAsset(string symbol)
    {
        var info = new SymbolInfo("binance", symbol, symbol, AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        Assert.NotNull(await _workspace.Assets.AddAsync(info, History, CancellationToken.None));
    }

    private async Task Save(Drawing drawing)
    {
        Assert.Equal(DrawingSaveResult.Saved, await _workspace.Drawings.SaveAsync(drawing, CancellationToken.None));
    }

    private void Execute(string sql)
    {
        using var connection = WorkspaceDatabase.OpenFile(_workspace.Locations.WorkspaceDatabasePath);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
