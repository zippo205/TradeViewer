using TradeViewer.Domain.Drawings;

namespace TradeViewer.UnitTests.Domain.Drawings;

/// <summary>DR5: the nine degrees write their labels in the notation accepted in the plan of Drawing Tools.</summary>
public class WaveNotationTests
{
    [Theory]
    [InlineData(WaveDegree.GrandSupercycle, "((I)) ((II)) ((III)) ((IV)) ((V))", "((a)) ((b)) ((c))")]
    [InlineData(WaveDegree.Supercycle, "(I) (II) (III) (IV) (V)", "(a) (b) (c)")]
    [InlineData(WaveDegree.Cycle, "I II III IV V", "a b c")]
    [InlineData(WaveDegree.Primary, "((1)) ((2)) ((3)) ((4)) ((5))", "((A)) ((B)) ((C))")]
    [InlineData(WaveDegree.Intermediate, "(1) (2) (3) (4) (5)", "(A) (B) (C)")]
    [InlineData(WaveDegree.Minor, "1 2 3 4 5", "A B C")]
    [InlineData(WaveDegree.Minute, "((i)) ((ii)) ((iii)) ((iv)) ((v))", "((a)) ((b)) ((c))")]
    [InlineData(WaveDegree.Minuette, "(i) (ii) (iii) (iv) (v)", "(a) (b) (c)")]
    [InlineData(WaveDegree.Subminuette, "i ii iii iv v", "a b c")]
    public void Each_degree_writes_motive_and_corrective_labels_in_its_form(WaveDegree degree, string motive, string corrective)
    {
        Assert.Equal(motive, Written(degree, "1", "2", "3", "4", "5"));
        Assert.Equal(corrective, Written(degree, "A", "B", "C"));
    }

    [Fact]
    public void Combinations_and_triangles_take_the_form_of_the_letters_of_the_degree()
    {
        Assert.Equal("W X Y D E", Written(WaveDegree.Minor, "W", "X", "Y", "D", "E"));
        Assert.Equal("((w)) ((x)) ((y)) ((d)) ((e))", Written(WaveDegree.Minute, "W", "X", "Y", "D", "E"));
        Assert.Equal("(W) (X) (Y)", Written(WaveDegree.Intermediate, "W", "X", "Y"));
    }

    [Fact]
    public void Double_brackets_are_drawn_as_a_circle()
    {
        var text = WaveNotation.Text(WaveDegree.Primary, "3");

        Assert.Equal("3", text.Text);
        Assert.Equal(WaveEnclosure.Circle, text.Enclosure);
    }

    [Theory]
    [InlineData("1", "2")]
    [InlineData("4", "5")]
    [InlineData("5", "1")]
    [InlineData("A", "B")]
    [InlineData("C", "D")]
    [InlineData("E", "A")]
    [InlineData("W", "X")]
    [InlineData("Y", "W")]
    public void The_chooser_moves_on_to_the_next_label(string label, string next)
    {
        Assert.Equal(next, WaveNotation.Next(label));
    }

    [Theory]
    [InlineData("6")]
    [InlineData("a")]
    [InlineData("")]
    public void An_unknown_label_is_refused(string label)
    {
        Assert.False(WaveNotation.IsLabel(label));
        Assert.Throws<ArgumentException>(() => WaveNotation.Text(WaveDegree.Minor, label));
        Assert.Throws<ArgumentException>(() => WaveNotation.Next(label));
    }

    [Fact]
    public void There_are_nine_degrees_largest_first()
    {
        Assert.Equal(9, WaveNotation.Degrees.Count);
        Assert.Equal(WaveDegree.GrandSupercycle, WaveNotation.Degrees[0]);
        Assert.Equal(WaveDegree.Subminuette, WaveNotation.Degrees[8]);
    }

    private static string Written(WaveDegree degree, params string[] labels)
    {
        return string.Join(" ", labels.Select(label => WaveNotation.Text(degree, label).ToString()));
    }
}
