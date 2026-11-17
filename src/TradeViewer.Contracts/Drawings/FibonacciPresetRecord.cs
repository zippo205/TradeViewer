namespace TradeViewer.Contracts.Drawings;

/// <summary>A Fibonacci preset (GET, PUT /api/fibonacci-presets): kind retracement or extension, levels as ratios, each on or off.</summary>
public sealed record FibonacciPresetRecord(Guid Id, string Name, string Kind, IReadOnlyList<FibonacciLevelRecord> Levels);
