using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Application.Features.Drawings.Queries;

/// <summary>All Fibonacci presets in the order they were added (DR4).</summary>
public sealed record GetFibonacciPresetsQuery : IQuery<IReadOnlyList<FibonacciPresetRecord>>;
