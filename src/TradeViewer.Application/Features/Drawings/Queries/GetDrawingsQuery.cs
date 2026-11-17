using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Application.Features.Drawings.Queries;

/// <summary>The drawings of an asset in the order they were made (DR3).</summary>
public sealed record GetDrawingsQuery(string Source, string Symbol) : IQuery<IReadOnlyList<DrawingRecord>>;
