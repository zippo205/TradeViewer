namespace TradeViewer.Domain.Scenarios;

/// <summary>A problem of the form of a scenario, at the field of the file it belongs to (e.g. trade.exits[1].share), shown there by the editor.</summary>
public sealed record ScenarioFormProblem(string Field, string Message);
