using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Web.Drawings;

/// <summary>
/// The wave labels as the chooser offers them (DR5): the nine degrees, the labels, the label that follows, and how
/// a label is written in its degree. The same table as WaveNotation in the Domain, which Web cannot reference; a
/// unit test keeps both equal.
/// </summary>
public static class WaveLabels
{
    private static readonly string[] UpperRoman = { "I", "II", "III", "IV", "V" };
    private static readonly string[] LowerRoman = { "i", "ii", "iii", "iv", "v" };

    /// <summary>The degrees with their names, largest first.</summary>
    public static IReadOnlyList<(string Code, string Title)> Degrees { get; } = new[]
    {
        ("grandSupercycle", "Grand Supercycle"),
        ("supercycle", "Supercycle"),
        ("cycle", "Cycle"),
        ("primary", "Primary"),
        ("intermediate", "Intermediate"),
        ("minor", "Minor"),
        ("minute", "Minute"),
        ("minuette", "Minuette"),
        ("subminuette", "Subminuette")
    };

    public static IReadOnlyList<string> Labels { get; } = new[] { "1", "2", "3", "4", "5", "A", "B", "C", "D", "E", "W", "X", "Y" };

    /// <summary>The degree a new count starts with.</summary>
    public const string FirstDegree = "minor";

    /// <summary>The label the chooser offers after this one: 1 to 5, A to E, W to Y, each run starting again.</summary>
    public static string Next(string label)
    {
        switch (label)
        {
            case "5":
                return "1";
            case "E":
                return "A";
            case "Y":
                return "W";
            default:
                var index = IndexOf(Labels, label);
                return index < 0 || index + 1 >= Labels.Count ? "1" : Labels[index + 1];
        }
    }

    /// <summary>The label as the textbook writes it in the degree: ((1)), (iv), C.</summary>
    public static string Written(string degree, string label)
    {
        var index = -1;
        for (var i = 0; i < Degrees.Count; i++)
        {
            if (Degrees[i].Code == degree)
            {
                index = i;
            }
        }

        if (index < 0 || IndexOf(Labels, label) < 0)
        {
            return label;
        }

        var group = index / 3;
        var number = "12345".IndexOf(label, StringComparison.Ordinal);
        string text;
        if (number >= 0)
        {
            text = group == 0 ? UpperRoman[number] : group == 1 ? label : LowerRoman[number];
        }
        else
        {
            text = group == 1 ? label : label.ToLowerInvariant();
        }

        switch (index % 3)
        {
            case 0:
                return "((" + text + "))";
            case 1:
                return "(" + text + ")";
            default:
                return text;
        }
    }

    /// <summary>
    /// Labels for the points of a polyline: the points after the first get the labels from `first` on, as a count
    /// starts at the beginning of the move. A point higher than the one before is labelled above, a lower one below.
    /// </summary>
    public static IReadOnlyList<PointLabelRecord> ForPoints(IReadOnlyList<AnchorRecord> anchors, string degree, string first)
    {
        var labels = new List<PointLabelRecord>();
        var label = first;
        for (var point = 1; point < anchors.Count; point++)
        {
            var higher = (anchors[point].Price ?? 0) >= (anchors[point - 1].Price ?? 0);
            labels.Add(new PointLabelRecord(point, new WaveMarkRecord(degree, label, higher ? "above" : "below")));
            label = Next(label);
        }

        return labels;
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index] == value)
            {
                return index;
            }
        }

        return -1;
    }
}
