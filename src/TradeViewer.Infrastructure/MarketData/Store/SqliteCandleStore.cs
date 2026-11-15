using Microsoft.Data.Sqlite;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.MarketData.Store;

public sealed class SqliteCandleStore : ICandleStore
{
    private const string SeriesIdSql = "(SELECT id FROM series WHERE source = $source AND symbol = $symbol AND timeframe = $timeframe)";

    private readonly MarketDataDatabase _database;
    private readonly TimeProvider _time;

    public SqliteCandleStore(MarketDataDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    public void Import(SeriesKey series, IReadOnlyList<FileImport> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();

        var seriesId = GetOrCreateSeriesId(connection, transaction, series);
        var now = _time.GetUtcNow().ToUnixTimeSeconds();

        using var deletePeriod = Command(
            connection,
            transaction,
            "DELETE FROM candles WHERE series_id = $series AND open_time >= $from AND open_time < $to;");
        var deleteFrom = deletePeriod.Parameters.Add("$from", SqliteType.Integer);
        var deleteTo = deletePeriod.Parameters.Add("$to", SqliteType.Integer);
        deletePeriod.Parameters.AddWithValue("$series", seriesId);

        using var insert = Command(
            connection,
            transaction,
            "INSERT INTO candles (series_id, open_time, open, high, low, close, volume) VALUES ($series, $time, $open, $high, $low, $close, $volume);");
        insert.Parameters.AddWithValue("$series", seriesId);
        var time = insert.Parameters.Add("$time", SqliteType.Integer);
        var open = insert.Parameters.Add("$open", SqliteType.Real);
        var high = insert.Parameters.Add("$high", SqliteType.Real);
        var low = insert.Parameters.Add("$low", SqliteType.Real);
        var close = insert.Parameters.Add("$close", SqliteType.Real);
        var volume = insert.Parameters.Add("$volume", SqliteType.Real);

        using var log = Command(
            connection,
            transaction,
            "INSERT INTO import_log (series_id, file, size, modified, candles, error, imported_at) "
            + "VALUES ($series, $file, $size, $modified, $candles, $error, $importedAt) "
            + "ON CONFLICT (series_id, file) DO UPDATE SET size = excluded.size, modified = excluded.modified, "
            + "candles = excluded.candles, error = excluded.error, imported_at = excluded.imported_at;");
        log.Parameters.AddWithValue("$series", seriesId);
        log.Parameters.AddWithValue("$importedAt", now);
        var logFile = log.Parameters.Add("$file", SqliteType.Text);
        var logSize = log.Parameters.Add("$size", SqliteType.Integer);
        var logModified = log.Parameters.Add("$modified", SqliteType.Integer);
        var logCandles = log.Parameters.Add("$candles", SqliteType.Integer);
        var logError = log.Parameters.Add("$error", SqliteType.Text);

        foreach (var file in files)
        {
            // The file replaces its period: a rewritten file can add, change and remove candles.
            deleteFrom.Value = file.File.PeriodStart;
            deleteTo.Value = file.File.PeriodEnd;
            deletePeriod.ExecuteNonQuery();

            var candles = file.Error == null ? file.Candles : Array.Empty<Candle>();
            foreach (var candle in candles)
            {
                time.Value = candle.Time;
                open.Value = candle.Open;
                high.Value = candle.High;
                low.Value = candle.Low;
                close.Value = candle.Close;
                volume.Value = candle.Volume;
                insert.ExecuteNonQuery();
            }

            logFile.Value = file.File.Name;
            logSize.Value = file.SizeBytes;
            logModified.Value = file.Modified;
            logCandles.Value = candles.Count;
            logError.Value = (object?)file.Error ?? DBNull.Value;
            log.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyDictionary<string, ImportLogEntry> GetImportLog(SeriesKey series)
    {
        using var connection = _database.Open();
        using var command = Command(
            connection,
            null,
            "SELECT file, size, modified, candles, error FROM import_log WHERE series_id = " + SeriesIdSql + ";");
        AddSeries(command, series);

        var entries = new Dictionary<string, ImportLogEntry>(StringComparer.Ordinal);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var entry = ReadLogEntry(reader);
            entries[entry.File] = entry;
        }

        return entries;
    }

    public void RemoveFiles(SeriesKey series, IReadOnlyList<CandleFile> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();

        using var deleteCandles = Command(
            connection,
            transaction,
            "DELETE FROM candles WHERE series_id = " + SeriesIdSql + " AND open_time >= $from AND open_time < $to;");
        AddSeries(deleteCandles, series);
        var from = deleteCandles.Parameters.Add("$from", SqliteType.Integer);
        var to = deleteCandles.Parameters.Add("$to", SqliteType.Integer);

        using var deleteLog = Command(connection, transaction, "DELETE FROM import_log WHERE series_id = " + SeriesIdSql + " AND file = $file;");
        AddSeries(deleteLog, series);
        var name = deleteLog.Parameters.Add("$file", SqliteType.Text);

        foreach (var file in files)
        {
            from.Value = file.PeriodStart;
            to.Value = file.PeriodEnd;
            deleteCandles.ExecuteNonQuery();

            name.Value = file.Name;
            deleteLog.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyList<Candle> GetCandles(SeriesKey series, long? before, int limit)
    {
        using var connection = _database.Open();
        using var command = Command(
            connection,
            null,
            "SELECT open_time, open, high, low, close, volume FROM candles WHERE series_id = " + SeriesIdSql
            + " AND open_time < $before ORDER BY open_time DESC LIMIT $limit;");
        AddSeries(command, series);
        command.Parameters.AddWithValue("$before", before ?? long.MaxValue);
        command.Parameters.AddWithValue("$limit", limit);

        var candles = ReadCandles(command, Math.Min(Math.Max(limit, 0), 5000));
        candles.Reverse();
        return candles;
    }

    public IReadOnlyList<Candle> GetCandlesFrom(SeriesKey series, long from)
    {
        using var connection = _database.Open();
        using var command = Command(
            connection,
            null,
            "SELECT open_time, open, high, low, close, volume FROM candles WHERE series_id = " + SeriesIdSql
            + " AND open_time >= $from ORDER BY open_time;");
        AddSeries(command, series);
        command.Parameters.AddWithValue("$from", from);

        return ReadCandles(command, 0);
    }

    public IReadOnlyList<SeriesRange> GetRanges(string source, string symbol)
    {
        using var connection = _database.Open();

        // MIN and MAX per series are two index lookups each; a GROUP BY over the candles would read them all.
        using var command = Command(
            connection,
            null,
            "SELECT s.timeframe, "
            + "(SELECT MIN(open_time) FROM candles WHERE series_id = s.id), "
            + "(SELECT MAX(open_time) FROM candles WHERE series_id = s.id) "
            + "FROM series s WHERE s.source = $source AND s.symbol = $symbol;");
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$symbol", symbol);

        var ranges = new List<SeriesRange>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (!reader.IsDBNull(1) && Timeframes.TryParse(reader.GetString(0), out var timeframe))
                {
                    ranges.Add(new SeriesRange(timeframe, reader.GetInt64(1), reader.GetInt64(2)));
                }
            }
        }

        return ranges.OrderBy(range => Timeframes.Seconds(range.Timeframe)).ToList();
    }

    public IReadOnlyList<ImportLogEntry> GetImportErrors(string source, string symbol)
    {
        using var connection = _database.Open();
        using var command = Command(
            connection,
            null,
            "SELECT l.file, l.size, l.modified, l.candles, l.error FROM import_log l JOIN series s ON s.id = l.series_id "
            + "WHERE s.source = $source AND s.symbol = $symbol AND l.error IS NOT NULL ORDER BY l.file;");
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$symbol", symbol);

        var errors = new List<ImportLogEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            errors.Add(ReadLogEntry(reader));
        }

        return errors;
    }

    public void DeleteAsset(string source, string symbol)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();

        // Candles and import log go with their series (ON DELETE CASCADE).
        foreach (var sql in new[]
        {
            "DELETE FROM series WHERE source = $source AND symbol = $symbol;",
            "DELETE FROM sync_state WHERE source = $source AND symbol = $symbol;"
        })
        {
            using var command = Command(connection, transaction, sql);
            command.Parameters.AddWithValue("$source", source);
            command.Parameters.AddWithValue("$symbol", symbol);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public SyncState? GetSyncState(string source, string symbol)
    {
        using var connection = _database.Open();
        using var command = Command(
            connection,
            null,
            "SELECT last_change, last_status_json, checked_at FROM sync_state WHERE source = $source AND symbol = $symbol;");
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$symbol", symbol);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new SyncState(reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt64(2));
    }

    public void SetSyncState(string source, string symbol, SyncState state)
    {
        using var connection = _database.Open();
        using var command = Command(
            connection,
            null,
            "INSERT INTO sync_state (source, symbol, last_change, last_status_json, checked_at) "
            + "VALUES ($source, $symbol, $lastChange, $status, $checkedAt) "
            + "ON CONFLICT (source, symbol) DO UPDATE SET last_change = excluded.last_change, "
            + "last_status_json = excluded.last_status_json, checked_at = excluded.checked_at;");
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$symbol", symbol);
        command.Parameters.AddWithValue("$lastChange", state.LastChange);
        command.Parameters.AddWithValue("$status", (object?)state.LastStatusJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$checkedAt", state.CheckedAt);
        command.ExecuteNonQuery();
    }

    private static long GetOrCreateSeriesId(SqliteConnection connection, SqliteTransaction transaction, SeriesKey series)
    {
        using (var insert = Command(
            connection,
            transaction,
            "INSERT INTO series (source, symbol, timeframe) VALUES ($source, $symbol, $timeframe) ON CONFLICT DO NOTHING;"))
        {
            AddSeries(insert, series);
            insert.ExecuteNonQuery();
        }

        using var select = Command(connection, transaction, "SELECT id FROM series WHERE source = $source AND symbol = $symbol AND timeframe = $timeframe;");
        AddSeries(select, series);
        return (long)select.ExecuteScalar()!;
    }

    private static List<Candle> ReadCandles(SqliteCommand command, int capacity)
    {
        var candles = new List<Candle>(capacity);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            candles.Add(new Candle(
                reader.GetInt64(0),
                reader.GetDouble(1),
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5)));
        }

        return candles;
    }

    private static ImportLogEntry ReadLogEntry(SqliteDataReader reader)
    {
        return new ImportLogEntry(
            reader.GetString(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private static void AddSeries(SqliteCommand command, SeriesKey series)
    {
        command.Parameters.AddWithValue("$source", series.Source);
        command.Parameters.AddWithValue("$symbol", series.Symbol);
        command.Parameters.AddWithValue("$timeframe", Timeframes.Code(series.Timeframe));
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
