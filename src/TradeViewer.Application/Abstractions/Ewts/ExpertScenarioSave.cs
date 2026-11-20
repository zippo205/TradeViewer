using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Abstractions.Ewts;

/// <summary>What became of a save: the scenario as written (with its id and times), or why nothing was written.</summary>
public sealed record ExpertScenarioSave(ExpertScenarioRecord? Saved, ExpertScenarioSaveProblem? Problem)
{
    public static ExpertScenarioSave Of(ExpertScenarioRecord saved) => new ExpertScenarioSave(saved, null);

    public static ExpertScenarioSave Refused(ExpertScenarioSaveProblem problem) => new ExpertScenarioSave(null, problem);
}
