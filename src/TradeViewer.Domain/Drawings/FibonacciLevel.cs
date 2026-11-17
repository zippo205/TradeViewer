namespace TradeViewer.Domain.Drawings;

/// <summary>A level of a Fibonacci preset or drawing: its ratio, and whether it is shown (DR4, as in TradingView).</summary>
public sealed record FibonacciLevel(double Ratio, bool Visible = true);
