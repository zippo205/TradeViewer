using TradeViewer.Contracts.Overlays;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Scenarios;

namespace TradeViewer.UnitTests.Web;

/// <summary>SC4, SC7, SC21: the list of the scenario page and what the chart shows on each level.</summary>
public class ScenarioViewsTests
{
    private static readonly IReadOnlyList<ExpertScenarioSummary> Heads = new[]
    {
        Head("xrpusdt-1d-2024-11-X1", "XRPUSDT", "trade-win", draft: true),
        Head("btcusdt-4h-2026-08-X1", "BTCUSDT", "no-trade", draft: true),
        Head("btcusdt-1d-2025-01-X1", "BTCUSDT", "trade-loss", draft: false),
        new ExpertScenarioSummary("broken", null, null, null, null, false, null, null, null, "Not valid JSON"),
    };

    [Fact]
    public void The_list_is_ordered_by_id_and_a_column_again_turns_the_order()
    {
        var view = new ExpertScenarioListView();
        view.Set(Heads);

        Assert.Equal(new[] { "broken", "btcusdt-1d-2025-01-X1", "btcusdt-4h-2026-08-X1", "xrpusdt-1d-2024-11-X1" }, Ids(view));
        view.SortBy("verdict");
        Assert.Equal(new[] { "broken", "btcusdt-4h-2026-08-X1", "btcusdt-1d-2025-01-X1", "xrpusdt-1d-2024-11-X1" }, Ids(view));
        view.SortBy("verdict");
        Assert.Equal(new[] { "xrpusdt-1d-2024-11-X1", "btcusdt-1d-2025-01-X1", "btcusdt-4h-2026-08-X1", "broken" }, Ids(view));
    }

    [Fact]
    public void Filters_by_asset_verdict_and_draft_keep_the_files_that_cannot_be_read()
    {
        var view = new ExpertScenarioListView();
        view.Set(Heads);

        view.Asset = "BTCUSDT";
        Assert.Equal(new[] { "broken", "btcusdt-1d-2025-01-X1", "btcusdt-4h-2026-08-X1" }, Ids(view));
        view.Draft = "final";
        Assert.Equal(new[] { "broken", "btcusdt-1d-2025-01-X1" }, Ids(view));
        view.Asset = ExpertScenarioListView.AnyValue;
        view.Draft = ExpertScenarioListView.AnyValue;
        view.Verdict = "no-trade";
        Assert.Equal(new[] { "broken", "btcusdt-4h-2026-08-X1" }, Ids(view));
        Assert.Equal(new[] { "BTCUSDT", "XRPUSDT" }, view.Assets);
    }

    [Fact]
    public void Previous_and_next_follow_the_list_as_it_is_shown()
    {
        var view = new ExpertScenarioListView();
        view.Set(Heads);
        view.SortBy("asset");

        Assert.Equal("btcusdt-4h-2026-08-X1", view.Step("btcusdt-1d-2025-01-X1", +1));
        Assert.Equal("xrpusdt-1d-2024-11-X1", view.Step("btcusdt-4h-2026-08-X1", +1));
        Assert.Null(view.Step("xrpusdt-1d-2024-11-X1", +1));
        Assert.Null(view.Step("broken", -1));
        Assert.Equal("broken", view.Step(null, +1)); // nothing open: next is the first
        Assert.Null(view.Step(null, -1));
    }

    [Fact]
    public void A_level_shows_its_own_groups_and_those_of_the_higher_levels()
    {
        var levels = new ScenarioLevelView();
        levels.Open(new[] { new ScenarioLevelRecord("context", "1d"), new ScenarioLevelRecord("setup", "4h"), new ScenarioLevelRecord("trigger", "1h") });
        var groups = new[] { Group("waves", "context"), Group("wave1", "setup"), Group("wave2", "setup"), Group("trade", "trigger") };

        Assert.Equal(("context", "1d"), (levels.Shown, levels.Timeframe));
        Assert.Equal(new[] { "wave1", "wave2", "trade" }, levels.HiddenKeys(groups));
        levels.Show("setup");
        Assert.Equal("4h", levels.Timeframe);
        Assert.Equal(new[] { "trade" }, levels.HiddenKeys(groups));
        levels.Show("trigger");
        Assert.Empty(levels.HiddenKeys(groups));
    }

    [Fact]
    public void A_hidden_level_stays_hidden_on_every_level_and_after_opening_the_next_scenario()
    {
        var levels = new ScenarioLevelView();
        var records = new[] { new ScenarioLevelRecord("context", "4h"), new ScenarioLevelRecord("setup", "1h"), new ScenarioLevelRecord("trigger", "15m") };
        levels.Open(records);
        levels.Toggle("context");
        levels.Show("trigger");

        Assert.Equal(new[] { "waves" }, levels.HiddenKeys(new[] { Group("waves", "context"), Group("trade", "trigger") }));

        levels.Open(records);
        Assert.Equal("context", levels.Shown); // a scenario opens on its context
        Assert.True(levels.IsHidden("context"));
        levels.Toggle("context");
        Assert.False(levels.IsHidden("context"));
    }

    private static ExpertScenarioSummary Head(string id, string symbol, string verdict, bool draft)
    {
        return new ExpertScenarioSummary(id, "binance", symbol, id.Contains("-4h-", StringComparison.Ordinal) ? "4h" : "1d", verdict, draft, null, null, null);
    }

    private static OverlayGroupRecord Group(string key, string level)
    {
        return new OverlayGroupRecord(key, key, "waves", null, Array.Empty<OverlayShapeRecord>(), level);
    }

    private static IReadOnlyList<string> Ids(ExpertScenarioListView view)
    {
        return view.Shown().Select(head => head.Id).ToList();
    }
}
