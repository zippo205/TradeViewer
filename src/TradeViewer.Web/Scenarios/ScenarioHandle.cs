namespace TradeViewer.Web.Scenarios;

/// <summary>
/// A named point as the chart module takes it (setScenarioPoints): its field and position in a list, its time in Unix
/// seconds and its price; Editable for the points of the level shown, which get a handle (SC10, decision 0012).
/// </summary>
public sealed record ScenarioHandle(string Field, int? Index, long Time, double Price, bool Editable, string? Colour = null);
