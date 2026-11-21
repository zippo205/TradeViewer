using TradeViewer.Contracts.Drawings;
using TradeViewer.Web.Drawings;

namespace TradeViewer.UnitTests.Web;

/// <summary>DR1-DR4, SC20: the tool of the rail and what a new drawing of it gets, shared by the chart page and the editor.</summary>
public class DrawingToolChoiceTests
{
    private static readonly FibonacciPresetRecord Standard = new FibonacciPresetRecord(Guid.NewGuid(), "Standard", "retracement", new[] { new FibonacciLevelRecord(0.618, true) });
    private static readonly FibonacciPresetRecord Deep = new FibonacciPresetRecord(Guid.NewGuid(), "Deep", "retracement", new[] { new FibonacciLevelRecord(0.786, true) });

    [Fact]
    public void A_pattern_is_placed_as_a_polyline_with_its_labels_and_ends_with_another_tool()
    {
        var tools = new DrawingToolChoice();

        Assert.Equal("polyline", tools.ChoosePattern("impulse"));
        tools.ToolChanged("polyline");
        Assert.True(tools.ChoosesWave);
        Assert.NotNull(tools.ExtrasFor("polyline"));

        tools.ToolChanged("trendLine");
        Assert.Null(tools.Pattern);
        Assert.Equal("impulse", tools.LastPattern);
    }

    [Fact]
    public void The_free_polyline_is_no_pattern_but_the_group_remembers_it()
    {
        var tools = new DrawingToolChoice();
        tools.ChoosePattern("impulse");

        Assert.Equal("polyline", tools.ChooseTool("polyline"));

        Assert.Null(tools.Pattern);
        Assert.Equal("polyline", tools.LastPattern);
        Assert.Null(tools.ExtrasFor("polyline"));
    }

    [Fact]
    public void A_wave_label_placed_moves_the_chooser_to_the_next_label()
    {
        var tools = new DrawingToolChoice();
        tools.ToolChanged("waveLabel");

        Assert.True(tools.Created("waveLabel", new WaveMarkRecord(WaveLabels.FirstDegree, "1", "above")));

        Assert.Equal(WaveLabels.Next("1"), tools.WaveLabel);
        Assert.False(tools.Created("trendLine", null));
    }

    [Fact]
    public void The_chosen_preset_gives_its_levels_the_first_one_before_a_choice()
    {
        var tools = new DrawingToolChoice { Presets = new[] { Standard, Deep } };

        Assert.Same(Standard, tools.PresetOf("retracement"));
        tools.ToolChanged("fibonacciRetracement");
        Assert.True(tools.ChoosePreset("retracement", Deep.Id));
        Assert.Same(Deep, tools.PresetOf("retracement"));
        Assert.Null(DrawingToolChoice.KindOf("trendLine"));
        Assert.Equal("extension", DrawingToolChoice.KindOf("fibonacciExtension"));
    }
}
