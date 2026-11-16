using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Watchlists;

namespace TradeViewer.Application.Features.Watchlists.Commands;

/// <summary>Replaces the order of one watchlist as a whole (AS1). AssetClass is the code of the list, e.g. crypto.</summary>
public sealed record ReorderWatchlistCommand(string AssetClass, IReadOnlyList<WatchlistEntry>? Assets) : ICommand<Unit>;
