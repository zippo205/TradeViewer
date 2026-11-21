using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.UnitTests.Domain.Scenarios;

/// <summary>Decision 0013: EWTS's rule for a turning point on a smaller timeframe.</summary>
public class ScenarioTurningTests
{
    private const long Day = 1_735_603_200; // 2024-12-31 00:00 UTC

    /// <summary>Six 4h candles of one day: lows 95, 93, 90, 92, 90, 94 and highs 100 + k.</summary>
    private static readonly IReadOnlyList<Candle> FourHours = new[] { 95.0, 93, 90, 92, 90, 94 }
        .Select((low, k) => new Candle(Day + (k * 14400), 96, 100 + k, low, 97, 1))
        .ToList();

    [Fact]
    public void The_candle_with_the_same_low_inside_the_context_candle_is_found_the_earliest_on_ties()
    {
        var found = ScenarioTurning.Find(FourHours, Day, Day + 86400, 90, ScenarioSnapKind.Low);

        Assert.Equal(Day + (2 * 14400), found!.Value.Time);
    }

    [Fact]
    public void A_high_is_matched_by_the_high()
    {
        Assert.Equal(Day + (5 * 14400), ScenarioTurning.Find(FourHours, Day, Day + 86400, 105, ScenarioSnapKind.High)!.Value.Time);
    }

    [Fact]
    public void No_candle_with_that_price_or_none_inside_the_span_finds_nothing()
    {
        Assert.Null(ScenarioTurning.Find(FourHours, Day, Day + 86400, 89, ScenarioSnapKind.Low));
        Assert.Null(ScenarioTurning.Find(FourHours, Day + 86400, Day + (2 * 86400), 90, ScenarioSnapKind.Low));
    }

    [Fact]
    public void For_the_view_a_point_that_is_no_extreme_sits_on_the_earliest_extreme_of_the_span()
    {
        Assert.Equal(Day + (2 * 14400), ScenarioTurning.ForView(FourHours, Day, Day + 86400, 89.5, ScenarioSnapKind.Low)!.Value.Time);
        Assert.Equal(Day + (5 * 14400), ScenarioTurning.ForView(FourHours, Day, Day + 86400, 200, ScenarioSnapKind.High)!.Value.Time);
        Assert.Null(ScenarioTurning.ForView(FourHours, Day + 86400, Day + (2 * 86400), 90, ScenarioSnapKind.Low));
    }

    [Fact]
    public void The_nested_starts_and_ends_are_paired_with_their_context_points()
    {
        Assert.Contains((ScenarioPointName.Wave1Origin, ScenarioPointName.Wave1Start), ScenarioPoints.Turnings);
        Assert.Contains((ScenarioPointName.Wave2Origin, ScenarioPointName.Wave1End), ScenarioPoints.Turnings);
        Assert.Equal("wave1.start", ScenarioPoints.FieldOf(ScenarioPointName.Wave1Origin));
        Assert.Equal(ScenarioSnapKind.High, ScenarioPoints.SnapOf(ScenarioPointName.Wave2Origin));
        Assert.Equal(ScenarioLevel.Setup, ScenarioPoints.LevelOf(ScenarioPointName.Wave2Origin));
    }
}
