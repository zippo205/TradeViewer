namespace TradeViewer.Contracts.Scenarios;

/// <summary>An entry of the list of expert scenarios (SC7). Error is set, and the other fields may be empty, for a file that cannot be read.</summary>
public sealed record ExpertScenarioSummary(
    string Id,
    string? Source,
    string? Symbol,
    string? ContextTimeframe,
    string? Verdict,
    bool Draft,
    DateTimeOffset? From,
    DateTimeOffset? To,
    DateTimeOffset? UpdatedAt,
    string? Error = null);
