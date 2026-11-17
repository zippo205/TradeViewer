using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Application.Features.Assets.Queries;

/// <summary>A symbol picked, looked up at the Downloader by its ticker before it is added (AS2).</summary>
public sealed record GetSymbolDetailsQuery(string Source, string Ticker) : IQuery<SymbolDetailsResponse>;
