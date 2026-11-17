using System.Text.Json;
using TradeViewer.Contracts.Drawings;
using TradeViewer.Web.Drawings;

namespace TradeViewer.UnitTests.Web;

/// <summary>DR2: what the user does shows at once, is saved, and can be undone and redone; locked stays locked.</summary>
public class DrawingSessionTests
{
    private static readonly DateTimeOffset Time = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeRemote _remote = new FakeRemote();
    private readonly DrawingSaveQueue _queue;
    private readonly DrawingSession _session;
    private int _changes;

    public DrawingSessionTests()
    {
        _queue = new DrawingSaveQueue(_remote, _ => Task.CompletedTask);
        _session = new DrawingSession(_queue);
        _session.Changed += () => _changes++;
        _session.Show("binance", "BTCUSDT", Array.Empty<DrawingRecord>());
    }

    [Fact]
    public async Task Create_change_and_delete_show_at_once_and_reach_the_server()
    {
        var line = Line("BTCUSDT");
        _session.Created(line);
        var moved = line with { Anchors = new[] { Anchor(2, 101), Anchor(8, 130) } };
        _session.Moved(moved);
        await _queue.WhenIdleAsync();

        Assert.Equal(new[] { Json(moved) }, _session.Drawings.Select(Json));
        Assert.Equal(Json(moved), Json(_remote.Stored[line.Id]));

        Assert.True(_session.Delete(line.Id));
        await _queue.WhenIdleAsync();

        Assert.Empty(_session.Drawings);
        Assert.Empty(_remote.Stored);
        Assert.True(_changes >= 3);
    }

    [Fact]
    public async Task Undo_and_redo_walk_through_create_change_and_delete()
    {
        var line = Line("BTCUSDT");
        var moved = line with { Anchors = new[] { Anchor(2, 101), Anchor(8, 130) } };
        _session.Created(line);
        _session.Moved(moved);
        _session.Delete(line.Id);

        Assert.True(_session.Undo()); // the delete: back as moved
        Assert.Equal(Json(moved), Json(_session.Drawings.Single()));
        Assert.True(_session.Undo()); // the move
        Assert.Equal(Json(line), Json(_session.Drawings.Single()));
        Assert.True(_session.Undo()); // the creation
        Assert.Empty(_session.Drawings);
        Assert.False(_session.Undo());

        Assert.True(_session.Redo());
        Assert.True(_session.Redo());
        Assert.Equal(Json(moved), Json(_session.Drawings.Single()));
        await _queue.WhenIdleAsync();
        Assert.Equal(Json(moved), Json(_remote.Stored[line.Id])); // an undone delete is a PUT of the same record
    }

    [Fact]
    public void A_new_action_drops_what_could_be_redone()
    {
        var line = Line("BTCUSDT");
        _session.Created(line);
        _session.Undo();
        _session.Created(Line("BTCUSDT"));

        Assert.False(_session.CanRedo);
        Assert.True(_session.CanUndo);
    }

    [Fact]
    public void Each_asset_has_its_own_history_which_stays_while_another_is_shown()
    {
        _session.Created(Line("BTCUSDT"));
        _session.Show("binance", "ETHUSDT", Array.Empty<DrawingRecord>());

        Assert.False(_session.CanUndo);

        _session.Show("binance", "BTCUSDT", Array.Empty<DrawingRecord>());
        Assert.True(_session.CanUndo);
    }

    [Fact]
    public void A_locked_drawing_can_only_be_unlocked_not_changed_moved_or_deleted()
    {
        var locked = Line("BTCUSDT") with { Locked = true };
        _session.Show("binance", "BTCUSDT", new[] { locked });

        Assert.False(_session.Edit(locked with { Style = locked.Style with { Color = "#F23645" } }));
        Assert.False(_session.Delete(locked.Id));
        _session.Moved(locked with { Anchors = new[] { Anchor(5, 1), Anchor(6, 2) } });
        Assert.Equal(Json(locked), Json(_session.Drawings.Single()));

        Assert.True(_session.Edit(locked with { Locked = false }));
        Assert.False(_session.Drawings.Single().Locked);
        Assert.True(_session.Delete(locked.Id));
    }

    [Fact]
    public void A_change_to_nothing_new_is_not_an_action()
    {
        var line = Line("BTCUSDT");
        _session.Show("binance", "BTCUSDT", new[] { line });

        Assert.True(_session.Edit(line with { }));
        Assert.False(_session.CanUndo);
    }

    [Fact]
    public async Task Reading_the_drawings_again_keeps_the_changes_the_server_has_not_confirmed()
    {
        var stored = Line("BTCUSDT");
        var other = Line("BTCUSDT");
        _session.Show("binance", "BTCUSDT", new[] { stored, other });

        _remote.Hold();
        var moved = stored with { Anchors = new[] { Anchor(3, 103), Anchor(9, 140) } };
        _session.Moved(moved);
        _session.Delete(other.Id);
        var created = Line("BTCUSDT");
        _session.Created(created);

        // The window reads the drawings again while the saves are on their way (another window, a reconnect).
        _session.Show("binance", "BTCUSDT", new[] { stored, other });

        Assert.Equal(new[] { Json(moved), Json(created) }, _session.Drawings.Select(Json));
        _remote.Release();
        await _queue.WhenIdleAsync();
    }

    [Fact]
    public void The_selection_follows_the_user_and_goes_with_its_drawing()
    {
        var line = Line("BTCUSDT");
        _session.Created(line);

        _session.Select(line.Id);
        Assert.Same(_session.Drawings.Single(), _session.Selected);

        _session.Select(Guid.NewGuid()); // unknown: nothing selected
        Assert.Null(_session.SelectedId);

        _session.Select(line.Id);
        _session.Delete(line.Id);
        Assert.Null(_session.SelectedId);
    }

    [Fact]
    public void A_drawing_of_another_asset_from_the_chart_is_ignored()
    {
        _session.Created(Line("ETHUSDT"));

        Assert.Empty(_session.Drawings);
        Assert.False(_session.CanUndo);
    }

    private static AnchorRecord Anchor(int hours, double price)
    {
        return new AnchorRecord(Time.AddHours(hours), price);
    }

    private static DrawingRecord Line(string symbol)
    {
        return new DrawingRecord(Guid.NewGuid(), "binance", symbol, "trendLine", 1, false, new[] { Anchor(0, 100), Anchor(6, 120) }, new DrawingStyleRecord("#2962FF", 2, "solid"));
    }

    private static string Json(DrawingRecord drawing)
    {
        return JsonSerializer.Serialize(drawing);
    }
}
