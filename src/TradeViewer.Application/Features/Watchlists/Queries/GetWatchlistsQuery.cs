using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Watchlists;

namespace TradeViewer.Application.Features.Watchlists.Queries;

/// <summary>The five watchlists with their rows and figures (AS1, AS6).</summary>
public sealed record GetWatchlistsQuery : IQuery<IReadOnlyList<WatchlistResponse>>;
