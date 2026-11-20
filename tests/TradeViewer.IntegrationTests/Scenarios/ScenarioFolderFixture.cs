using TradeViewer.Infrastructure.Scenarios;
using TradeViewer.IntegrationTests.Workspace;

namespace TradeViewer.IntegrationTests.Scenarios;

/// <summary>
/// A temporary scenario folder: expert/ holds a copy of the 13 converted expert scenarios, test-runs/ one run of
/// EWTS that the store must never touch (SC19). The fixtures themselves are never written.
/// </summary>
public sealed class ScenarioFolderFixture : IDisposable
{
    public ScenarioFolderFixture()
    {
        Directory.CreateDirectory(ExpertFolder);
        foreach (var file in Directory.GetFiles(Fixtures.ExpertScenarios, "*.json"))
        {
            File.Copy(file, Path.Combine(ExpertFolder, Path.GetFileName(file)));
        }

        var run = Path.Combine(Root, "test-runs", "ewts-1-7");
        Directory.CreateDirectory(run);
        File.WriteAllText(Path.Combine(run, "ethusdt-4h-2026-08-T3.json"), "{ \"version\": 1, \"id\": \"ethusdt-4h-2026-08-T3\" }\n");
        File.WriteAllText(Path.Combine(run, "ethusdt-4h-2026-08-T3.pine"), "//@version=6\n");

        Store = new ExpertScenarioStore(Root, Clock);
    }

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "tradeviewer-tests", Guid.NewGuid().ToString("N"), "scenarios");

    public string ExpertFolder => Path.Combine(Root, "expert");

    public TestClock Clock { get; } = new TestClock();

    public ExpertScenarioStore Store { get; }

    public string PathOf(string id) => Path.Combine(ExpertFolder, id + ".json");

    /// <summary>Every file below test-runs/ with its content and time, to prove nothing there changed.</summary>
    public IReadOnlyList<string> TestRunsSnapshot()
    {
        return Directory.GetFiles(Path.Combine(Root, "test-runs"), "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => path + "|" + File.ReadAllText(path) + "|" + File.GetLastWriteTimeUtc(path).Ticks)
            .ToList();
    }

    public void Dispose()
    {
        Directory.Delete(Path.GetDirectoryName(Root)!, recursive: true);
    }
}
