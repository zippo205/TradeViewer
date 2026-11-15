using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Queries;

/// <summary>One asset with its loading progress and its timeframes (AS4).</summary>
public sealed record GetAssetQuery(string Source, string Symbol) : IQuery<AssetResponse>;
