using TradeViewer.Domain.Drawings;

namespace TradeViewer.UnitTests.Domain.Drawings;

/// <summary>DR4: the default presets, their levels on and off, and the rules of a preset.</summary>
public class FibonacciPresetsTests
{
    [Fact]
    public void The_defaults_show_the_levels_of_the_specification()
    {
        Assert.Equal(new[] { 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1 }, Shown(FibonacciPresets.Retracement));
        Assert.Equal(new[] { 1, 1.272, 1.618, 2, 2.618, 3.618 }, Shown(FibonacciPresets.Extension));
        Assert.Equal(FibonacciKind.Retracement, FibonacciPresets.Retracement.Kind);
        Assert.Equal(FibonacciKind.Extension, FibonacciPresets.Extension.Kind);
    }

    [Fact]
    public void The_defaults_hold_more_levels_switched_off_in_the_order_of_their_ratios()
    {
        Assert.Equal(new[] { 1.272, 1.414, 1.618, 2, 2.618 }, Off(FibonacciPresets.Retracement));
        Assert.Equal(new[] { 0, 0.382, 0.5, 0.618, 0.786, 4.236 }, Off(FibonacciPresets.Extension));
        foreach (var preset in FibonacciPresets.Defaults)
        {
            Assert.Equal(preset.Levels.Select(level => level.Ratio).OrderBy(ratio => ratio), preset.Levels.Select(level => level.Ratio));
        }
    }

    [Fact]
    public void The_defaults_are_valid_and_have_fixed_distinct_ids()
    {
        foreach (var preset in FibonacciPresets.Defaults)
        {
            Assert.Empty(FibonacciPresets.Check(preset));
        }

        Assert.NotEqual(FibonacciPresets.Retracement.Id, FibonacciPresets.Extension.Id);
    }

    [Fact]
    public void A_preset_needs_an_id_a_name_and_levels()
    {
        var problems = FibonacciPresets.Check(new FibonacciPreset(Guid.Empty, " ", FibonacciKind.Retracement, Array.Empty<FibonacciLevel>()));

        Assert.Contains("id", problems.Keys);
        Assert.Contains("name", problems.Keys);
        Assert.Contains("levels", problems.Keys);
    }

    [Fact]
    public void A_name_that_is_too_long_and_an_unknown_kind_are_refused()
    {
        var problems = FibonacciPresets.Check(new FibonacciPreset(Guid.NewGuid(), new string('n', FibonacciPresets.MaxNameLength + 1), (FibonacciKind)7, Levels(0.5)));

        Assert.Contains("name", problems.Keys);
        Assert.Contains("kind", problems.Keys);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(100.5)]
    [InlineData(-101)]
    public void A_level_must_be_a_finite_ratio_within_the_limit(double level)
    {
        Assert.NotNull(FibonacciPresets.CheckLevels(Levels(0, level)));
    }

    [Fact]
    public void Negative_levels_and_levels_above_one_are_allowed_once_each_whether_on_or_off()
    {
        Assert.Null(FibonacciPresets.CheckLevels(Levels(-0.272, 0, 1, 1.618)));
        Assert.NotNull(FibonacciPresets.CheckLevels(new[] { new FibonacciLevel(1.618, true), new FibonacciLevel(1.618, false) }));
        Assert.NotNull(FibonacciPresets.CheckLevels(Levels(Enumerable.Range(0, FibonacciPresets.MaxLevels + 1).Select(index => index / 10.0).ToArray())));
    }

    [Fact]
    public void All_levels_may_be_switched_off()
    {
        Assert.Null(FibonacciPresets.CheckLevels(new[] { new FibonacciLevel(0.5, false), new FibonacciLevel(1, false) }));
    }

    private static FibonacciLevel[] Levels(params double[] ratios)
    {
        return ratios.Select(ratio => new FibonacciLevel(ratio)).ToArray();
    }

    private static double[] Shown(FibonacciPreset preset)
    {
        return preset.Levels.Where(level => level.Visible).Select(level => level.Ratio).ToArray();
    }

    private static double[] Off(FibonacciPreset preset)
    {
        return preset.Levels.Where(level => !level.Visible).Select(level => level.Ratio).ToArray();
    }
}
