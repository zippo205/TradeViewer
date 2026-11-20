namespace TradeViewer.Contracts.Scenarios;

/// <summary>A form problem of a scenario at its field of the file, e.g. trade.exits[1].share (SC15). It never prevents a save.</summary>
public sealed record ScenarioProblemRecord(string Field, string Message);
