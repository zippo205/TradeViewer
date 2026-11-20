namespace TradeViewer.Contracts.Scenarios;

/// <summary>Whether the scenario folder is configured (the menu entry is shown) and whether the editor can write to it.</summary>
public sealed record ScenariosStatusResponse(bool Enabled, bool Writable);
