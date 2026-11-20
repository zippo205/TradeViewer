using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Abstractions.Ewts;

/// <summary>What a read of an expert scenario found: the scenario, nothing (unknown id), or a file it cannot read and why.</summary>
public sealed record ExpertScenarioRead(ExpertScenarioRecord? Scenario, string? Error)
{
    public static readonly ExpertScenarioRead NotFound = new ExpertScenarioRead(null, null);

    public bool Found => Scenario != null;

    public bool Unreadable => Error != null;

    public static ExpertScenarioRead Of(ExpertScenarioRecord scenario) => new ExpertScenarioRead(scenario, null);

    public static ExpertScenarioRead Broken(string error) => new ExpertScenarioRead(null, error);
}
