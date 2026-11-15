using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>
/// /api/symbols, /api/assets and /api/settings through the real application, without the Downloader: the
/// assets are added from the files of a temporary output root (AS2 to AS5, MD5, NF5).
/// </summary>
public class AssetsApiTests : IDisposable
{
    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private readonly HttpClient _client;

    public AssetsApiTests()
    {
        // The output root of the test: the fixture files of BTCUSDT.
        foreach (var (timeframe, name) in new[]
        {
            ("15m", "BTCUSDT-15m-2024-12-31.csv"),
            ("15m", "BTCUSDT-15m-2025-01-01.csv"),
            ("1w", "BTCUSDT-1w-2026-10.csv")
        })
        {
            var folder = Path.Combine(_factory.MarketDataRoot, "binance", "BTCUSDT", timeframe);
            Directory.CreateDirectory(folder);
            File.Copy(Fixtures.CandleFile("binance", "BTCUSDT", timeframe, name), Path.Combine(folder, name));
        }

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task The_symbols_of_a_source_are_found_by_typing()
    {
        var all = await _client.GetFromJsonAsync<List<SymbolResponse>>("/api/symbols/binance");
        var found = await _client.GetFromJsonAsync<List<SymbolResponse>>("/api/symbols/binance?q=btc");
        var unknown = await _client.GetAsync("/api/symbols/nasdaq");

        Assert.Equal(13, all!.Count);
        Assert.Equal(new SymbolResponse("binance", "BTCUSDT", "BTC/USDT", "crypto", false), Assert.Single(found!));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task An_added_asset_is_imported_in_the_background_and_listed_with_its_timeframes()
    {
        await SetCompleteHistoryAsync();

        var response = await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        var added = await response.Content.ReadFromJsonAsync<AssetResponse>();
        var asset = await WaitUntilLoadedAsync("BTCUSDT");
        var list = await _client.GetFromJsonAsync<List<AssetResponse>>("/api/assets");
        var symbols = await _client.GetFromJsonAsync<List<SymbolResponse>>("/api/symbols/binance?q=btc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BTC/USDT", added!.DisplayName);
        Assert.Equal(new[] { "15m", "1h", "4h", "1d", "1w" }, asset.Timeframes.Select(timeframe => timeframe.Timeframe));

        var m15 = asset.Timeframes[0];
        Assert.True(m15.Chartable);
        Assert.Equal((2, 2), (m15.FilesImported, m15.FilesWanted));
        Assert.Equal(new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero), m15.FirstCandle);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 23, 45, 0, TimeSpan.Zero), m15.LastCandle);
        Assert.True(asset.Timeframes[4].Chartable);
        Assert.False(asset.Timeframes[1].Chartable); // no 1h files in this output root
        Assert.Empty(asset.Errors);

