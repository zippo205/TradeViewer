namespace TradeViewer.Domain.Drawings;

/// <summary>
/// A set of Fibonacci levels the user can edit and add to (DR4). A new Fibonacci drawing copies the levels of
/// the preset chosen; editing the preset later never changes drawings already made.
/// </summary>
public sealed record FibonacciPreset(Guid Id, string Name, FibonacciKind Kind, IReadOnlyList<double> Levels);
