using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.Configuration;
using TradeViewer.Infrastructure.MarketData.Sync;
using TradeViewer.UnitTests.Application.Assets;

namespace TradeViewer.UnitTests.Infrastructure.MarketData;

/// <summary>MD2 and NF5: when the Downloader is asked, what a greater change number leads to, and what happens when it is down.</summary>
public class MarketDataSyncTests
{
    private static readonly Error Unreachable = Error.Unavailable("downloader.unreachable", "The Downloader cannot be reached.");

    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeDownloader _downloader = new FakeDownloader();
    private readonly FakeCandleStore _store = new FakeCandleStore();
    private readonly FakeImportQueue _imports = new FakeImportQueue();
    private readonly FakeNotifier _notifier = new FakeNotifier();
    private readonly FixedClock _clock = new FixedClock { Now = At("15:15:40") };
    private readonly MarketDataSync _sync;

    public MarketDataSyncTests()
    {
        var options = new MarketDataOptions { RootPath = "/unused" };
        options.Downloader.Enabled = true;
        options.Downloader.PollSeconds = 20;
        options.Downloader.PollWindowMinutes = 10;

        _sync = new MarketDataSync(_assets, _downloader, _store, _imports, _notifier, Options.Create(options), _clock, NullLogger<MarketDataSync>.Instance);
        AddAsset("BTCUSDT");
    }

    [Fact]
    public async Task The_first_contact_rescans_the_asset_and_remembers_the_change_number()
    {
        _downloader.Status = Status(48213);

        await _sync.RunOnceAsync(CancellationToken.None);

        Assert.Equal(new[] { "status binance/BTCUSDT" }, _downloader.Calls);
        Assert.Equal(new[] { "rescan binance/BTCUSDT" }, _imports.Work);
        var state = _store.SyncStates[("binance", "BTCUSDT")];
        Assert.Equal(48213, state.LastChange);
        Assert.Same(_downloader.Status, state.LastStatus);
        Assert.Equal(_clock.Now.ToUnixTimeSeconds(), state.CheckedAt);
        Assert.Equal(new[] { "DownloaderStatusChanged True", "AssetStatusChanged binance/BTCUSDT" }, _notifier.Events);
        Assert.True(_sync.DownloaderReachable);
    }

    [Fact]
    public async Task A_greater_change_number_imports_the_changed_files_and_no_other()
    {
        await KnownAsync(48100);
        _downloader.Status = Status(48213);
        _downloader.Changes = new SymbolChanges(48213, true, new[] { "1d/BTCUSDT-1d-2026-11-03.csv", "15m/BTCUSDT-15m-2026-11-04.csv" });

        await RunAtAsync("15:30:30");

        Assert.Equal(new[] { "status binance/BTCUSDT", "changes binance/BTCUSDT" }, _downloader.Calls);
        Assert.Equal(48100, _downloader.ChangesAfter);
        Assert.Equal(new[] { "files binance/BTCUSDT" }, _imports.Work);
        Assert.Equal(new[] { "1d/BTCUSDT-1d-2026-11-03.csv", "15m/BTCUSDT-15m-2026-11-04.csv" }, _imports.Files);
        Assert.Equal(48213, _store.SyncStates[("binance", "BTCUSDT")].LastChange);
    }

    [Fact]
    public async Task Without_a_new_change_nothing_is_imported()
    {
        await KnownAsync(48100);

        await RunAtAsync("15:30:30");

        Assert.Equal(new[] { "status binance/BTCUSDT" }, _downloader.Calls);
        Assert.Empty(_imports.Work);
        Assert.Empty(_notifier.Events);
    }

    [Fact]
    public async Task Changes_the_Downloader_no_longer_knows_lead_to_a_rescan()
    {
        await KnownAsync(5);
        _downloader.Status = Status(48213);
        _downloader.Changes = new SymbolChanges(48213, false, Array.Empty<string>());

        await RunAtAsync("15:30:30");

        Assert.Equal(new[] { "rescan binance/BTCUSDT" }, _imports.Work);
        Assert.Equal(48213, _store.SyncStates[("binance", "BTCUSDT")].LastChange);
    }

