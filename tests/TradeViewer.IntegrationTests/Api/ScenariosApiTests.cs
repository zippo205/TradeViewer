using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.Drawings;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Contracts.Workspace;
using TradeViewer.IntegrationTests.Scenarios;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>
/// /api/scenarios through the real application (SC6-SC16, SC20, SC21): the 13 converted files are listed and opened
/// without being written, points are snapped by the server on the candles of their level, saves keep the file's
/// unknown fields, and nothing is written outside expert/.
/// </summary>
public class ScenariosApiTests : IAsyncLifetime
{
    private const string Converted = "xrpusdt-1d-2024-11-X1";
    private const long Day = 1_735_603_200; // 2024-12-31 00:00 UTC

    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly ScenarioFolderFixture _folder = new ScenarioFolderFixture();
    private readonly TradeViewerFactory _factory;
    private HttpClient _client = null!;

    public ScenariosApiTests()
    {
        _factory = TradeViewerFactory.WithScenarios(_folder.Root);
    }

    public async Task InitializeAsync()
    {
        // BTCUSDT on 2024-12-31 in the three timeframes of a 4h scenario (4h, 1h, 15m), with prices that tell the
        // candles apart: candle k of a day has open 100+k, high 110+k, low 90+k, close 105+k.
        WriteDay("4h", 4 * 3600, 6);
        WriteDay("1h", 3600, 24);
        WriteDay("15m", 900, 96);

        _client = _factory.CreateClient();
        var complete = new Dictionary<string, int?> { ["15m"] = null, ["1h"] = null, ["4h"] = null, ["1d"] = null, ["1w"] = null };
        await _client.PutAsJsonAsync("/api/settings", new SettingsResponse(complete));
        await _client.PostAsJsonAsync("/api/assets", new AddAssetRequest("binance", "BTCUSDT"));
        AssetResponse? last = null;
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var asset = await _client.GetFromJsonAsync<AssetResponse>("/api/assets/binance/BTCUSDT");
            if (!asset!.Loading && new[] { "15m", "1h", "4h" }.All(code => asset.Timeframes.Any(timeframe => timeframe.Timeframe == code && timeframe.Chartable)))
            {
                return;
            }

            last = asset;

            await Task.Delay(20);
        }

        Assert.Fail("The asset did not finish loading: " + JsonSerializer.Serialize(last, Json));
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        _folder.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_status_says_the_editor_is_on_and_can_write()
    {
        var status = await _client.GetFromJsonAsync<ScenariosStatusResponse>("/api/scenarios");

        Assert.Equal(new ScenariosStatusResponse(true, true), status);
    }

