using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Scenarios;

namespace TradeViewer.UnitTests.Web;

/// <summary>SC13, SC14 and proposal 10: the scenario in work, whether it changed, undo and redo, the id of the first save.</summary>
public class ExpertScenarioSessionTests
{
    private static readonly DateTimeOffset Day = new DateTimeOffset(2024, 3, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Opening_is_no_change()
    {
        var session = new ExpertScenarioSession(Response(Scenario(null, "a")));

        Assert.False(session.Changed);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void A_new_scenario_waits_for_wave_2_end_before_it_can_be_saved()
    {
        var session = new ExpertScenarioSession(Response(Scenario(null, "a")));
        Assert.True(session.WaitsForWave2End);

        session.Change(Response(Scenario(null, "a", wave2End: true)));
        Assert.False(session.WaitsForWave2End);
    }

    [Fact]
    public void Undo_and_redo_go_through_the_states_of_the_scenario()
    {
        var session = new ExpertScenarioSession(Response(Scenario("x", "a")));
        session.Change(Response(Scenario("x", "b")));
        session.Change(Response(Scenario("x", "c")));

        Assert.Equal("b", session.UndoTarget()!.Reason);
        session.Undone(Response(session.UndoTarget()!));
        Assert.Equal("b", session.Scenario.Reason);
        session.Undone(Response(session.UndoTarget()!));
        Assert.Equal("a", session.Scenario.Reason);
        Assert.False(session.CanUndo);

        session.Redone(Response(session.RedoTarget()!));
        Assert.Equal("b", session.Scenario.Reason);
        Assert.True(session.Changed);
    }

    [Fact]
    public void A_change_after_undo_drops_what_could_be_redone()
    {
        var session = new ExpertScenarioSession(Response(Scenario("x", "a")));
        session.Change(Response(Scenario("x", "b")));
        session.Undone(Response(session.UndoTarget()!));

        session.Change(Response(Scenario("x", "c")));

        Assert.False(session.CanRedo);
    }

    [Fact]
    public void After_the_first_save_every_state_keeps_the_id_also_one_undo_brings_back()
    {
        var session = new ExpertScenarioSession(Response(Scenario(null, "a", wave2End: true)));
        session.Change(Response(Scenario(null, "b", wave2End: true)));
        var created = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        session.Saved(Scenario(null, "b", wave2End: true) with { Id = "btcusdt-1d-2024-03-X1", CreatedAt = created, UpdatedAt = created });

        Assert.Equal("btcusdt-1d-2024-03-X1", session.Scenario.Id);
        Assert.Equal("b", session.Scenario.Reason);
        var back = session.UndoTarget()!;
        Assert.Equal(("btcusdt-1d-2024-03-X1", created), (back.Id, back.CreatedAt!.Value));
        session.Undone(Response(Scenario(null, "a", wave2End: true)));
        Assert.Equal("btcusdt-1d-2024-03-X1", session.Scenario.Id);
    }

    [Fact]
    public void A_save_keeps_a_newer_change_of_the_user()
    {
        var session = new ExpertScenarioSession(Response(Scenario("x", "a")));
        session.Change(Response(Scenario("x", "b")));
        session.Change(Response(Scenario("x", "c")));

        session.Saved(Scenario("x", "b"));

        Assert.Equal("c", session.Scenario.Reason);
    }

    private static ExpertScenarioRecord Scenario(string? id, string reason, bool wave2End = false)
    {
        var waves = wave2End ? new ScenarioWavesRecord("1d", null, null, new ScenarioPointRecord(Day, 100)) : null;
        return new ExpertScenarioRecord(id, "binance", "BTCUSDT", "1d", "long", "no-trade", true, reason, null, Day.AddDays(-60), Day.AddDays(30), null, null, waves, null, null, null, null);
    }

    private static ExpertScenarioResponse Response(ExpertScenarioRecord scenario)
    {
        return new ExpertScenarioResponse(
            scenario,
            Array.Empty<TradeViewer.Contracts.Overlays.OverlayGroupRecord>(),
            Array.Empty<ScenarioProblemRecord>(),
            new[] { new ScenarioLevelRecord("context", "1d"), new ScenarioLevelRecord("setup", "4h"), new ScenarioLevelRecord("trigger", "1h") },
            new ScenarioAssetRecord(true, new Dictionary<string, string?>()));
    }
}
