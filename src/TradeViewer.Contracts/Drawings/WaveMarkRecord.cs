namespace TradeViewer.Contracts.Drawings;

/// <summary>A wave label: degree (grandSupercycle ... subminuette), label (1-5, A-E, W, X, Y) and side (above, below).</summary>
public sealed record WaveMarkRecord(string Degree, string Label, string Side);