    [Fact]
    public async Task Without_a_scenario_folder_the_pages_are_off()
    {
        using var factory = new TradeViewerFactory();
        using var client = factory.CreateClient();

        Assert.Equal(new ScenariosStatusResponse(false, false), await client.GetFromJsonAsync<ScenariosStatusResponse>("/api/scenarios"));
        var list = await client.GetAsync("/api/scenarios/expert");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Contains("scenarios.off", await list.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_list_holds_the_13_converted_scenarios()
    {
        var list = await _client.GetFromJsonAsync<List<ExpertScenarioSummary>>("/api/scenarios/expert");

        Assert.Equal(13, list!.Count);
        Assert.All(list, head => Assert.True(head.Draft));
    }

    [Fact]
    public async Task Opening_a_converted_scenario_draws_it_on_its_levels_and_writes_nothing()
    {
        var written = File.GetLastWriteTimeUtc(_folder.PathOf(Converted));
        var text = File.ReadAllText(_folder.PathOf(Converted));

        var response = await _client.GetFromJsonAsync<ExpertScenarioResponse>("/api/scenarios/expert/" + Converted, Json);

        Assert.Equal(Converted, response!.Scenario.Id);
        Assert.Empty(response.Problems);
        Assert.Equal(new[] { ("context", "1d"), ("setup", "4h"), ("trigger", "1h") }, response.Levels.Select(level => (level.Level, level.Timeframe)));
        Assert.Equal(new[] { ("waves", "context"), ("wave2", "setup"), ("trade", "trigger") }, response.Groups.Select(group => (group.Key, group.Level!)));
        var waves = response.Groups[0];
        Assert.Equal(new[] { "line", "label", "label", "label" }, waves.Shapes.Select(shape => shape.Type));
        Assert.Equal(new[] { "0", "1", "2" }, waves.Shapes.Skip(1).Select(shape => shape.Text));
        var trade = response.Groups[2];
        Assert.Equal(3, trade.Shapes.Count(shape => shape.Type == "level" && shape.Role == "stop")); // the stop and its two moves
        Assert.Equal(new[] { "Exit 50 %", "Exit 40 %", "Exit 10 %" }, trade.Shapes.Where(shape => shape.Type == "marker" && shape.Text!.StartsWith("Exit", StringComparison.Ordinal)).Select(shape => shape.Text));
        Assert.Contains("EM4", trade.Shapes.Single(shape => shape.Text == "Entry").Detail); // the reason of the point
        Assert.False(response.Asset.InWorkspace); // XRPUSDT is not in this workspace (SC6)
        Assert.Equal(written, File.GetLastWriteTimeUtc(_folder.PathOf(Converted)));
        Assert.Equal(text, File.ReadAllText(_folder.PathOf(Converted)));
    }

    [Fact]
    public async Task An_unknown_and_a_broken_scenario_are_answered_with_404_and_422()
    {
        File.WriteAllText(_folder.PathOf("btcusdt-1d-2026-01-X1"), "{ broken");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/scenarios/expert/btcusdt-1d-2026-02-X1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/scenarios/expert/..%2Ftest-runs")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.GetAsync("/api/scenarios/expert/btcusdt-1d-2026-01-X1")).StatusCode);
    }

    [Fact]
    public async Task The_server_snaps_each_point_on_the_candles_of_its_level_and_writes_nothing()
    {
        var scenario = NewScenario();

        // Wave 2 end on the context (4h): let go at 09:30 -> the 4h candle of 08:00 (k = 2), its low.
        var snapped = await Snap(scenario, "waves.wave2End", Day + (9 * 3600) + 1800, 1.0);
        Assert.Equal(At(8 * 3600), snapped.Scenario.Waves!.Wave2End!.Time);
        Assert.Equal(92, snapped.Scenario.Waves.Wave2End.Price);
        Assert.Equal("4h", snapped.Scenario.Waves.Timeframe);

        // a of wave 2 on the setup (1h): 05:20 -> the 1h candle of 05:00 (k = 5), its low.
        snapped = await Snap(snapped.Scenario, "wave2.aEnd", Day + (5 * 3600) + 1200, 1.0);
        Assert.Equal((At(5 * 3600), 95.0, "1h"), (snapped.Scenario.Wave2!.AEnd!.Time, snapped.Scenario.Wave2.AEnd.Price, snapped.Scenario.Wave2.Timeframe!));

        // i of wave 1 on the setup: a high; the section is made with the pattern impulse.
        snapped = await Snap(snapped.Scenario, "wave1.iEnd", Day + (2 * 3600) + 59, 1.0);
        Assert.Equal((112.0, "impulse"), (snapped.Scenario.Wave1!.IEnd!.Price, snapped.Scenario.Wave1.Pattern!));

        // Entry on the trigger (15m): 10:07 -> the 15m candle of 10:00 (k = 40), its close.
        snapped = await Snap(snapped.Scenario with { Verdict = "trade-win" }, "trade.entry", Day + (10 * 3600) + 420, 1.0);
        Assert.Equal((At(10 * 3600), 145.0, "15m"), (snapped.Scenario.Trade!.Entry!.Time, snapped.Scenario.Trade.Entry.Price, snapped.Scenario.Trade.Timeframe!));

        // Exits keep the price where they were let go; a new one gets the share left to 100 %.
        snapped = await Snap(snapped.Scenario, "trade.exits", Day + (12 * 3600) + 100, 150.5, kind: "target");
        snapped = await Snap(snapped.Scenario with { Trade = snapped.Scenario.Trade!.WithFirstExitAt90() }, "trade.exits", Day + (13 * 3600), 120.25, kind: "stop");
        Assert.Equal(new[] { (90.0, "target", 150.5), (10.0, "stop", 120.25) }, snapped.Scenario.Trade!.Exits!.Select(exit => (exit.Share, exit.Kind, exit.Price)));

        // Moving an exit keeps its share and kind.
        snapped = await Snap(snapped.Scenario, "trade.exits", Day + (14 * 3600), 151, index: 0);
        Assert.Equal((At(14 * 3600), 151.0, 90.0), (snapped.Scenario.Trade!.Exits![0].Time, snapped.Scenario.Trade.Exits[0].Price, snapped.Scenario.Trade.Exits[0].Share));

        Assert.Equal(13, Directory.GetFiles(_folder.ExpertFolder).Length);
        Assert.Contains(snapped.Problems, problem => problem.Field == "trade.stop"); // not set yet: a form problem, nothing more
    }

    [Fact]
    public async Task A_point_without_a_candle_and_an_unknown_point_are_refused()
    {
        var noCandle = await _client.PostAsJsonAsync("/api/scenarios/expert/snap", new SnapScenarioPointRequest(NewScenario(), "waves.wave2End", At(3 * 86400), 1));
        var unknown = await _client.PostAsJsonAsync("/api/scenarios/expert/snap", new SnapScenarioPointRequest(NewScenario(), "waves.wave3End", At(3600), 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, noCandle.StatusCode);
        Assert.Contains("scenario.noCandle", await noCandle.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task A_new_scenario_is_saved_once_wave_2_end_is_placed_then_changed_and_deleted()
    {
        var notYet = await _client.PostAsJsonAsync("/api/scenarios/expert", NewScenario());
        Assert.Equal(HttpStatusCode.BadRequest, notYet.StatusCode);
        Assert.Contains("waves.wave2End", await notYet.Content.ReadAsStringAsync());

        var snapped = await Snap(NewScenario(), "waves.wave2End", Day + (9 * 3600), 1.0);
        var created = await (await _client.PostAsJsonAsync("/api/scenarios/expert", snapped.Scenario)).Content.ReadFromJsonAsync<ExpertScenarioResponse>(Json);
        Assert.Equal("btcusdt-4h-2024-12-X1", created!.Scenario.Id);
        Assert.NotNull(created.Scenario.CreatedAt);

        var changed = await _client.PutAsJsonAsync("/api/scenarios/expert/btcusdt-4h-2024-12-X1", created.Scenario with { Reason = "too deep" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("too deep", (string?)JsonNode.Parse(File.ReadAllText(_folder.PathOf("btcusdt-4h-2024-12-X1")))!["reason"]);

        var otherId = await _client.PutAsJsonAsync("/api/scenarios/expert/btcusdt-4h-2024-12-X2", created.Scenario);
        Assert.Equal(HttpStatusCode.BadRequest, otherId.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/scenarios/expert/btcusdt-4h-2024-12-X1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/scenarios/expert/btcusdt-4h-2024-12-X1")).StatusCode);
    }

    [Fact]
    public async Task A_save_through_the_api_keeps_the_legacy_section()
    {
        var before = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!["legacy"]!.ToJsonString();
        var response = await _client.GetFromJsonAsync<ExpertScenarioResponse>("/api/scenarios/expert/" + Converted, Json);

        var saved = await _client.PutAsJsonAsync("/api/scenarios/expert/" + Converted, response!.Scenario with { Draft = false });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var after = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!;
        Assert.Equal(before, after["legacy"]!.ToJsonString());
        Assert.False((bool)after["draft"]!);
    }

    [Fact]
    public async Task A_drawing_that_breaks_the_rules_of_its_tool_is_refused_a_form_problem_never()
    {
        var response = await _client.GetFromJsonAsync<ExpertScenarioResponse>("/api/scenarios/expert/" + Converted, Json);
        var oneAnchor = new ScenarioDrawingRecord(Guid.NewGuid(), "context", "trendLine", 1, false, new[] { new AnchorRecord(At(0), 1) }, new DrawingStyleRecord("#2962FF", 2, "solid"));

        var refused = await _client.PutAsJsonAsync("/api/scenarios/expert/" + Converted, response!.Scenario with { Drawings = new[] { oneAnchor with { Level = "nowhere" } } });
        var accepted = await _client.PutAsJsonAsync("/api/scenarios/expert/" + Converted, response.Scenario with { Verdict = null });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var body = await refused.Content.ReadAsStringAsync();
        Assert.Contains("drawings[0].level", body);
        Assert.Contains("drawings[0].anchors", body);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Contains((await accepted.Content.ReadFromJsonAsync<ExpertScenarioResponse>(Json))!.Problems, problem => problem.Field == "verdict");
    }

    [Fact]
    public async Task Preview_answers_shapes_and_problems_without_writing()
    {
        var preview = await _client.PostAsJsonAsync("/api/scenarios/expert/preview", NewScenario());

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var response = await preview.Content.ReadFromJsonAsync<ExpertScenarioResponse>(Json);
        Assert.Contains(response!.Problems, problem => problem.Field == "waves.wave2End");
        Assert.True(response.Asset.InWorkspace);
        Assert.Empty(response.Asset.HistoryNeeded);
        Assert.Equal(13, Directory.GetFiles(_folder.ExpertFolder).Length);
    }

    [Fact]
    public async Task A_history_that_starts_after_the_span_is_named_for_the_page_to_change()
    {
        await _client.PutAsJsonAsync("/api/assets/binance/BTCUSDT/history", new ChangeHistoryRequest(new Dictionary<string, string?> { ["15m"] = "2025-01-01" }));

        var response = await (await _client.PostAsJsonAsync("/api/scenarios/expert/preview", NewScenario())).Content.ReadFromJsonAsync<ExpertScenarioResponse>(Json);

        Assert.Equal(new Dictionary<string, string?> { ["15m"] = "2024-12-31" }, response!.Asset.HistoryNeeded);
    }

    private async Task<ExpertScenarioResponse> Snap(ExpertScenarioRecord scenario, string point, long time, double price, int? index = null, string? kind = null)
    {
        var response = await _client.PostAsJsonAsync("/api/scenarios/expert/snap", new SnapScenarioPointRequest(scenario, point, At(time - Day), price, index, kind));
        Assert.True(response.IsSuccessStatusCode, point + ": " + await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ExpertScenarioResponse>(Json))!;
    }

    private static ExpertScenarioRecord NewScenario()
    {
        return new ExpertScenarioRecord(
            null, "binance", "BTCUSDT", "4h", "long", "no-trade", true, null, null,
            At(0), At(86400 - 900), null, null, null, null, null, null, null);
    }

    private static DateTimeOffset At(long secondsAfterDay)
    {
        return DateTimeOffset.FromUnixTimeSeconds(Day + secondsAfterDay);
    }

    private void WriteDay(string timeframe, long seconds, int count)
    {
        var folder = Path.Combine(_factory.MarketDataRoot, "binance", "BTCUSDT", timeframe);
        Directory.CreateDirectory(folder);
        var lines = Enumerable.Range(0, count).Select(k =>
        {
            var open = (Day + (k * seconds)) * 1000;
            string Price(int value) => value.ToString(CultureInfo.InvariantCulture) + ".00000000";
            return string.Join(",", open, Price(100 + k), Price(110 + k), Price(90 + k), Price(105 + k), "1.00000000", open + (seconds * 1000) - 1, "100.0", "10", "0.5", "50.0", "0");
        });
        File.WriteAllLines(Path.Combine(folder, "BTCUSDT-" + timeframe + "-2024-12-31.csv"), lines);
    }
}

internal static class TradeRecordTestExtensions
{
    /// <summary>The first exit at 90 %, so the next one gets the 10 % that are left.</summary>
    public static ScenarioTradeRecord WithFirstExitAt90(this ScenarioTradeRecord trade)
    {
        var exits = trade.Exits!.ToList();
        exits[0] = exits[0] with { Share = 90 };
        return trade with { Exits = exits };
    }
}