    [Fact]
    public async Task After_a_due_time_the_Downloader_is_asked_in_short_intervals_until_the_change_shows_and_is_quiet()
    {
        await KnownAsync(100);

        // Due at 15:30:30: nothing yet, so ask again every 20 seconds.
        Assert.Equal(At("15:30:50"), await RunAtAsync("15:30:30"));
        Assert.Equal(At("15:31:10"), await RunAtAsync("15:30:50"));

        // The 15m file is written: import it and look once more, the cycle writes other timeframes too.
        _downloader.Status = Status(101);
        _downloader.Changes = new SymbolChanges(101, true, new[] { "15m/BTCUSDT-15m-2026-11-04.csv" });
        Assert.Equal(At("15:31:30"), await RunAtAsync("15:31:10"));

        _downloader.Status = Status(102);
        _downloader.Changes = new SymbolChanges(102, true, new[] { "1h/BTCUSDT-1h-2026-11-04.csv" });
        Assert.Equal(At("15:31:50"), await RunAtAsync("15:31:30"));

        // Quiet now: the next check is the next due time.
        _downloader.Calls.Clear();
        var next = await RunAtAsync("15:31:50");
        Assert.Equal(new[] { "status binance/BTCUSDT" }, _downloader.Calls);
        Assert.True(next <= At("15:32:10")); // the loop wakes with every poll ...

        _downloader.Calls.Clear();
        await RunAtAsync("15:32:10");
        await RunAtAsync("15:40:00");
        Assert.Empty(_downloader.Calls); // ... but does not ask the Downloader before 15:45:30

        await RunAtAsync("15:45:30");
        Assert.Equal(new[] { "status binance/BTCUSDT" }, _downloader.Calls);
        Assert.Equal(new[] { "files binance/BTCUSDT", "files binance/BTCUSDT" }, _imports.Work);
    }

    [Fact]
    public async Task A_due_check_gives_up_after_the_poll_window()
    {
        await KnownAsync(100);

        await RunAtAsync("15:30:30");
        await RunAtAsync("15:40:20"); // still inside the 10 minutes
        _downloader.Calls.Clear();
        await RunAtAsync("15:40:40"); // the window is over: last question for this due time
        await RunAtAsync("15:41:00");
        await RunAtAsync("15:45:00");

        Assert.Equal(new[] { "status binance/BTCUSDT" }, _downloader.Calls);

        await RunAtAsync("15:45:30");
        Assert.Equal(2, _downloader.Calls.Count);
    }

    [Fact]
    public async Task While_the_Downloader_downloads_history_the_asset_is_polled_all_the_time()
    {
        await KnownAsync(100);
        _downloader.Status = Status(100, pendingJobs: 7);

        await RunAtAsync("15:30:30");
        _downloader.Status = Status(150, pendingJobs: 3);
        _downloader.Changes = new SymbolChanges(150, true, new[] { "15m/BTCUSDT-15m-2026-08-17.csv" });
        await RunAtAsync("15:30:50");
        _downloader.Status = Status(150, pendingJobs: 3);
        await RunAtAsync("15:31:10");
        _downloader.Calls.Clear();
        await RunAtAsync("15:31:30"); // quiet since the last change, but still downloading

        Assert.Equal(new[] { "status binance/BTCUSDT" }, _downloader.Calls);
        Assert.Contains("AssetStatusChanged binance/BTCUSDT", _notifier.Events);
    }

    [Fact]
    public async Task An_unreachable_Downloader_is_reported_once_keeps_the_last_status_and_is_asked_again_with_every_poll()
    {
        await KnownAsync(100);
        var known = _store.SyncStates[("binance", "BTCUSDT")];
        _notifier.Events.Clear();
        _downloader.StatusError = Unreachable;

        Assert.Equal(At("15:30:50"), await RunAtAsync("15:30:30"));
        await RunAtAsync("15:30:50");
        await RunAtAsync("15:31:10");

        Assert.Equal(new[] { "DownloaderStatusChanged False" }, _notifier.Events);
        Assert.False(_sync.DownloaderReachable);
        Assert.Same(known, _store.SyncStates[("binance", "BTCUSDT")]);
        Assert.Empty(_imports.Work);

        // Back again: the changes of the time in between are fetched by their number.
        _downloader.StatusError = null;
        _downloader.Status = Status(130);
        _downloader.Changes = new SymbolChanges(130, true, new[] { "15m/BTCUSDT-15m-2026-11-04.csv" });
        await RunAtAsync("15:31:30");

        Assert.Equal(new[] { "DownloaderStatusChanged False", "DownloaderStatusChanged True", "AssetStatusChanged binance/BTCUSDT" }, _notifier.Events);
        Assert.Equal(100, _downloader.ChangesAfter);
        Assert.Equal(new[] { "files binance/BTCUSDT" }, _imports.Work);
    }

