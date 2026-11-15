using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Store;
using TradeViewer.Infrastructure.Sqlite;
using TradeViewer.IntegrationTests.Workspace;

namespace TradeViewer.IntegrationTests.MarketData;

/// <summary>The candle store: a file replaces its period, pages are read newest first, and the store is a cache (MD5).</summary>
public class CandleStoreTests : IDisposable
{
    private static readonly SeriesKey Btc15m = new SeriesKey("binance", "BTCUSDT", Timeframe.M15);
    private static readonly SeriesKey Btc1h = new SeriesKey("binance", "BTCUSDT", Timeframe.H1);
    private static readonly SeriesKey Eth15m = new SeriesKey("binance", "ETHUSDT", Timeframe.M15);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tradeviewer-tests", Guid.NewGuid().ToString("N"));
    private readonly TestClock _clock = new TestClock();
    private MarketDataDatabase _database;
    private SqliteCandleStore _store;

    public CandleStoreTests()
    {
        Directory.CreateDirectory(_folder);
        _database = NewDatabase(MarketDataSchema.FromEmbeddedScripts());
        _database.Initialize();
        _store = new SqliteCandleStore(_database, _clock);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, true);
    }

    [Fact]
    public void Imported_candles_are_read_back_unchanged()
    {
        var day = Day("BTCUSDT-15m-2026-11-04.csv", 96);

        _store.Import(Btc15m, new[] { day });

        Assert.Equal(day.Candles, _store.GetCandles(Btc15m, null, 5000));
        Assert.Empty(_store.GetCandles(Btc1h, null, 5000));
        Assert.Empty(_store.GetCandles(Eth15m, null, 5000));
    }

    [Fact]
    public void A_page_holds_the_newest_candles_before_a_time_oldest_first()
    {
        var first = Day("BTCUSDT-15m-2026-11-03.csv", 96);
        var second = Day("BTCUSDT-15m-2026-11-04.csv", 96);
        _store.Import(Btc15m, new[] { second, first });
        var all = first.Candles.Concat(second.Candles).ToList();

        var newest = _store.GetCandles(Btc15m, null, 10);
        var older = _store.GetCandles(Btc15m, newest[0].Time, 100);
        var oldest = _store.GetCandles(Btc15m, older[0].Time, 100);

        Assert.Equal(all.Skip(182), newest);
        Assert.Equal(all.Skip(82).Take(100), older);
        Assert.Equal(all.Take(82), oldest);
        Assert.Empty(_store.GetCandles(Btc15m, all[0].Time, 100));
    }

    [Fact]
    public void Candles_from_a_time_on_are_read_oldest_first()
    {
        var day = Day("BTCUSDT-15m-2026-11-04.csv", 96);
        _store.Import(Btc15m, new[] { day });

        var tail = _store.GetCandlesFrom(Btc15m, day.Candles[90].Time);

        Assert.Equal(day.Candles.Skip(90), tail);
    }

    [Fact]
    public void A_rewritten_file_replaces_its_period_and_nothing_else()
    {
        var before = Day("BTCUSDT-15m-2026-11-03.csv", 96);
        var day = Day("BTCUSDT-15m-2026-11-04.csv", 96);
        _store.Import(Btc15m, new[] { before, day });

        // The official file: one candle gone, one changed.
        var rewritten = day.Candles.Where((_, index) => index != 10).ToList();
        rewritten[20] = rewritten[20] with { Close = 123.456 };
        _store.Import(Btc15m, new[] { new FileImport(day.File, 9000, 2, rewritten, null) });

        Assert.Equal(before.Candles.Concat(rewritten), _store.GetCandles(Btc15m, null, 5000));
        var entry = _store.GetImportLog(Btc15m)[day.File.Name];
        Assert.Equal(new ImportLogEntry(day.File.Name, 9000, 2, 95, null), entry);
    }

    [Fact]
    public void A_file_that_broke_the_rules_empties_its_period_and_keeps_the_error()
    {
        var before = Day("BTCUSDT-15m-2026-11-03.csv", 96);
        var day = Day("BTCUSDT-15m-2026-11-04.csv", 96);
        _store.Import(Btc15m, new[] { before, day });

        _store.Import(Btc15m, new[] { new FileImport(day.File, 500, 3, Array.Empty<Candle>(), "BTCUSDT-15m-2026-11-04.csv, line 7: high is below open or close") });

        Assert.Equal(before.Candles, _store.GetCandles(Btc15m, null, 5000));
        var error = Assert.Single(_store.GetImportErrors("binance", "BTCUSDT"));
        Assert.Equal(day.File.Name, error.File);
        Assert.Contains("line 7", error.Error);
        Assert.Empty(_store.GetImportErrors("binance", "ETHUSDT"));

        // The repaired file clears the error.
        _store.Import(Btc15m, new[] { day });
        Assert.Empty(_store.GetImportErrors("binance", "BTCUSDT"));
        Assert.Equal(192, _store.GetCandles(Btc15m, null, 5000).Count);
    }

    [Fact]
    public void The_import_log_tells_size_and_time_of_every_file()
    {
        var day = Day("BTCUSDT-15m-2026-11-04.csv", 96);
        _store.Import(Btc15m, new[] { day });

        var log = _store.GetImportLog(Btc15m);

        Assert.Equal(new ImportLogEntry(day.File.Name, day.SizeBytes, day.Modified, 96, null), Assert.Single(log).Value);
        Assert.Empty(_store.GetImportLog(Btc1h));
    }

    [Fact]
    public void A_file_that_no_longer_exists_is_forgotten_with_its_candles()
    {
        var first = Day("BTCUSDT-15m-2026-11-03.csv", 96);
        var second = Day("BTCUSDT-15m-2026-11-04.csv", 96);
        _store.Import(Btc15m, new[] { first, second });

        _store.RemoveFiles(Btc15m, new[] { first.File });

        Assert.Equal(second.Candles, _store.GetCandles(Btc15m, null, 5000));
        Assert.Equal(new[] { second.File.Name }, _store.GetImportLog(Btc15m).Keys);
    }

    [Fact]
    public void The_ranges_tell_first_and_last_candle_per_timeframe()
    {
        var day = Day("BTCUSDT-15m-2026-11-04.csv", 96);
        var hours = Day("BTCUSDT-1h-2026-11-04.csv", 24);
        _store.Import(Btc1h, new[] { hours });
        _store.Import(Btc15m, new[] { day });
        _store.Import(Eth15m, new[] { Day("ETHUSDT-15m-2026-11-04.csv", 0) }); // a series without candles has no range

        var ranges = _store.GetRanges("binance", "BTCUSDT");

        Assert.Equal(
            new[]
            {
                new SeriesRange(Timeframe.M15, day.Candles[0].Time, day.Candles[95].Time),
                new SeriesRange(Timeframe.H1, hours.Candles[0].Time, hours.Candles[23].Time)
            },
            ranges);
        Assert.Empty(_store.GetRanges("binance", "ETHUSDT"));
    }

    [Fact]
    public void Removing_an_asset_deletes_all_of_it_and_nothing_of_another()
    {
        _store.Import(Btc15m, new[] { Day("BTCUSDT-15m-2026-11-04.csv", 96) });
        _store.Import(Btc1h, new[] { Day("BTCUSDT-1h-2026-11-04.csv", 24) });
        _store.Import(Eth15m, new[] { Day("ETHUSDT-15m-2026-11-04.csv", 96) });
        _store.SetSyncState("binance", "BTCUSDT", new SyncState(10, null, 1));
        _store.SetSyncState("binance", "ETHUSDT", new SyncState(20, null, 1));

        _store.DeleteAsset("binance", "BTCUSDT");

        Assert.Empty(_store.GetCandles(Btc15m, null, 5000));
        Assert.Empty(_store.GetCandles(Btc1h, null, 5000));
        Assert.Empty(_store.GetImportLog(Btc15m));
        Assert.Null(_store.GetSyncState("binance", "BTCUSDT"));
        Assert.Equal(96, _store.GetCandles(Eth15m, null, 5000).Count);
        Assert.Equal(20, _store.GetSyncState("binance", "ETHUSDT")!.LastChange);
        Assert.Equal("0", Scalar("SELECT COUNT(*) FROM candles WHERE series_id NOT IN (SELECT id FROM series);"));
    }

    [Fact]
    public void The_sync_state_is_kept_per_asset()
    {
        Assert.Null(_store.GetSyncState("binance", "BTCUSDT"));

        var status = new SymbolStatus(48100, new[]
        {
            new TimeframeStatus(
                Timeframe.M15,
                new DateOnly(2019, 1, 1),
                DateTimeOffset.UnixEpoch,
                null,
                3,
                new[] { new CoverageGap(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1), "pending") },
                "an error")
        });
        _store.SetSyncState("binance", "BTCUSDT", new SyncState(48100, status, 1000));

        var read = _store.GetSyncState("binance", "BTCUSDT")!;
        Assert.Equal(48100, read.LastChange);
        Assert.Equal(1000, read.CheckedAt);
        var timeframe = Assert.Single(read.LastStatus!.Timeframes);
        Assert.Equal(status.Timeframes[0] with { Gaps = timeframe.Gaps }, timeframe);
        Assert.Equal(status.Timeframes[0].Gaps, timeframe.Gaps);

        _store.SetSyncState("binance", "BTCUSDT", new SyncState(48213, null, 2000));
        Assert.Equal(new SyncState(48213, null, 2000), _store.GetSyncState("binance", "BTCUSDT"));
    }

    [Fact]
    public void The_store_survives_a_restart_and_runs_in_WAL_mode()
    {
        _store.Import(Btc15m, new[] { Day("BTCUSDT-15m-2026-11-04.csv", 96) });

        Restart(MarketDataSchema.FromEmbeddedScripts(), expectRebuilt: false);

        Assert.Equal(96, _store.GetCandles(Btc15m, null, 5000).Count);
        Assert.Equal("wal", Scalar("PRAGMA journal_mode;"));
    }

    [Fact]
    public void A_store_of_a_newer_version_is_set_aside_and_a_new_one_is_created()
    {
        var next = MarketDataSchema.FromEmbeddedScripts().LatestVersion + 1;
        var newer = new MarketDataSchema(MarketDataSchema.FromEmbeddedScripts().Scripts
            .Append(new SchemaScript(next, next.ToString("0000") + "_test.sql", "CREATE TABLE future (id INTEGER PRIMARY KEY);")));
        Restart(newer, expectRebuilt: false);
        _store.Import(Btc15m, new[] { Day("BTCUSDT-15m-2026-11-04.csv", 96) });

        Restart(MarketDataSchema.FromEmbeddedScripts(), expectRebuilt: true);

        Assert.Empty(_store.GetCandles(Btc15m, null, 5000));
        Assert.Single(Directory.GetFiles(_folder, "marketdata.db.broken-*"));
        _store.Import(Btc15m, new[] { Day("BTCUSDT-15m-2026-11-04.csv", 96) });
        Assert.Equal(96, _store.GetCandles(Btc15m, null, 5000).Count);
    }

    [Fact]
    public void A_file_that_is_no_database_is_set_aside_and_a_new_store_is_created()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_database.FilePath + "-wal");
        File.Delete(_database.FilePath + "-shm");
        File.WriteAllText(_database.FilePath, new string('x', 10000));

        Restart(MarketDataSchema.FromEmbeddedScripts(), expectRebuilt: true);

        Assert.Empty(_store.GetCandles(Btc15m, null, 5000));
        var aside = Assert.Single(Directory.GetFiles(_folder, "marketdata.db.broken-*"));
        Assert.Equal(10000, new FileInfo(aside).Length);
    }

    private void Restart(MarketDataSchema schema, bool expectRebuilt)
    {
        SqliteConnection.ClearAllPools();
        _database = NewDatabase(schema);
        Assert.Equal(expectRebuilt, _database.Initialize());
        _store = new SqliteCandleStore(_database, _clock);
    }

    private MarketDataDatabase NewDatabase(MarketDataSchema schema)
    {
        return new MarketDataDatabase(Path.Combine(_folder, MarketDataDatabase.FileName), schema, _clock, NullLogger<MarketDataDatabase>.Instance);
    }

    private string? Scalar(string sql)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A file with evenly spaced candles over its period; prices differ per candle so mix-ups show.</summary>
    private static FileImport Day(string name, int count)
    {
        CandleFileRules.TryParseName(name, out var file);
        var step = count == 0 ? 0 : (file!.PeriodEnd - file.PeriodStart) / count;
        var candles = new List<Candle>();
        for (var index = 0; index < count; index++)
        {
            var price = 100 + (file!.PeriodStart % 1000) + index * 0.25;
            candles.Add(new Candle(file.PeriodStart + index * step, price, price + 2, price - 1, price + 1, 10 + index));
        }

        return new FileImport(file!, 14000 + count, 1, candles, null);
    }
}
