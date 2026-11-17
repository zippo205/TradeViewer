using TradeViewer.Domain.Drawings;

namespace TradeViewer.UnitTests.Domain.Drawings;

/// <summary>DR4: the default presets and the rules of a preset.</summary>
public class FibonacciPresetsTests
{
    [Fact]
    public void The_defaults_have_the_levels_of_the_specification()
    {
        Assert.Equal(new[] { 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1 }, FibonacciPresets.Retracement.Levels);
        Assert.Equal(new[] { 1, 1.272, 1.618, 2, 2.618, 3.618 }, FibonacciPresets.Extension.Levels);
        Assert.Equal(FibonacciKind.Retracement, FibonacciPresets.Retracement.Kind);
        Assert.Equal(FibonacciKind.Extension, FibonacciPresets.Extension.Kind);
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
        var problems = FibonacciPresets.Check(new FibonacciPreset(Guid.Empty, " ", FibonacciKind.Retracement, Array.Empty<double>()));

        Assert.Contains("id", problems.Keys);
        Assert.Contains("name", problems.Keys);
        Assert.Contains("levels", problems.Keys);
    }

    [Fact]
    public void A_name_that_is_too_long_and_an_unknown_kind_are_refused()
    {
        var problems = FibonacciPresets.Check(new FibonacciPreset(Guid.NewGuid(), new string('n', FibonacciPresets.MaxNameLength + 1), (FibonacciKind)7, new[] { 0.5 }));

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
        Assert.NotNull(FibonacciPresets.CheckLevels(new[] { 0, level }));
    }

    [Fact]
    public void Negative_levels_and_levels_above_one_are_allowed_once_each()
    {
        Assert.Null(FibonacciPresets.CheckLevels(new[] { -0.272, 0, 1, 1.618 }));
        Assert.NotNull(FibonacciPresets.CheckLevels(new[] { 1.618, 1.618 }));
        Assert.NotNull(FibonacciPresets.CheckLevels(Enumerable.Range(0, FibonacciPresets.MaxLevels + 1).Select(index => index / 10.0).ToArray()));
    }
}
