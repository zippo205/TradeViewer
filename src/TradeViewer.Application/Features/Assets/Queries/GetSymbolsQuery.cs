using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Queries;

/// <summary>The symbols of a source that can be added, found by typing (AS2).</summary>
public sealed record GetSymbolsQuery(string Source, string? Text) : IQuery<IReadOnlyList<SymbolResponse>>;
