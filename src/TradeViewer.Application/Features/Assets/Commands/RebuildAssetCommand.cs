using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Assets.Commands;

/// <summary>Deletes the candles of the asset and imports them again from the Downloader's files (MD5).</summary>
public sealed record RebuildAssetCommand(string Source, string Symbol) : ICommand<Unit>;
