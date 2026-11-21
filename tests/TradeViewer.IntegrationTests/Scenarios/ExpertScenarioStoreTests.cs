using System.Text.Json.Nodes;
using TradeViewer.Application.Abstractions.Ewts;
using TradeViewer.Contracts.Drawings;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Infrastructure.Scenarios;

namespace TradeViewer.IntegrationTests.Scenarios;

/// <summary>
/// SC7, SC13, SC14, SC16, SC19, SC20 and decisions 0010 and 0012: the expert scenarios as files. The 13 converted
/// files are read; a save keeps every field TradeViewer does not know (legacy above all) and writes atomically; the
/// first save takes the next free id; nothing outside expert/ is touched.
/// </summary>
public class ExpertScenarioStoreTests : IDisposable
{
    private const string Converted = "xrpusdt-1d-2024-11-X1"; // split exits 50/40/10, add, two stop moves

    private readonly ScenarioFolderFixture _folder = new ScenarioFolderFixture();
    private readonly IReadOnlyList<string> _testRunsBefore;

    public ExpertScenarioStoreTests()
    {
        _testRunsBefore = _folder.TestRunsSnapshot();
    }

    public void Dispose()
    {
        Assert.Equal(_testRunsBefore, _folder.TestRunsSnapshot()); // every test: test-runs/ is never touched (SC19)
        _folder.Dispose();
    }

    [Fact]
    public async Task The_13_converted_scenarios_are_listed_in_order_of_their_id()
    {
        var list = await _folder.Store.ListAsync(CancellationToken.None);

        Assert.Equal(13, list.Count);
        Assert.All(list, head => Assert.Null(head.Error));
        Assert.All(list, head => Assert.True(head.Draft));
        Assert.Equal(list.Select(head => head.Id).OrderBy(id => id, StringComparer.Ordinal), list.Select(head => head.Id));
        var xrp = list.Single(head => head.Id == Converted);
        Assert.Equal(("binance", "XRPUSDT", "1d", "trade-win"), (xrp.Source, xrp.Symbol, xrp.ContextTimeframe, xrp.Verdict));
        Assert.Equal(new DateTimeOffset(2024, 9, 19, 0, 0, 0, TimeSpan.Zero), xrp.From);
    }

    [Fact]
    public async Task Every_converted_scenario_is_read_with_its_points()
    {
        var read = await _folder.Store.ReadAsync(Converted, CancellationToken.None);

        var scenario = Assert.IsType<ExpertScenarioRecord>(read.Scenario);
        Assert.Equal(Converted, scenario.Id);
        Assert.Equal(("1d", 0.486), (scenario.Waves!.Timeframe, scenario.Waves.Wave1Start!.Price));
        Assert.Equal(new DateTimeOffset(2024, 11, 3, 0, 0, 0, TimeSpan.Zero), scenario.Waves.Wave2End!.Time);
        Assert.Equal(("4h", "zigzag"), (scenario.Wave2!.Timeframe, scenario.Wave2.Pattern));
        Assert.Equal("1h", scenario.Trade!.Timeframe);
        Assert.Equal(0.4908, scenario.Trade.Stop!.Price);
        Assert.Equal(2, scenario.Trade.StopMoves!.Count);
        Assert.Equal(new[] { (50.0, "target"), (40.0, "target"), (10.0, "stop") }, scenario.Trade.Exits!.Select(exit => (exit.Share, exit.Kind)));
        Assert.Null(scenario.Drawings);

        foreach (var head in await _folder.Store.ListAsync(CancellationToken.None))
        {
            Assert.True((await _folder.Store.ReadAsync(head.Id, CancellationToken.None)).Found, head.Id);
        }
    }

