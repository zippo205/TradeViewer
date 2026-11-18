using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.MarketData;

namespace TradeViewer.Application.Features.MarketData.Queries;

/// <summary>
/// A page of candles of an asset: the newest ones that open before To (Unix seconds, exclusive; the latest
/// candles without it), at most Limit (2,000 without it, never more than 5,000; see ICandleSeriesReader).
/// </summary>
public sealed record GetCandlesQuery(string Source, string Symbol, string Timeframe, long? To, int? Limit) : IQuery<CandlesResponse>;
