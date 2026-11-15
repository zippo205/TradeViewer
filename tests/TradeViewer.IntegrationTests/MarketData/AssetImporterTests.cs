using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.IntegrationTests.MarketData;

/// <summary>MD1, MD5, MD6 and AS4: the store follows the files of the asset, newest first, and never the other way round.</summary>
public class AssetImporterTests : IDisposable
{
    private readonly MarketDataFixture _data = new MarketDataFixture();
    private readonly List<TimeframeProgress> _reports = new List<TimeframeProgress>();

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void The_first_import_reads_every_file_of_every_timeframe()
    {
        var first = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96);
        var second = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 40);
        var days = _data.WriteFile("BTCUSDT", "BTCUSDT-1d-2026-11-03.csv", 1);
        _data.WriteFile("ETHUSDT", "ETHUSDT-15m-2026-11-04.csv", 96);

        var changed = Rescan(MarketDataFixture.Asset("BTCUSDT"));

        Assert.Equal(first.Concat(second), _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Equal(days, _data.Candles("BTCUSDT", Timeframe.D1));
        Assert.Empty(_data.Candles("BTCUSDT", Timeframe.H1));
        Assert.Empty(_data.Candles("ETHUSDT", Timeframe.M15));
        Assert.Equal(new[] { Timeframe.D1, Timeframe.M15 }, changed);
    }

    [Fact]
    public void Large_timeframes_come_first_and_the_progress_is_reported_per_timeframe()
    {
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96);
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        _data.WriteFile("BTCUSDT", "BTCUSDT-1w-2026-11.csv", 4);

        Rescan(MarketDataFixture.Asset("BTCUSDT"));

        Assert.Equal(
            new[]
            {
                new TimeframeProgress(Timeframe.W1, 0, 1),
                new TimeframeProgress(Timeframe.W1, 1, 1),
                new TimeframeProgress(Timeframe.D1, 0, 0),
                new TimeframeProgress(Timeframe.H4, 0, 0),
                new TimeframeProgress(Timeframe.H1, 0, 0),
                new TimeframeProgress(Timeframe.M15, 0, 2),
                new TimeframeProgress(Timeframe.M15, 2, 2)
            },
            _reports);
    }

    [Fact]
    public void A_second_rescan_without_changes_imports_nothing()
    {
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        var asset = MarketDataFixture.Asset("BTCUSDT", Timeframe.M15);
        Rescan(asset);
        _reports.Clear();

        var changed = Rescan(asset);

        Assert.Empty(changed);
        Assert.Equal(new[] { new TimeframeProgress(Timeframe.M15, 1, 1) }, _reports);
    }

    [Fact]
    public void A_grown_and_a_rewritten_file_are_imported_again()
    {
        var asset = MarketDataFixture.Asset("BTCUSDT", Timeframe.M15);
        var untouched = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-02.csv", 96);
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96);
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 40);
        Rescan(asset);

        var official = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96, seed: 7); // same size, other content and time
        var grown = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 41);
        _reports.Clear();
        var changed = Rescan(asset);

        Assert.Equal(untouched.Concat(official).Concat(grown), _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Equal(new[] { Timeframe.M15 }, changed);
        Assert.Equal(new TimeframeProgress(Timeframe.M15, 1, 3), _reports[0]);
    }

    [Fact]
    public void A_file_that_breaks_the_rules_is_not_imported_and_named_with_its_line()
    {
        var asset = MarketDataFixture.Asset("BTCUSDT", Timeframe.M15);
        var good = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96);
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        Rescan(asset);

        var path = _data.FilePath("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv");
        var lines = File.ReadAllLines(path);
        lines[6] = lines[6].Replace(lines[6].Split(',')[2], "1.00000000"); // high below everything
        _data.WriteText("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", string.Join("\n", lines));
        Rescan(asset);

        Assert.Equal(good, _data.Candles("BTCUSDT", Timeframe.M15));
        var error = Assert.Single(_data.Store.GetImportErrors("binance", "BTCUSDT"));
        Assert.StartsWith("BTCUSDT-15m-2026-11-04.csv, line 7: ", error.Error);

        // Nothing changed: the broken file is not read again and again.
        _reports.Clear();
        Assert.Empty(Rescan(asset));

        // The Downloader repairs the file.
        var repaired = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        Rescan(asset);
        Assert.Equal(good.Concat(repaired), _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Empty(_data.Store.GetImportErrors("binance", "BTCUSDT"));
    }

    [Fact]
    public void Only_files_from_the_history_start_are_imported_and_a_later_start_removes_older_candles()
    {
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-02.csv", 96);
        var third = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96);
        var fourth = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        var asset = MarketDataFixture.Asset("BTCUSDT", Timeframe.M15);
        var from3 = asset with { History = asset.History.With(Timeframe.M15, new DateOnly(2026, 11, 3)) };
        var from4 = asset with { History = asset.History.With(Timeframe.M15, new DateOnly(2026, 11, 4)) };

        Rescan(from3);
        Assert.Equal(third.Concat(fourth), _data.Candles("BTCUSDT", Timeframe.M15));

        Rescan(from4);
        Assert.Equal(fourth, _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Single(_data.Store.GetImportLog(new SeriesKey("binance", "BTCUSDT", Timeframe.M15)));

        Rescan(asset); // complete history again
        Assert.Equal(288, _data.Candles("BTCUSDT", Timeframe.M15).Count);
    }

    [Fact]
    public void A_file_that_was_deleted_takes_its_candles_with_it()
    {
        var asset = MarketDataFixture.Asset("BTCUSDT", Timeframe.M15);
        var kept = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96);
        _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        Rescan(asset);

        File.Delete(_data.FilePath("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv"));
        var changed = Rescan(asset);

        Assert.Equal(kept, _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Equal(new[] { Timeframe.M15 }, changed);
    }

    [Fact]
    public void Part_files_foreign_files_and_files_of_other_symbols_are_not_read()
    {
        var good = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 96);
        var folder = Path.GetDirectoryName(_data.FilePath("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv"))!;
        File.WriteAllText(Path.Combine(folder, "BTCUSDT-15m-2026-11-05.csv.part"), "half written");
        File.WriteAllText(Path.Combine(folder, "notes.csv"), "x");
        File.WriteAllText(Path.Combine(folder, "ETHUSDT-15m-2026-11-05.csv"), "x");
        File.WriteAllText(Path.Combine(folder, "BTCUSDT-1h-2026-11-05.csv"), "x");
        File.WriteAllText(Path.Combine(folder, "..", "manifest.json"), "{}");

        Rescan(MarketDataFixture.Asset("BTCUSDT"));

        Assert.Equal(good, _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Empty(_data.Store.GetImportErrors("binance", "BTCUSDT"));
    }

    [Fact]
    public void An_asset_without_a_folder_has_no_data()
    {
        var changed = Rescan(MarketDataFixture.Asset("DOGEUSDT"));

        Assert.Empty(changed);
        Assert.False(_data.Files.HasFolder("binance", "DOGEUSDT"));
        Assert.False(_data.Files.HasFolder("binance", ".."));
        Assert.False(_data.Files.HasFolder("..", "binance"));
    }

    [Fact]
    public void More_files_than_one_batch_are_imported_newest_first()
    {
        var start = new DateOnly(2025, 1, 1);
        var total = AssetImporter_BatchSize() + 30;
        for (var day = 0; day < total; day++)
        {
            _data.WriteFile("BTCUSDT", "BTCUSDT-1d-" + start.AddDays(day).ToString("yyyy-MM-dd") + ".csv", 1);
        }

        var asset = MarketDataFixture.Asset("BTCUSDT", Timeframe.D1);
        using var stopAfterFirstBatch = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => _data.Importer.Rescan(
            asset,
            progress =>
            {
                if (progress.FilesImported > 0)
                {
                    stopAfterFirstBatch.Cancel();
                }
            },
            stopAfterFirstBatch.Token));

        // The first batch holds the newest days; what is imported stays.
        var candles = _data.Candles("BTCUSDT", Timeframe.D1);
        Assert.Equal(AssetImporter_BatchSize(), candles.Count);
        Assert.Equal(start.AddDays(total - 1), DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(candles[candles.Count - 1].Time).UtcDateTime));

        // The next rescan finishes the rest.
        Rescan(asset);
        Assert.Equal(total, _data.Candles("BTCUSDT", Timeframe.D1).Count);
        Assert.Equal(new TimeframeProgress(Timeframe.D1, AssetImporter_BatchSize(), total), _reports[0]);
    }

    [Fact]
    public void Files_named_by_the_Downloader_are_imported_and_other_names_are_ignored()
    {
        var asset = MarketDataFixture.Asset("BTCUSDT", Timeframe.M15, Timeframe.H1);
        var old = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-03.csv", 96);
        Rescan(asset);
        var fresh = _data.WriteFile("BTCUSDT", "BTCUSDT-15m-2026-11-04.csv", 30);
        var hours = _data.WriteFile("BTCUSDT", "BTCUSDT-1h-2026-11-04.csv", 7);
        _data.WriteFile("BTCUSDT", "BTCUSDT-1d-2026-11-04.csv", 1);          // a timeframe the asset does not have
        _data.WriteFile("ETHUSDT", "ETHUSDT-15m-2026-11-04.csv", 30);

        var changed = _data.Importer.ImportFiles(asset, new[]
        {
            "15m/BTCUSDT-15m-2026-11-04.csv",
            "15m/BTCUSDT-15m-2026-11-04.csv",
            "1h/BTCUSDT-1h-2026-11-04.csv",
            "1d/BTCUSDT-1d-2026-11-04.csv",
            "15m/BTCUSDT-15m-2026-11-09.csv",                 // does not exist
            "1h/BTCUSDT-15m-2026-11-04.csv",                  // wrong folder
            "15m/../../ETHUSDT/15m/ETHUSDT-15m-2026-11-04.csv",
            "../ETHUSDT/15m/ETHUSDT-15m-2026-11-04.csv",
            "15m/ETHUSDT-15m-2026-11-04.csv",
            "manifest.json"
        });

        Assert.Equal(new[] { Timeframe.M15, Timeframe.H1 }, changed);
        Assert.Equal(old.Concat(fresh), _data.Candles("BTCUSDT", Timeframe.M15));
        Assert.Equal(hours, _data.Candles("BTCUSDT", Timeframe.H1));
        Assert.Empty(_data.Candles("BTCUSDT", Timeframe.D1));
        Assert.Empty(_data.Candles("ETHUSDT", Timeframe.M15));
    }

    [Fact]
    public void The_fixture_files_of_the_Downloader_are_imported_as_they_are()
    {
        var folder = Path.GetDirectoryName(_data.FilePath("BTCUSDT", "BTCUSDT-15m-2025-01-01.csv"))!;
        Directory.CreateDirectory(folder);
        foreach (var name in new[] { "BTCUSDT-15m-2024-12-31.csv", "BTCUSDT-15m-2025-01-01.csv" })
        {
            File.Copy(Fixtures.CandleFile("binance", "BTCUSDT", "15m", name), Path.Combine(folder, name));
        }

        Rescan(MarketDataFixture.Asset("BTCUSDT", Timeframe.M15));

        var candles = _data.Candles("BTCUSDT", Timeframe.M15);
        Assert.Equal(192, candles.Count);
        Assert.Equal(new Candle(1735603200, 92792.05, 92907.03, 92706.02, 92831.10, 148.07186), candles[0]);
        Assert.Equal(new Candle(1735689600, 93576.00, 93702.15, 93489.03, 93656.18, 175.85673), candles[96]); // milliseconds, then microseconds
    }

    private IReadOnlyList<Timeframe> Rescan(Asset asset)
    {
        return _data.Importer.Rescan(asset, _reports.Add, CancellationToken.None);
    }

    private static int AssetImporter_BatchSize()
    {
        return TradeViewer.Infrastructure.MarketData.Import.AssetImporter.BatchSize;
    }
}
