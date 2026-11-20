namespace TradeViewer.Web.Scenarios;

/// <summary>A point tool as the chart module takes it: the fields it places one after the other, and the kind of an exit.</summary>
public sealed record ScenarioTool(string Code, IReadOnlyList<string> Fields, string? Kind = null);
