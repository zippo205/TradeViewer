using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Queries;

/// <summary>All assets with their state, ordered by asset class and position.</summary>
public sealed record GetAssetsQuery : IQuery<IReadOnlyList<AssetResponse>>;
