namespace TradeViewer.Contracts.Scenarios;

/// <summary>
/// A form problem of a scenario at its field of the file, e.g. trade.exits[1].share (SC15). Blocking: the levels do
/// not fit (decision 0013), and Save stays disabled until the user corrects it; any other problem never prevents a save.
/// </summary>
public sealed record ScenarioProblemRecord(string Field, string Message, bool Blocking = false);
