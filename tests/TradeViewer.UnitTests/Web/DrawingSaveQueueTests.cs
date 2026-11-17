using TradeViewer.Contracts.Drawings;
using TradeViewer.Web.Drawings;

namespace TradeViewer.UnitTests.Web;

/// <summary>NF3, WS1 and priority 1: saving in the background, only the last change, retry, "not saved".</summary>
public class DrawingSaveQueueTests
{
    private readonly FakeRemote _remote = new FakeRemote();
    private readonly List<TimeSpan> _waits = new List<TimeSpan>();
    private readonly DrawingSaveQueue _queue;

    public DrawingSaveQueueTests()
    {
        _queue = new DrawingSaveQueue(_remote, delay =>
        {
            _waits.Add(delay);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task A_change_is_sent_at_once_and_then_nothing_is_unsaved()
    {
        var drawing = Drawing("a");

        _queue.Put(drawing);
        await _queue.WhenIdleAsync();

        Assert.Equal(new[] { "put a" }, _remote.Requests);
        Assert.False(_queue.HasUnsaved);
        Assert.Null(_queue.Problem);
        Assert.Same(drawing, _remote.Stored[drawing.Id]);
    }

    [Fact]
    public async Task Changes_made_while_a_save_is_on_its_way_send_only_the_last()
    {
        var id = Guid.NewGuid();
        _remote.Hold();

        _queue.Put(Drawing("1", id));
        _queue.Put(Drawing("2", id));
        _queue.Put(Drawing("3", id));
        Assert.True(_queue.HasUnsaved);
        _remote.Release();
        await _queue.WhenIdleAsync();

        Assert.Equal(new[] { "put 1", "put 3" }, _remote.Requests);
        Assert.Equal("3", _remote.Stored[id].Text);
        Assert.False(_queue.HasUnsaved);
    }

    [Fact]
    public async Task A_failed_save_shows_not_saved_and_is_tried_again_until_it_works()
    {
        var drawing = Drawing("a");
        _remote.Answer(new SaveOutcome(SaveResult.Retry, "The server is not reachable."), new SaveOutcome(SaveResult.Retry, "Busy."));
        var problems = new List<string?>();
        _queue.StateChanged += () => problems.Add(_queue.Problem);

        _queue.Put(drawing);
        await _queue.WhenIdleAsync();

        Assert.Equal(new[] { "put a", "put a", "put a" }, _remote.Requests);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, _waits);
        Assert.Contains("Not saved: The server is not reachable. Trying again.", problems);
        Assert.Null(_queue.Problem);
        Assert.False(_queue.HasUnsaved);
    }

    [Fact]
    public async Task A_newer_change_made_during_a_failure_is_the_one_tried_again()
    {
        var id = Guid.NewGuid();
        var queue = new DrawingSaveQueue(_remote, _ =>
        {
            // While the queue waits to try again, the user changes the drawing once more.
            if (_remote.Requests.Count == 1)
            {
                _queue2!.Put(Drawing("newer", id));
            }

            return Task.CompletedTask;
        });
        _queue2 = queue;
        _remote.Answer(new SaveOutcome(SaveResult.Retry, "Down."));

        queue.Put(Drawing("older", id));
        await queue.WhenIdleAsync();

        Assert.Equal(new[] { "put older", "put newer" }, _remote.Requests);
        Assert.Equal("newer", _remote.Stored[id].Text);
        Assert.False(queue.HasUnsaved);
    }

    [Fact]
    public async Task A_refused_drawing_stays_unsaved_with_the_message_of_the_server()
    {
        _remote.Answer(new SaveOutcome(SaveResult.Refused, "The width is 1 to 4 pixels."));
        var drawing = Drawing("a");

        _queue.Put(drawing);
        await _queue.WhenIdleAsync();

        Assert.Single(_remote.Requests); // not tried again: it would be refused again
        Assert.True(_queue.HasUnsaved);  // leaving the page warns
        Assert.Equal("Not saved: The width is 1 to 4 pixels.", _queue.Problem);
        Assert.Same(drawing, _queue.Unsent()[drawing.Id]);
    }

    [Fact]
    public async Task Unsent_holds_the_newest_record_or_the_delete_until_the_server_confirms()
    {
        var kept = Drawing("kept");
        var gone = Drawing("gone");
        _remote.Hold();

        _queue.Put(kept);
        _queue.Delete(gone.Id);
        var unsent = _queue.Unsent();
        _remote.Release();
        await _queue.WhenIdleAsync();

        Assert.Same(kept, unsent[kept.Id]);
        Assert.True(unsent.ContainsKey(gone.Id));
        Assert.Null(unsent[gone.Id]);
        Assert.Empty(_queue.Unsent());
    }

    [Fact]
    public async Task An_exception_of_the_client_counts_as_a_failure_and_is_tried_again()
    {
        var queue = new DrawingSaveQueue(new ThrowingOnce(_remote), _ => Task.CompletedTask);

        queue.Put(Drawing("a"));
        await queue.WhenIdleAsync();

        Assert.Equal(new[] { "put a" }, _remote.Requests);
        Assert.False(queue.HasUnsaved);
    }

    private DrawingSaveQueue? _queue2;

    private static DrawingRecord Drawing(string text, Guid? id = null)
    {
        return new DrawingRecord(
            id ?? Guid.NewGuid(),
            "binance",
            "BTCUSDT",
            "textNote",
            1,
            false,
            new[] { new AnchorRecord(new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero), 100) },
            new DrawingStyleRecord("#2962FF", 2, "solid"),
            text);
    }

    private sealed class ThrowingOnce : IDrawingRemote
    {
        private readonly IDrawingRemote _inner;
        private bool _thrown;

        public ThrowingOnce(IDrawingRemote inner)
        {
            _inner = inner;
        }

        public Task<SaveOutcome> PutAsync(DrawingRecord drawing)
        {
            if (!_thrown)
            {
                _thrown = true;
                throw new InvalidOperationException("no connection");
            }

            return _inner.PutAsync(drawing);
        }

        public Task<SaveOutcome> DeleteAsync(Guid id)
        {
            return _inner.DeleteAsync(id);
        }
    }
}