    [Fact]
    public async Task Saving_a_converted_scenario_unchanged_keeps_the_file_equal_apart_from_its_times()
    {
        foreach (var head in await _folder.Store.ListAsync(CancellationToken.None))
        {
            var before = JsonNode.Parse(File.ReadAllText(_folder.PathOf(head.Id)))!.AsObject();
            var scenario = (await _folder.Store.ReadAsync(head.Id, CancellationToken.None)).Scenario!;

            var save = await _folder.Store.SaveAsync(scenario, CancellationToken.None);

            Assert.Null(save.Problem);
            var after = JsonNode.Parse(File.ReadAllText(_folder.PathOf(head.Id)))!.AsObject();
            Assert.Equal("2026-11-15T12:00:00Z", (string?)after["createdAt"]); // the converted files had none (proposal 5)
            Assert.Equal("2026-11-15T12:00:00Z", (string?)after["updatedAt"]);
            after.Remove("createdAt");
            after.Remove("updatedAt");
            Assert.True(JsonNode.DeepEquals(before, after), head.Id + ":\n" + before + "\n---\n" + after);
            Assert.Equal(before["legacy"]!.ToJsonString(), after["legacy"]!.ToJsonString()); // legacy as it was, in its order
        }
    }

    [Fact]
    public async Task Saving_a_converted_scenario_unchanged_changes_no_line_of_its_text()
    {
        foreach (var head in await _folder.Store.ListAsync(CancellationToken.None))
        {
            var before = File.ReadAllText(_folder.PathOf(head.Id));

            await _folder.Store.SaveAsync((await _folder.Store.ReadAsync(head.Id, CancellationToken.None)).Scenario!, CancellationToken.None);

            var after = System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(_folder.PathOf(head.Id)),
                ",\n  \"createdAt\": \"[^\"]+\",\n  \"updatedAt\": \"[^\"]+\"",
                string.Empty);
            Assert.Equal(before, after); // only the two times are added at the end; 62275.0 stays 62275.0
        }
    }

    [Fact]
    public async Task A_changed_point_changes_only_that_field()
    {
        var scenario = (await _folder.Store.ReadAsync(Converted, CancellationToken.None)).Scenario!;
        var moved = scenario.Waves! with { Wave2End = new ScenarioPointRecord(new DateTimeOffset(2024, 11, 4, 0, 0, 0, TimeSpan.Zero), 0.48, "moved") };
        var before = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!.AsObject();

        await _folder.Store.SaveAsync(scenario with { Waves = moved, Verdict = "trade-loss" }, CancellationToken.None);

        var after = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!.AsObject();
        Assert.Equal("2024-11-04T00:00:00Z", (string?)after["waves"]!["wave2End"]!["time"]);
        Assert.Equal("moved", (string?)after["waves"]!["wave2End"]!["why"]);
        Assert.Equal("trade-loss", (string?)after["verdict"]);
        before["waves"]!["wave2End"] = after["waves"]!["wave2End"]!.DeepClone();
        before["verdict"] = "trade-loss";
        foreach (var name in new[] { "createdAt", "updatedAt" })
        {
            after.Remove(name);
        }

        Assert.True(JsonNode.DeepEquals(before, after));
        Assert.Equal(Converted, (await _folder.Store.ReadAsync(Converted, CancellationToken.None)).Scenario!.Id); // the id stays (SC14)
    }

    [Fact]
    public async Task Unknown_fields_are_kept_at_every_depth_and_by_position_in_the_exits()
    {
        var file = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!.AsObject();
        file["waves"]!["checkedBy"] = "EWTS";
        file["trade"]!["exits"]![1]!["fill"] = "partial";
        file["later"] = new JsonObject { ["a"] = 1 };
        File.WriteAllText(_folder.PathOf(Converted), file.ToJsonString());
        var scenario = (await _folder.Store.ReadAsync(Converted, CancellationToken.None)).Scenario!;
        var exits = scenario.Trade!.Exits!.ToList();
        exits[1] = exits[1] with { Share = 45 };
        exits.RemoveAt(2);

        await _folder.Store.SaveAsync(scenario with { Trade = scenario.Trade with { Exits = exits } }, CancellationToken.None);

        var after = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!;
        Assert.Equal("EWTS", (string?)after["waves"]!["checkedBy"]);
        Assert.Equal("partial", (string?)after["trade"]!["exits"]![1]!["fill"]);
        Assert.Equal(45, (double)after["trade"]!["exits"]![1]!["share"]!);
        Assert.Equal(2, after["trade"]!["exits"]!.AsArray().Count);
        Assert.Equal(1, (int)after["later"]!["a"]!);
    }

    [Fact]
    public async Task A_known_field_set_to_nothing_is_removed_from_the_file()
    {
        var scenario = (await _folder.Store.ReadAsync(Converted, CancellationToken.None)).Scenario!;

        await _folder.Store.SaveAsync(scenario with { Wave2 = scenario.Wave2! with { BEnd = null }, Notes = null }, CancellationToken.None);

        var after = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!.AsObject();
        Assert.False(after["wave2"]!.AsObject().ContainsKey("bEnd"));
        Assert.True(after["wave2"]!.AsObject().ContainsKey("aEnd"));
        Assert.False(after.ContainsKey("notes"));
        Assert.True(after.ContainsKey("legacy"));
    }

    [Fact]
    public async Task A_point_or_section_new_to_the_file_is_written_without_its_empty_fields()
    {
        var scenario = (await _folder.Store.ReadAsync(Converted, CancellationToken.None)).Scenario!;
        await _folder.Store.SaveAsync(scenario with { Waves = scenario.Waves! with { Wave2End = null } }, CancellationToken.None);
        var exits = new[] { new ScenarioExitRecord(new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero), 2.0, 100, "target") };

        // Undo puts the point back; a new section and new exits come without why.
        await _folder.Store.SaveAsync(
            scenario with { Wave1 = new ScenarioWave1Record("4h", "three"), Trade = scenario.Trade! with { Exits = exits } },
            CancellationToken.None);

        var after = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!.AsObject();
        Assert.Equal(scenario.Waves!.Wave2End!.Why != null, after["waves"]!["wave2End"]!.AsObject().ContainsKey("why"));
        Assert.Equal(new[] { "timeframe", "pattern" }, after["wave1"]!.AsObject().Select(pair => pair.Key));
        Assert.DoesNotContain(after.ToJsonString(), "null", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_first_save_takes_the_next_free_number_of_the_month_of_wave_2_end()
    {
        // solusdt-1d-2025-06-X1 and -X2 exist among the converted files.
        var first = await _folder.Store.SaveAsync(NewScenario("SOLUSDT", new DateTimeOffset(2025, 6, 22, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None);
        var second = await _folder.Store.SaveAsync(NewScenario("SOLUSDT", new DateTimeOffset(2025, 6, 30, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None);
        var otherMonth = await _folder.Store.SaveAsync(NewScenario("SOLUSDT", new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None);

        Assert.Equal("solusdt-1d-2025-06-X3", first.Saved!.Id);
        Assert.Equal("solusdt-1d-2025-06-X4", second.Saved!.Id);
        Assert.Equal("solusdt-1d-2025-07-X1", otherMonth.Saved!.Id);
        var written = JsonNode.Parse(File.ReadAllText(_folder.PathOf("solusdt-1d-2025-06-X3")))!.AsObject();
        Assert.Equal(new[] { "format", "version", "id" }, written.Select(field => field.Key).Take(3));
        Assert.Equal(("ewts-expert-scenario", 1), ((string?)written["format"], (int)written["version"]!));
        Assert.Equal("2026-11-15T12:00:00Z", (string?)written["createdAt"]);
        Assert.False(written.ContainsKey("trade"));
        Assert.False(written.ContainsKey("drawings"));
        Assert.Empty(Directory.GetFiles(_folder.ExpertFolder, "*.part"));
    }

    [Fact]
    public async Task Concurrent_first_saves_never_take_the_same_id()
    {
        var wave2End = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

        var saves = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _folder.Store.SaveAsync(NewScenario("ADAUSDT", wave2End), CancellationToken.None)));

        Assert.Equal(Enumerable.Range(1, 8).Select(n => "adausdt-1d-2026-03-X" + n), saves.Select(save => save.Saved!.Id!).OrderBy(id => id.Length).ThenBy(id => id));
    }

    [Fact]
    public async Task Without_the_end_of_wave_2_a_new_scenario_is_not_saved()
    {
        var save = await _folder.Store.SaveAsync(NewScenario("ADAUSDT", null), CancellationToken.None);

        Assert.Equal(ExpertScenarioSaveProblem.NoWave2End, save.Problem);
        Assert.Equal(13, Directory.GetFiles(_folder.ExpertFolder).Length);
    }

    [Fact]
    public async Task Later_saves_keep_createdAt_and_move_updatedAt()
    {
        var created = (await _folder.Store.SaveAsync(NewScenario("ADAUSDT", new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None)).Saved!;
        _folder.Clock.Now = _folder.Clock.Now.AddHours(2).AddMilliseconds(345);

        var saved = (await _folder.Store.SaveAsync(created with { CreatedAt = null, Reason = "later" }, CancellationToken.None)).Saved!;

        var read = (await _folder.Store.ReadAsync(created.Id!, CancellationToken.None)).Scenario!;
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero), read.CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 14, 0, 0, TimeSpan.Zero), read.UpdatedAt); // whole seconds
        Assert.Equal(read, saved);
    }

    [Fact]
    public async Task The_drawings_of_the_levels_are_written_and_read_back_and_replaced_by_the_next_save()
    {
        var scenario = (await _folder.Store.ReadAsync(Converted, CancellationToken.None)).Scenario!;
        var line = new ScenarioDrawingRecord(
            Guid.Parse("4b1f0c52-7a0e-4b8e-9a51-0d8e6f3c2a11"), "context", "trendLine", 1, false,
            new[] { new AnchorRecord(new DateTimeOffset(2024, 10, 25, 0, 0, 0, TimeSpan.Zero), 0.486), new AnchorRecord(new DateTimeOffset(2024, 10, 29, 0, 0, 0, TimeSpan.Zero), 0.5305) },
            new DrawingStyleRecord("#2962FF", 2, "solid"));
        var label = line with { Id = Guid.Parse("9d2e4a77-1c3b-4f60-8e21-5a7b9c0d1e22"), Level = "setup", Tool = "waveLabel", Anchors = new[] { new AnchorRecord(new DateTimeOffset(2024, 10, 26, 0, 0, 0, TimeSpan.Zero), null) }, Wave = new WaveMarkRecord("minor", "1", "above") };

        await _folder.Store.SaveAsync(scenario with { Drawings = new[] { line, label } }, CancellationToken.None);
        var read = (await _folder.Store.ReadAsync(Converted, CancellationToken.None)).Scenario!;
        await _folder.Store.SaveAsync(read with { Drawings = new[] { label } }, CancellationToken.None);
        var again = (await _folder.Store.ReadAsync(Converted, CancellationToken.None)).Scenario!;

        Assert.Equal(new[] { "context", "setup" }, read.Drawings!.Select(drawing => drawing.Level));
        Assert.Equal(new[] { 0.486, 0.5305 }, read.Drawings![0].Anchors.Select(anchor => anchor.Price!.Value));
        Assert.Equal("minor", read.Drawings[1].Wave!.Degree);
        Assert.Equal(label.Id, Assert.Single(again.Drawings!).Id);
        var file = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!;
        Assert.Null(file["drawings"]![0]!["text"]); // written without empty parts
        Assert.NotNull(file["legacy"]);
    }

    [Fact]
    public async Task A_file_that_cannot_be_read_is_listed_with_its_error_and_never_overwritten()
    {
        File.WriteAllText(_folder.PathOf("btcusdt-1d-2026-01-X1"), "{ not json");
        File.WriteAllText(_folder.PathOf("btcusdt-1d-2026-02-X1"), "{ \"format\": \"ewts-expert-scenario\", \"version\": 2 }");
        File.WriteAllText(_folder.PathOf("btcusdt-1d-2026-03-X1"), "{ \"format\": \"something-else\", \"version\": 1 }");
        File.WriteAllText(Path.Combine(_folder.ExpertFolder, "My Notes.json"), "{}");
        File.WriteAllText(Path.Combine(_folder.ExpertFolder, "readme.txt"), "not a scenario");

        var list = await _folder.Store.ListAsync(CancellationToken.None);
        var save = await _folder.Store.SaveAsync(NewScenario("BTCUSDT", null) with { Id = "btcusdt-1d-2026-01-X1" }, CancellationToken.None);

        Assert.Equal(17, list.Count); // the .txt is not listed
        Assert.StartsWith("Not valid JSON", list.Single(head => head.Id == "btcusdt-1d-2026-01-X1").Error);
        Assert.StartsWith("Unknown version 2", list.Single(head => head.Id == "btcusdt-1d-2026-02-X1").Error);
        Assert.StartsWith("Unknown format", list.Single(head => head.Id == "btcusdt-1d-2026-03-X1").Error);
        Assert.StartsWith("The file name is not an id", list.Single(head => head.Id == "My Notes").Error);
        Assert.True((await _folder.Store.ReadAsync("btcusdt-1d-2026-01-X1", CancellationToken.None)).Unreadable);
        Assert.Equal(ExpertScenarioSaveProblem.UnreadableFile, save.Problem);
        Assert.Equal("{ not json", File.ReadAllText(_folder.PathOf("btcusdt-1d-2026-01-X1")));
    }

    [Fact]
    public async Task A_file_changed_by_hand_is_seen_by_the_next_list()
    {
        await _folder.Store.ListAsync(CancellationToken.None);
        var file = JsonNode.Parse(File.ReadAllText(_folder.PathOf(Converted)))!.AsObject();
        file["verdict"] = "no-trade";
        File.WriteAllText(_folder.PathOf(Converted), file.ToJsonString());
        File.Delete(_folder.PathOf("btcusdt-4h-2026-08-X1"));

        var list = await _folder.Store.ListAsync(CancellationToken.None);

        Assert.Equal("no-trade", list.Single(head => head.Id == Converted).Verdict);
        Assert.DoesNotContain(list, head => head.Id == "btcusdt-4h-2026-08-X1");
    }

    [Theory]
    [InlineData("../test-runs/ewts-1-7/ethusdt-4h-2026-08-T3")]
    [InlineData("ethusdt-4h-2026-08-T3")]
    [InlineData("XRPUSDT-1d-2024-11-X1")]
    [InlineData("xrpusdt-1d-2024-11-X1.json")]
    [InlineData("xrpusdt-15m-2024-11-X1")]
    [InlineData("")]
    public async Task Anything_that_is_not_an_id_is_never_read_written_or_deleted(string id)
    {
        Assert.False((await _folder.Store.ReadAsync(id, CancellationToken.None)).Found);
        Assert.False(await _folder.Store.DeleteAsync(id, CancellationToken.None));
        Assert.Equal(ExpertScenarioSaveProblem.InvalidId, (await _folder.Store.SaveAsync(NewScenario("XRPUSDT", null) with { Id = id }, CancellationToken.None)).Problem);
        Assert.Equal(13, Directory.GetFiles(_folder.ExpertFolder).Length);
    }

    [Fact]
    public async Task Delete_removes_the_file_once()
    {
        Assert.True(await _folder.Store.DeleteAsync(Converted, CancellationToken.None));
        Assert.False(await _folder.Store.DeleteAsync(Converted, CancellationToken.None));
        Assert.False(File.Exists(_folder.PathOf(Converted)));
        Assert.Equal(12, (await _folder.Store.ListAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Without_an_expert_folder_the_list_is_empty_and_the_first_save_creates_it()
    {
        Directory.Delete(_folder.ExpertFolder, recursive: true);

        Assert.Empty(await _folder.Store.ListAsync(CancellationToken.None));
        Assert.Equal("btcusdt-1d-2026-03-X1", (await _folder.Store.SaveAsync(NewScenario("BTCUSDT", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)), CancellationToken.None)).Saved!.Id);
    }

    [Fact]
    public async Task Without_a_scenario_folder_there_are_no_scenarios()
    {
        var store = new ExpertScenarioStore((string?)null, _folder.Clock);

        Assert.Empty(await store.ListAsync(CancellationToken.None));
        Assert.False((await store.ReadAsync(Converted, CancellationToken.None)).Found);
        Assert.False(await store.DeleteAsync(Converted, CancellationToken.None));
    }

    private static ExpertScenarioRecord NewScenario(string symbol, DateTimeOffset? wave2End)
    {
        var waves = wave2End == null ? null : new ScenarioWavesRecord("1d", null, null, new ScenarioPointRecord(wave2End.Value, 1.0));
        return new ExpertScenarioRecord(
            null, "binance", symbol, "1d", "long", "no-trade", true, null, null,
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), null, null, null, waves, null, null, null, null);
    }
}
