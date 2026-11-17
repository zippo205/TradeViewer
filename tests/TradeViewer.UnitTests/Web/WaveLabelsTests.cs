using TradeViewer.Contracts.Drawings;
using TradeViewer.Domain.Drawings;
using TradeViewer.Web.Drawings;

namespace TradeViewer.UnitTests.Web;

/// <summary>DR5: the chooser of Web offers exactly the notation of the Domain, and labels the points of a count.</summary>
public class WaveLabelsTests
{
    [Fact]
    public void Web_knows_the_same_degrees_labels_and_order_as_the_Domain()
    {
        Assert.Equal(WaveNotation.Degrees.Select(DrawingCodes.Code), WaveLabels.Degrees.Select(degree => degree.Code));
        Assert.Equal(WaveNotation.Labels, WaveLabels.Labels);
        foreach (var label in WaveNotation.Labels)
        {
            Assert.Equal(WaveNotation.Next(label), WaveLabels.Next(label));
        }
    }

    [Fact]
    public void Web_writes_every_label_of_every_degree_as_the_Domain_does()
    {
        foreach (var degree in WaveNotation.Degrees)
        {
            foreach (var label in WaveNotation.Labels)
            {
                Assert.Equal(WaveNotation.Text(degree, label).ToString(), WaveLabels.Written(DrawingCodes.Code(degree), label));
            }
        }
    }

    [Fact]
    public void A_count_labels_the_points_after_the_start_above_highs_and_below_lows()
    {
        var time = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        var prices = new[] { 100.0, 120, 110, 140, 130, 150 };
        var anchors = prices.Select((price, index) => new AnchorRecord(time.AddHours(index), price)).ToList();

        var labels = WaveLabels.ForPoints(anchors, "minor", "1");

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, labels.Select(label => label.Point));
        Assert.Equal(new[] { "1", "2", "3", "4", "5" }, labels.Select(label => label.Mark.Label));
        Assert.Equal(new[] { "above", "below", "above", "below", "above" }, labels.Select(label => label.Mark.Side));
        Assert.All(labels, label => Assert.Equal("minor", label.Mark.Degree));
    }

    [Fact]
    public void A_correction_counted_from_A_goes_on_with_the_letters()
    {
        var time = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        var anchors = new[] { 150.0, 130, 140, 120 }.Select((price, index) => new AnchorRecord(time.AddHours(index), price)).ToList();

        var labels = WaveLabels.ForPoints(anchors, "intermediate", "A");

        Assert.Equal(new[] { "A", "B", "C" }, labels.Select(label => label.Mark.Label));
        Assert.Equal(new[] { "below", "above", "below" }, labels.Select(label => label.Mark.Side));
    }
}
