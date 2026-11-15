using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Commands;

/// <summary>Adds a symbol of a source as an asset: the Downloader confirms and subscribes it, then its files are imported (AS2, AS3).</summary>
public sealed record AddAssetCommand(string Source, string Symbol) : ICommand<AssetResponse>;
