namespace TradeViewer.Domain.Indicators;

/// <summary>
/// One indicator on the charts of an asset (IN1): its parameters, the style of each of its lines, and
/// whether it is hidden. The id is a UUID the client makes when the user adds the indicator.
/// </summary>
public sealed record IndicatorInstance(Guid Id, IndicatorParameters Parameters, IReadOnlyList<IndicatorLineStyle> Styles, bool Hidden)
{
    public IndicatorType Type => Parameters.Type;

    /// <summary>A new indicator of the type with the defaults of IN1.</summary>
    public static IndicatorInstance New(Guid id, IndicatorType type) =>
        new(id, IndicatorParameters.Default(type), IndicatorStyles.Default(type), false);
}
