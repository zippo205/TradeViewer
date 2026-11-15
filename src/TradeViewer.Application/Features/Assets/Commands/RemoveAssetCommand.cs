using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Assets.Commands;

/// <summary>Removes an asset from TradeViewer: its subscription ends and its candles and what the user created for it are deleted (AS5).</summary>
public sealed record RemoveAssetCommand(string Source, string Symbol) : ICommand<Unit>;