    [Fact]
    public async Task A_subscription_that_could_not_be_sent_is_sent_again()
    {
        await KnownAsync(100);
        _assets.Assets[0] = _assets.Assets[0] with { SubscriptionSent = false };
        _downloader.SubscribeError = Unreachable;

        await RunAtAsync("15:30:30");
        Assert.False(_assets.Assets[0].SubscriptionSent);

        _downloader.SubscribeError = null;
        _downloader.Calls.Clear();
        await RunAtAsync("15:30:50");
        await RunAtAsync("15:31:10");

        Assert.True(_assets.Assets[0].SubscriptionSent);
        Assert.Equal(1, _downloader.Calls.Count(call => call == "subscribe binance/BTCUSDT"));
        Assert.Same(_assets.Assets[0].History, _downloader.SubscribedHistory);
    }

    [Fact]
    public async Task An_unsubscription_that_could_not_be_sent_is_sent_again_although_the_asset_is_gone()
    {
        await KnownAsync(100);
        await _assets.RemoveAsync("binance", "BTCUSDT", true, CancellationToken.None);
        _downloader.UnsubscribeError = Unreachable;
        _downloader.Calls.Clear();

        await RunAtAsync("15:30:30");
        Assert.Equal(new[] { ("binance", "BTCUSDT") }, _assets.Pending);

        _downloader.UnsubscribeError = null;
        await RunAtAsync("15:30:50");
        await RunAtAsync("15:31:10");

        Assert.Empty(_assets.Pending);
        Assert.Equal(new[] { "unsubscribe binance/BTCUSDT", "unsubscribe binance/BTCUSDT" }, _downloader.Calls); // no status of a removed asset
    }

    [Fact]
    public async Task Nothing_is_sent_again_while_the_Downloader_is_unreachable()
    {
        await KnownAsync(100);
        _assets.Assets[0] = _assets.Assets[0] with { SubscriptionSent = false };
        _downloader.StatusError = Unreachable;
        _downloader.Calls.Clear();

        await RunAtAsync("15:30:30");

        Assert.Equal(new[] { "status binance/BTCUSDT" }, _downloader.Calls);
    }

    [Fact]
    public async Task Every_asset_is_checked_on_its_own_and_a_new_one_at_once()
    {
        await KnownAsync(100);
        await RunAtAsync("15:30:30");
        await RunAtAsync("15:40:40"); // BTCUSDT is done until 15:45:30
        _downloader.Calls.Clear();
        _imports.Work.Clear();

        AddAsset("ETHUSDT");
        await RunAtAsync("15:41:00");

        Assert.Equal(new[] { "status binance/ETHUSDT" }, _downloader.Calls);
        Assert.Equal(new[] { "rescan binance/ETHUSDT" }, _imports.Work);
    }

    private void AddAsset(string symbol)
    {
        var info = new SymbolInfo("binance", symbol, symbol, AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        var history = new AssetHistory(Timeframes.All.ToDictionary(timeframe => timeframe, _ => (DateOnly?)null));
        _assets.Assets.Add(new Asset(info, history, _assets.Assets.Count + 1, true));
    }

    /// <summary>The asset is known with this change number, checked at 15:15:40 and quiet since.</summary>
    private async Task KnownAsync(long lastChange)
    {
        _downloader.Status = Status(lastChange);
        await RunAtAsync("15:15:40"); // first contact
        await RunAtAsync("15:25:40"); // the window of 15:15:30 is over
        _downloader.Calls.Clear();
        _imports.Work.Clear();
        _imports.Files.Clear();
        _notifier.Events.Clear();
    }

    private Task<DateTimeOffset> RunAtAsync(string time)
    {
        _clock.Now = At(time);
        return _sync.RunOnceAsync(CancellationToken.None);
    }

    private static SymbolStatus Status(long lastChange, int pendingJobs = 0)
    {
        return new SymbolStatus(lastChange, new[]
        {
            new TimeframeStatus(Timeframe.M15, null, null, null, pendingJobs, Array.Empty<CoverageGap>(), null)
        });
    }

    private static DateTimeOffset At(string time)
    {
        return new DateTimeOffset(new DateOnly(2026, 11, 4).ToDateTime(TimeOnly.Parse(time), DateTimeKind.Utc));
    }
}
