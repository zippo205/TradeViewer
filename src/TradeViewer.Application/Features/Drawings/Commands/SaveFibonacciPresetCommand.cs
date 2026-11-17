using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Application.Features.Drawings.Commands;

/// <summary>Stores a Fibonacci preset under its id, new or edited (DR4). Drawings already made keep their levels.</summary>
public sealed record SaveFibonacciPresetCommand(Guid Id, FibonacciPresetRecord? Preset) : ICommand<FibonacciPresetRecord>;
