using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Drawings.Commands;

/// <summary>Deletes a Fibonacci preset; the last preset of a kind stays, so each Fibonacci tool always has levels.</summary>
public sealed record DeleteFibonacciPresetCommand(Guid Id) : ICommand<Unit>;
