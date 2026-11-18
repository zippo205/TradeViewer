using Microsoft.Extensions.Logging.Abstractions;
using TradeViewer.Infrastructure.MarketData.Downloader;
using TradeViewer.UnitTests.Application.Assets;

namespace TradeViewer.UnitTests.Infrastructure.MarketData;

/// <summary>DL4: since when the Downloader does not answer; a change is told to the open windows once.</summary>
public class DownloaderReachabilityTests
{
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeNotifier _notifier = new FakeNotifier();
    private readonly FixedClock _clock = new FixedClock { Now = Start };
    private readonly DownloaderReachability _reachability;

    public DownloaderReachabilityTests()
    {
        _reachability = new DownloaderReachability(_notifier, _clock, NullLogger<DownloaderReachability>.Instance);
    }

    [Fact]
    public void Before_the_first_question_nothing_is_known()
    {
        var state = _reachability.Current;

        Assert.Null(state.Reachable);
        Assert.Null(state.UnreachableSince);
        Assert.Null(state.CheckedAt);
    }

    [Fact]
    public async Task Unreachable_since_is_the_first_failure_of_a_run_and_goes_with_the_next_answer()
    {
        await ReportAtAsync(0, true);
        await ReportAtAsync(5, false, "Connection refused");
        await ReportAtAsync(10, false, "The Downloader did not answer in time.");

        var away = _reachability.Current;
        Assert.False(away.Reachable);
        Assert.Equal(Start.AddMinutes(5), away.UnreachableSince);
        Assert.Equal(Start.AddMinutes(10), away.CheckedAt);
        Assert.Equal("The Downloader did not answer in time.", away.Reason);

        await ReportAtAsync(12, true);

        var back = _reachability.Current;
        Assert.True(back.Reachable);
        Assert.Null(back.UnreachableSince);
        Assert.Null(back.Reason);
        Assert.Equal(Start.AddMinutes(12), back.CheckedAt);
    }

    [Fact]
    public async Task A_failure_right_after_the_start_counts_from_then()
    {
        await ReportAtAsync(1, false, "Connection refused");

        Assert.Equal(Start.AddMinutes(1), _reachability.Current.UnreachableSince);
    }

    [Fact]
    public async Task Only_a_change_is_sent_to_the_windows()
    {
        await ReportAtAsync(0, true);
        await ReportAtAsync(1, true);
        await ReportAtAsync(2, false, "x");
        await ReportAtAsync(3, false, "x");
        await ReportAtAsync(4, true);

        Assert.Equal(new[] { "DownloaderStatusChanged True", "DownloaderStatusChanged False", "DownloaderStatusChanged True" }, _notifier.Events);
    }

    [Fact]
    public async Task Reports_from_several_threads_keep_the_state_whole()
    {
        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => _reachability.ReportAsync(i % 2 == 0, "x"))));

        var state = _reachability.Current;
        Assert.NotNull(state.Reachable);
        Assert.Equal(state.Reachable == false, state.UnreachableSince != null);
    }

    private Task ReportAtAsync(int minutes, bool reachable, string? reason = null)
    {
        _clock.Now = Start.AddMinutes(minutes);
        return _reachability.ReportAsync(reachable, reason);
    }
}
