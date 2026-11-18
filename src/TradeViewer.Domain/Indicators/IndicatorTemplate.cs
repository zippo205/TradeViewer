namespace TradeViewer.Domain.Indicators;

/// <summary>A named set of indicators the user saved and can apply to a chart (IN2). It belongs to no asset.</summary>
public sealed record IndicatorTemplate(Guid Id, string Name, IReadOnlyList<IndicatorInstance> Indicators);
