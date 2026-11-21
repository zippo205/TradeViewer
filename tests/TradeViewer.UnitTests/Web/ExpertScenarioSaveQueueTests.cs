using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Drawings;
using TradeViewer.Web.Scenarios;

namespace TradeViewer.UnitTests.Web;

/// <summary>SC13, SC14, WS1 and priority 1: saving scenarios in the background, the id of the first save, retry, "not saved".</summary>
public class ExpertScenarioSaveQueueTests
{
    private readonly FakeRemote _remote = new FakeRemote();
    private readonly List<TimeSpan> _waits = new List<TimeSpan>();
    private readonly ExpertScenarioSaveQueue _queue;

    public ExpertScenarioSaveQueueTests()
    {
        _queue = new ExpertScenarioSaveQueue(_remote, delay =>
        {
            _waits.Add(delay);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task The_first_save_takes_the_id_and_reports_it()
    {
        var key = Guid.NewGuid();
        var saved = new List<string?>();
        _queue.Saved += (_, scenario) => saved.Add(scenario.Id);

        _queue.Put(key, Scenario(null, "a"));
        await _queue.WhenIdleAsync();

        Assert.Equal(new[] { "post a" }, _remote.Requests);
        Assert.Equal(new[] { "btcusdt-1d-2024-03-X1" }, saved);
        Assert.Equal("btcusdt-1d-2024-03-X1", _queue.IdOf(key));
        Assert.False(_queue.HasUnsaved);
    }

    [Fact]
    public async Task A_change_made_while_the_first_save_is_on_its_way_is_sent_with_its_id_and_never_creates_a_second_file()
    {
        var key = Guid.NewGuid();
        _remote.Hold();

        _queue.Put(key, Scenario(null, "a"));
        _queue.Put(key, Scenario(null, "b"));
        _queue.Put(key, Scenario(null, "c"));
        Assert.True(_queue.IsUnsaved(key));
        _remote.Release();
        await _queue.WhenIdleAsync();

        Assert.Equal(new[] { "post a", "put btcusdt-1d-2024-03-X1 c" }, _remote.Requests);
        Assert.False(_queue.HasUnsaved);
    }

    [Fact]
    public async Task A_failed_save_shows_not_saved_and_is_tried_again_until_it_works()
    {
        var key = Guid.NewGuid();
        _remote.FailNext(2);

        _queue.Put(key, Scenario("btcusdt-1d-2024-03-X1", "a"));
        await _queue.WhenIdleAsync();

        Assert.Equal(3, _remote.Requests.Count);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, _waits);
        Assert.False(_queue.HasUnsaved);
        Assert.Null(_queue.Problem);
    }

    [Fact]
    public async Task A_refused_scenario_stays_unsaved_with_the_message_of_the_server()
    {
        var key = Guid.NewGuid();
        _remote.RefuseNext("The drawing is broken.");

        _queue.Put(key, Scenario("btcusdt-1d-2024-03-X1", "a"));
        await _queue.WhenIdleAsync();

        Assert.True(_queue.HasUnsaved);
        Assert.Equal("Not saved: The drawing is broken.", _queue.Problem);
        Assert.Equal("a", _queue.UnsentOf("btcusdt-1d-2024-03-X1")!.Reason);
    }

    [Fact]
    public async Task Unsent_and_the_key_are_found_by_the_id_so_opening_the_scenario_again_keeps_the_change()
    {
        var key = Guid.NewGuid();
        _remote.Hold();

        _queue.Put(key, Scenario("btcusdt-1d-2024-03-X1", "a"));

        Assert.Equal("a", _queue.UnsentOf("btcusdt-1d-2024-03-X1")!.Reason);
        Assert.Equal(key, _queue.KeyOf("btcusdt-1d-2024-03-X1"));
        _remote.Release();
        await _queue.WhenIdleAsync();
        Assert.Null(_queue.UnsentOf("btcusdt-1d-2024-03-X1"));
    }

    [Fact]
    public async Task A_forgotten_scenario_sends_nothing_more_once_the_request_on_its_way_is_back()
    {
        var key = Guid.NewGuid();
        _remote.Hold();
        _queue.Put(key, Scenario("btcusdt-1d-2024-03-X1", "a"));
        _queue.Put(key, Scenario("btcusdt-1d-2024-03-X1", "b"));

        var forgotten = _queue.ForgetAsync(key);
        Assert.False(forgotten.IsCompleted);
        _remote.Release();
        await forgotten;

        Assert.Equal(new[] { "put btcusdt-1d-2024-03-X1 a" }, _remote.Requests);
        Assert.False(_queue.HasUnsaved);
    }

    [Fact]
    public async Task An_exception_of_the_client_counts_as_a_failure_and_is_tried_again()
    {
        var key = Guid.NewGuid();
        _remote.ThrowNext();

        _queue.Put(key, Scenario("btcusdt-1d-2024-03-X1", "a"));
        await _queue.WhenIdleAsync();

        Assert.Single(_remote.Requests);
        Assert.Single(_waits);
        Assert.False(_queue.HasUnsaved);
    }

    private static ExpertScenarioRecord Scenario(string? id, string reason)
    {
        var day = new DateTimeOffset(2024, 3, 5, 0, 0, 0, TimeSpan.Zero);
        return new ExpertScenarioRecord(
            id, "binance", "BTCUSDT", "1d", "long", "no-trade", true, reason, null, day.AddDays(-60), day.AddDays(30), null, null,
            new ScenarioWavesRecord("1d", null, null, new ScenarioPointRecord(day, 100)), null, null, null, null);
    }

    private sealed class FakeRemote : IExpertScenarioRemote
    {
        private TaskCompletionSource? _hold;
        private int _failures;
        private string? _refusal;
        private bool _throw;

        public List<string> Requests { get; } = new List<string>();

        public void Hold() => _hold = new TaskCompletionSource();

        public void Release()
        {
            var hold = _hold;
            _hold = null;
            hold?.SetResult();
        }

        public void FailNext(int count) => _failures = count;

        public void RefuseNext(string message) => _refusal = message;

        public void ThrowNext() => _throw = true;

        public async Task<ScenarioSaveOutcome> SaveAsync(ExpertScenarioRecord scenario)
        {
            if (_throw)
            {
                _throw = false;
                throw new InvalidOperationException("no connection");
            }

            Requests.Add(scenario.Id == null ? "post " + scenario.Reason : "put " + scenario.Id + " " + scenario.Reason);
            if (_hold != null)
            {
                await _hold.Task;
            }

            if (_failures > 0)
            {
                _failures--;
                return new ScenarioSaveOutcome(SaveResult.Retry, Message: "The server is not reachable.");
            }

            if (_refusal != null)
            {
                var message = _refusal;
                _refusal = null;
                return new ScenarioSaveOutcome(SaveResult.Refused, Message: message);
            }

            var now = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
            return new ScenarioSaveOutcome(SaveResult.Saved, scenario with { Id = scenario.Id ?? "btcusdt-1d-2024-03-X1", CreatedAt = scenario.CreatedAt ?? now, UpdatedAt = now });
        }
    }
}
