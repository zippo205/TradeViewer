using TradeViewer.Application.Features.Scenarios.Services;
using TradeViewer.Domain.Scenarios;
using TradeViewer.Infrastructure.Scenarios;
using Xunit.Abstractions;

namespace TradeViewer.UnitTests.Application.Scenarios;

/// <summary>SC15 on the 13 expert scenarios EWTS converted (tests/fixtures/scenarios/expert): the form checks against real readings.</summary>
public class ConvertedScenarioFormTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "fixtures", "scenarios", "expert");

    private readonly ITestOutputHelper _output;

    public ConvertedScenarioFormTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> Ids()
    {
        return Directory.GetFiles(Folder, "*.json").Select(path => new object[] { Path.GetFileNameWithoutExtension(path) }).OrderBy(id => (string)id[0]);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void A_converted_scenario_has_no_form_problem(string id)
    {
        var file = ExpertScenarioJson.Parse(File.ReadAllText(Path.Combine(Folder, id + ".json")), out var error);
        Assert.Null(error);
        var record = ExpertScenarioJson.ToRecord(file!, id, out error);
        Assert.Null(error);

        var problems = ScenarioFormRules.Check(ExpertScenarioReadings.Of(record!));

        foreach (var problem in problems)
        {
            _output.WriteLine(problem.Field + ": " + problem.Message);
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void The_13_files_are_there()
    {
        Assert.Equal(13, Ids().Count());
    }
}
