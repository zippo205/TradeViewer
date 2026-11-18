namespace TradeViewer.Contracts.Indicators;

/// <summary>
/// A named set of indicators (IN2); also the body of PUT /api/indicator-templates/{id}. Applying it to a chart
/// is done by the client: it copies the indicators with new ids into the chart settings.
/// </summary>
public sealed record IndicatorTemplateRecord(Guid Id, string Name, IReadOnlyList<IndicatorRecord> Indicators);
