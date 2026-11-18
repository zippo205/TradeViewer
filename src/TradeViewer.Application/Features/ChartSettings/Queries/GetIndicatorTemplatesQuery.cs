using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Queries;

/// <summary>All indicator templates, ordered by name (IN2).</summary>
public sealed record GetIndicatorTemplatesQuery : IQuery<IReadOnlyList<IndicatorTemplateRecord>>;
