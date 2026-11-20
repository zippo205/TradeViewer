using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.UnitTests.Domain.Scenarios;

/// <summary>SC11, SC21, decision 0012: the three levels from the chain of the context timeframe, and what each level shows.</summary>
public class ScenarioLevelsTests
{
    [Theory]
    [InlineData(Timeframe.D1, Timeframe.D1, Timeframe.H4, Timeframe.H1)]
    [InlineData(Timeframe.H4, Timeframe.H4, Timeframe.H1, Timeframe.M15)]
    public void The_levels_follow_EWTS_chain(Timeframe context, Timeframe contextLevel, Timeframe setup, Timeframe trigger)
    {
        Assert.Equal(contextLevel, ScenarioLevels.TimeframeOf(context, ScenarioLevel.Context));
        Assert.Equal(setup, ScenarioLevels.TimeframeOf(context, ScenarioLevel.Setup));
        Assert.Equal(trigger, ScenarioLevels.TimeframeOf(context, ScenarioLevel.Trigger));
        Assert.Equal(ScenarioLevel.Setup, ScenarioLevels.LevelOf(context, setup));
    }

    [Fact]
    public void Only_1d_and_4h_are_contexts()
    {
        Assert.Equal(new[] { Timeframe.H4, Timeframe.D1 }, Timeframes.All.Where(ScenarioLevels.IsContext));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioLevels.TimeframeOf(Timeframe.H1, ScenarioLevel.Setup));
        Assert.Null(ScenarioLevels.LevelOf(Timeframe.D1, Timeframe.M15));
    }

    [Theory]
    [InlineData(ScenarioLevel.Context, ScenarioLevel.Context, true)]
    [InlineData(ScenarioLevel.Context, ScenarioLevel.Trigger, true)]
    [InlineData(ScenarioLevel.Setup, ScenarioLevel.Context, false)]
    [InlineData(ScenarioLevel.Setup, ScenarioLevel.Trigger, true)]
    [InlineData(ScenarioLevel.Trigger, ScenarioLevel.Setup, false)]
    public void A_level_is_shown_on_itself_and_the_lower_levels_never_on_a_higher_one(ScenarioLevel owner, ScenarioLevel shown, bool expected)
    {
        Assert.Equal(expected, ScenarioLevels.IsShownOn(owner, shown));
    }

    [Fact]
    public void The_codes_are_those_of_the_drawings_section()
    {
        Assert.Equal(new[] { "context", "setup", "trigger" }, ScenarioLevels.All.Select(ScenarioLevels.Code));
        Assert.True(ScenarioLevels.TryParse("setup", out var level));
        Assert.Equal(ScenarioLevel.Setup, level);
        Assert.False(ScenarioLevels.TryParse("Setup", out _));
    }

    [Fact]
    public void The_points_belong_to_their_level()
    {
        Assert.Equal(ScenarioLevel.Context, ScenarioPoints.LevelOf(ScenarioPointName.Wave2End));
        Assert.Equal(ScenarioLevel.Setup, ScenarioPoints.LevelOf(ScenarioPointName.Wave1Iii));
        Assert.Equal(ScenarioLevel.Setup, ScenarioPoints.LevelOf(ScenarioPointName.Wave2B));
        Assert.Equal(ScenarioLevel.Trigger, ScenarioPoints.LevelOf(ScenarioPointName.StopMove));
        Assert.All(Enum.GetValues<ScenarioPointName>(), name => Assert.NotEmpty(ScenarioPoints.FieldOf(name)));
        Assert.Equal("trade.exits[2]", ScenarioPoints.FieldOf(ScenarioPointName.Exit, 2));
    }
}
