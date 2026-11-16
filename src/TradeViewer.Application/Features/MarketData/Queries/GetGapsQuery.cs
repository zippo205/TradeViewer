using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.MarketData;

namespace TradeViewer.Application.Features.MarketData.Queries;

/// <summary>The gaps of one timeframe of an asset that the source confirmed as missing, oldest first (MD4).</summary>
public sealed record GetGapsQuery(string Source, string Symbol, string Timeframe) : IQuery<IReadOnlyList<GapResponse>>;