        Assert.Equal("BTCUSDT", Assert.Single(list!).Symbol);
        Assert.True(Assert.Single(symbols!).Added);
        Assert.True(File.Exists(Path.Combine(_factory.DataPath, "marketdata.db")));
    }

    [Fact]
    public async Task With_the_default_history_old_files_are_not_imported_until_the_history_is_extended()
    {
        // The initial default: 15m only 90 days back, so the files of 2024 and 2025 are not wanted.
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        var recent = await WaitUntilLoadedAsync("BTCUSDT");

        var change = await _client.PutAsJsonAsync(
            "/api/assets/binance/BTCUSDT/history",
            new ChangeHistoryRequest(new Dictionary<string, string?> { ["15m"] = "2025-01-01" }));
        var extended = await WaitUntilLoadedAsync("BTCUSDT", asset => asset.Timeframes[0].Chartable);

        Assert.False(recent.Timeframes[0].Chartable);
        Assert.True(recent.Timeframes[4].Chartable); // 1w is complete by default
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal("2025-01-01", extended.Timeframes[0].HistoryFrom);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), extended.Timeframes[0].FirstCandle);
        Assert.Equal((1, 1), (extended.Timeframes[0].FilesImported, extended.Timeframes[0].FilesWanted));
    }

    [Fact]
    public async Task Wrong_requests_are_answered_with_problem_details()
    {
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));

        var twice = await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        var notListed = await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "NOPEUSDT"));
        var noFolder = await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "ETHUSDT"));
        var unknown = await _client.GetAsync("/api/assets/binance/ETHUSDT");
        var badHistory = await _client.PutAsJsonAsync(
            "/api/assets/binance/BTCUSDT/history",
            new ChangeHistoryRequest(new Dictionary<string, string?> { ["15m"] = "yesterday" }));

        Assert.Equal((HttpStatusCode.Conflict, "asset.exists"), (twice.StatusCode, await CodeAsync(twice)));
        Assert.Equal((HttpStatusCode.NotFound, "symbol.notFound"), (notListed.StatusCode, await CodeAsync(notListed)));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "asset.noData"), (noFolder.StatusCode, await CodeAsync(noFolder)));
        Assert.Equal((HttpStatusCode.NotFound, "asset.notFound"), (unknown.StatusCode, await CodeAsync(unknown)));
        Assert.Equal(HttpStatusCode.BadRequest, badHistory.StatusCode);
        using var problem = JsonDocument.Parse(await badHistory.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("historyFrom.15m", out _));
    }

    [Fact]
    public async Task Reload_rebuild_and_remove_work_on_the_asset()
    {
        await SetCompleteHistoryAsync();
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        await WaitUntilLoadedAsync("BTCUSDT");

        var rescan = await _client.PostAsync("/api/assets/binance/BTCUSDT/rescan", null);
        var rebuild = await _client.PostAsync("/api/assets/binance/BTCUSDT/rebuild", null);
        var rebuilt = await WaitUntilLoadedAsync("BTCUSDT", asset => asset.Timeframes[0].Chartable);
        var remove = await _client.DeleteAsync("/api/assets/binance/BTCUSDT");
        var gone = await _client.GetAsync("/api/assets/binance/BTCUSDT");
        var again = await _client.DeleteAsync("/api/assets/binance/BTCUSDT");

        Assert.Equal(HttpStatusCode.NoContent, rescan.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, rebuild.StatusCode);
        Assert.Equal((2, 2), (rebuilt.Timeframes[0].FilesImported, rebuilt.Timeframes[0].FilesWanted));
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<List<AssetResponse>>("/api/assets"))!);

        // The files of the Downloader are never touched.
        Assert.Equal(3, Directory.GetFiles(_factory.MarketDataRoot, "*.csv", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task The_assets_survive_a_deleted_candle_store_and_are_imported_again_at_the_next_start()
    {
        await SetCompleteHistoryAsync();
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        await WaitUntilLoadedAsync("BTCUSDT");

        // "Restart" on the same folders with the candle store set aside, as if the user had deleted it (MD5).
        var store = Path.Combine(_factory.DataPath, "marketdata.db");
        using var restarted = _factory.Restart(beforeStart: () =>
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var file in new[] { store, store + "-wal", store + "-shm" })
            {
                File.Delete(file);
            }
        });
        using var client = restarted.CreateClient();

        var asset = await WaitUntilLoadedAsync("BTCUSDT", item => item.Timeframes[0].Chartable, client);

        Assert.Equal((2, 2), (asset.Timeframes[0].FilesImported, asset.Timeframes[0].FilesWanted));
        Assert.True(asset.Timeframes[4].Chartable);
    }

    [Fact]
    public async Task After_a_restore_the_assets_are_those_of_the_backup_and_the_candle_store_follows()
    {
        await SetCompleteHistoryAsync();
        var backup = await (await _client.PostAsync("/api/workspace/backups", null)).Content.ReadFromJsonAsync<BackupResponse>();
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        await WaitUntilLoadedAsync("BTCUSDT", asset => asset.Timeframes[0].Chartable);
        var withAsset = await (await _client.PostAsync("/api/workspace/backups", null)).Content.ReadFromJsonAsync<BackupResponse>();

        // Back to the time before the asset existed: it is gone, and so are its candles.
        (await _client.PostAsync("/api/workspace/backups/" + backup!.Name + "/restore", null)).EnsureSuccessStatusCode();
        var store = _factory.Services.GetRequiredService<ICandleStore>();
        for (var attempt = 0; attempt < 500 && store.GetAssets().Count > 0; attempt++)
        {
            await Task.Delay(20);
        }

        Assert.Empty((await _client.GetFromJsonAsync<List<AssetResponse>>("/api/assets"))!);
        Assert.Empty(store.GetAssets());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/candles/binance/BTCUSDT/15m")).StatusCode);

        // And forward again: the asset is back and its candles are imported without anybody asking.
        (await _client.PostAsync("/api/workspace/backups/" + withAsset!.Name + "/restore", null)).EnsureSuccessStatusCode();
        var asset = await WaitUntilLoadedAsync("BTCUSDT", item => item.Timeframes[0].Chartable);

        Assert.Equal((2, 2), (asset.Timeframes[0].FilesImported, asset.Timeframes[0].FilesWanted));
    }

    [Fact]
    public async Task The_default_history_is_read_and_changed_as_a_setting()
    {
        var initial = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");

        var put = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(new Dictionary<string, int?> { ["15m"] = 30, ["1h"] = null }));
        var changed = await _client.GetFromJsonAsync<SettingsResponse>("/api/settings");
        var wrong = await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(new Dictionary<string, int?> { ["15m"] = -1 }));

        Assert.Equal(90, initial!.DefaultHistory["15m"]);
        Assert.Null(initial.DefaultHistory["1d"]);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(30, changed!.DefaultHistory["15m"]);
        Assert.Null(changed.DefaultHistory["1h"]);
        Assert.Null(changed.DefaultHistory["4h"]); // not named: complete
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(30, (await _client.GetFromJsonAsync<SettingsResponse>("/api/settings"))!.DefaultHistory["15m"]);
    }

    private async Task SetCompleteHistoryAsync()
    {
        var all = new Dictionary<string, int?> { ["15m"] = null, ["1h"] = null, ["4h"] = null, ["1d"] = null, ["1w"] = null };
        (await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(all))).EnsureSuccessStatusCode();
    }

    private async Task<AssetResponse> WaitUntilLoadedAsync(string symbol, Func<AssetResponse, bool>? until = null, HttpClient? client = null)
    {
        AssetResponse? asset = null;
        for (var attempt = 0; attempt < 500; attempt++)
        {
            asset = await (client ?? _client).GetFromJsonAsync<AssetResponse>("/api/assets/binance/" + symbol);
            if (!asset!.Loading && (until == null || until(asset)))
            {
                return asset;
            }

            await Task.Delay(20);
        }

        Assert.Fail("The asset did not finish loading: " + JsonSerializer.Serialize(asset));
        return asset!;
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
