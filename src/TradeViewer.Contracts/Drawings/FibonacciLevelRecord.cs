namespace TradeViewer.Contracts.Drawings;

/// <summary>A level of a Fibonacci preset or drawing: the ratio, and whether it is shown (DR4).</summary>
public sealed record FibonacciLevelRecord(double Ratio, bool Visible = true);
