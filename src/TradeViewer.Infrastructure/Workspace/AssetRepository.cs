using System.Globalization;
using Microsoft.Data.Sqlite;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.Workspace;

public sealed class AssetRepository : IAssetRepository
{
    private const string AssetColumns =
        "source, symbol, display_name, asset_class, tick_size, session_type, session_time_zone, session_open, session_close, source_timeframes, position";

    private const string DateFormat = "yyyy-MM-dd";

    private readonly WorkspaceDatabase _database;
    private readonly TimeProvider _time;

    public AssetRepository(WorkspaceDatabase database, TimeProvider time)
    {
        _database = database;
        _time = time;
    }

    public async Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        return Read(workspace, "1 = 1", null);
    }

    public async Task<Asset?> GetAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        return Find(workspace, source, symbol);
    }

    public async Task<Asset?> AddAsync(SymbolInfo info, AssetHistory history, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var transaction = workspace.Connection.BeginTransaction();

        using (var insert = workspace.CreateCommand(
            "INSERT INTO assets (" + AssetColumns + ", subscription_sent, added_at) "
            + "SELECT $source, $symbol, $displayName, $assetClass, $tickSize, $sessionType, $timeZone, $open, $close, $timeframes, "
            + "COALESCE((SELECT MAX(position) FROM assets WHERE asset_class = $assetClass), 0) + 1, 1, $addedAt "
            + "WHERE NOT EXISTS (SELECT 1 FROM assets WHERE source = $source AND symbol = $symbol);"))
        {
            insert.Transaction = transaction;
            insert.Parameters.AddWithValue("$source", info.Source);
            insert.Parameters.AddWithValue("$symbol", info.Symbol);
            insert.Parameters.AddWithValue("$displayName", info.DisplayName);
            insert.Parameters.AddWithValue("$assetClass", AssetCodes.Code(info.AssetClass));
            insert.Parameters.AddWithValue("$tickSize", info.TickSize);
            insert.Parameters.AddWithValue("$sessionType", AssetCodes.Code(info.Session.Type));
            insert.Parameters.AddWithValue("$timeZone", info.Session.TimeZone);
            insert.Parameters.AddWithValue("$open", (object?)info.Session.Open ?? DBNull.Value);
            insert.Parameters.AddWithValue("$close", (object?)info.Session.Close ?? DBNull.Value);
            insert.Parameters.AddWithValue("$timeframes", string.Join(",", info.SourceTimeframes.Select(Timeframes.Code)));
            insert.Parameters.AddWithValue("$addedAt", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

            if (insert.ExecuteNonQuery() == 0)
            {
                return null; // exists already
            }
        }

        WriteHistory(workspace, transaction, info.Source, info.Symbol, history);
        transaction.Commit();

        return Find(workspace, info.Source, info.Symbol);
    }

    public async Task SetHistoryAsync(string source, string symbol, AssetHistory history, bool subscriptionSent, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var transaction = workspace.Connection.BeginTransaction();

        using (var delete = workspace.CreateCommand("DELETE FROM asset_history WHERE source = $source AND symbol = $symbol;"))
        {
            delete.Transaction = transaction;
            AddKey(delete, source, symbol);
            delete.ExecuteNonQuery();
        }

        WriteHistory(workspace, transaction, source, symbol, history);

        using (var update = workspace.CreateCommand("UPDATE assets SET subscription_sent = $sent WHERE source = $source AND symbol = $symbol;"))
        {
            update.Transaction = transaction;
            AddKey(update, source, symbol);
            update.Parameters.AddWithValue("$sent", subscriptionSent ? 1 : 0);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public async Task SetSubscriptionSentAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var update = workspace.CreateCommand("UPDATE assets SET subscription_sent = 1 WHERE source = $source AND symbol = $symbol;");
        AddKey(update, source, symbol);
        update.ExecuteNonQuery();
    }

    public async Task<IReadOnlyList<Asset>> GetUnsentSubscriptionsAsync(CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        return Read(workspace, "subscription_sent = 0", null);
    }

    public async Task<bool> RemoveAsync(string source, string symbol, bool unsubscribePending, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var transaction = workspace.Connection.BeginTransaction();

        // The history goes with the asset (ON DELETE CASCADE). Later epics delete drawings and chart settings here too (AS5).
        using (var delete = workspace.CreateCommand("DELETE FROM assets WHERE source = $source AND symbol = $symbol;"))
        {
            delete.Transaction = transaction;
            AddKey(delete, source, symbol);
            if (delete.ExecuteNonQuery() == 0)
            {
                return false;
            }
        }

        if (unsubscribePending)
        {
            using var pending = workspace.CreateCommand(
                "INSERT INTO pending_unsubscriptions (source, symbol, requested_at) VALUES ($source, $symbol, $at) ON CONFLICT DO NOTHING;");
            pending.Transaction = transaction;
            AddKey(pending, source, symbol);
            pending.Parameters.AddWithValue("$at", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            pending.ExecuteNonQuery();
        }

        transaction.Commit();
        return true;
    }

    public async Task<IReadOnlyList<(string Source, string Symbol)>> GetPendingUnsubscriptionsAsync(CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("SELECT source, symbol FROM pending_unsubscriptions ORDER BY requested_at, source, symbol;");

        var pending = new List<(string, string)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            pending.Add((reader.GetString(0), reader.GetString(1)));
        }

        return pending;
    }

    public async Task<bool> SetOrderAsync(AssetClass assetClass, IReadOnlyList<(string Source, string Symbol)> order, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var transaction = workspace.Connection.BeginTransaction();

        // Checked inside the transaction: another window may have added or removed an asset a moment ago.
        var current = new List<(string, string)>();
        using (var select = workspace.CreateCommand("SELECT source, symbol FROM assets WHERE asset_class = $assetClass;"))
        {
            select.Transaction = transaction;
            select.Parameters.AddWithValue("$assetClass", AssetCodes.Code(assetClass));
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                current.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        if (!WatchlistOrder.IsValid(current, order))
        {
            return false;
        }

        for (var index = 0; index < order.Count; index++)
        {
            using var update = workspace.CreateCommand("UPDATE assets SET position = $position WHERE source = $source AND symbol = $symbol;");
            update.Transaction = transaction;
            AddKey(update, order[index].Source, order[index].Symbol);
            update.Parameters.AddWithValue("$position", index + 1);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
        return true;
    }

    public async Task ClearPendingUnsubscriptionAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        using var workspace = await _database.OpenAsync(cancellationToken);
        using var command = workspace.CreateCommand("DELETE FROM pending_unsubscriptions WHERE source = $source AND symbol = $symbol;");
        AddKey(command, source, symbol);
        command.ExecuteNonQuery();
    }

    private static Asset? Find(WorkspaceConnection workspace, string source, string symbol)
    {
        return Read(workspace, "source = $source AND symbol = $symbol", command => AddKey(command, source, symbol)).FirstOrDefault();
    }

    private static List<Asset> Read(WorkspaceConnection workspace, string where, Action<SqliteCommand>? addParameters)
    {
        // The history of all matching assets first, then the assets themselves.
        var histories = new Dictionary<(string, string), Dictionary<Timeframe, DateOnly?>>();
        using (var command = workspace.CreateCommand(
            "SELECT source, symbol, timeframe, history_from FROM asset_history WHERE (source, symbol) IN (SELECT source, symbol FROM assets WHERE " + where + ");"))
        {
            addParameters?.Invoke(command);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!Timeframes.TryParse(reader.GetString(2), out var timeframe))
                {
                    continue;
                }

                var key = (reader.GetString(0), reader.GetString(1));
                if (!histories.TryGetValue(key, out var starts))
                {
                    starts = new Dictionary<Timeframe, DateOnly?>();
                    histories[key] = starts;
                }

                starts[timeframe] = reader.IsDBNull(3)
                    ? null
                    : DateOnly.ParseExact(reader.GetString(3), DateFormat, CultureInfo.InvariantCulture);
            }
        }

        var assets = new List<Asset>();
        using (var command = workspace.CreateCommand("SELECT " + AssetColumns + ", subscription_sent FROM assets WHERE " + where + " ORDER BY asset_class, position;"))
        {
            addParameters?.Invoke(command);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = reader.GetString(0);
                var symbol = reader.GetString(1);
                AssetCodes.TryParse(reader.GetString(3), out AssetClass assetClass);
                AssetCodes.TryParse(reader.GetString(5), out SessionType sessionType);

                var timeframes = new List<Timeframe>();
                foreach (var code in reader.GetString(9).Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Timeframes.TryParse(code, out var timeframe))
                    {
                        timeframes.Add(timeframe);
                    }
                }

                var info = new SymbolInfo(
                    source,
                    symbol,
                    reader.GetString(2),
                    assetClass,
                    reader.GetDouble(4),
                    new Session(sessionType, reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8)),
                    timeframes);

                histories.TryGetValue((source, symbol), out var starts);
                assets.Add(new Asset(info, new AssetHistory(starts ?? new Dictionary<Timeframe, DateOnly?>()), reader.GetInt32(10), reader.GetInt64(11) != 0));
            }
        }

        return assets;
    }

    private static void WriteHistory(WorkspaceConnection workspace, SqliteTransaction transaction, string source, string symbol, AssetHistory history)
    {
        foreach (var timeframe in history.Timeframes)
        {
            using var insert = workspace.CreateCommand(
                "INSERT INTO asset_history (source, symbol, timeframe, history_from) VALUES ($source, $symbol, $timeframe, $from);");
            insert.Transaction = transaction;
            AddKey(insert, source, symbol);
            insert.Parameters.AddWithValue("$timeframe", Timeframes.Code(timeframe));
            var start = history.StartOf(timeframe);
            insert.Parameters.AddWithValue("$from", start == null ? DBNull.Value : start.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
            insert.ExecuteNonQuery();
        }
    }

    private static void AddKey(SqliteCommand command, string source, string symbol)
    {
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$symbol", symbol);
    }
}
