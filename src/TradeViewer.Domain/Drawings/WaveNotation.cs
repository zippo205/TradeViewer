namespace TradeViewer.Domain.Drawings;

/// <summary>
/// The notation of the wave labels (DR5, the table accepted in the plan of Drawing Tools). Each degree writes
/// the same labels in its own form:
/// <code>
/// Grand Supercycle   ((I)) ... ((V))   ((a)) ((b)) ((c))
/// Supercycle         (I) ... (V)       (a) (b) (c)
/// Cycle              I ... V           a b c
/// Primary            ((1)) ... ((5))   ((A)) ((B)) ((C))
/// Intermediate       (1) ... (5)       (A) (B) (C)
/// Minor              1 ... 5           A B C
/// Minute             ((i)) ... ((v))   ((a)) ((b)) ((c))
/// Minuette           (i) ... (v)       (a) (b) (c)
/// Subminuette        i ... v           a b c
/// </code>
/// W, X, Y, Z and D, E take the form of the letters of the degree.
/// </summary>
public static class WaveNotation
{
    private static readonly string[] UpperRoman = { "I", "II", "III", "IV", "V" };
    private static readonly string[] LowerRoman = { "i", "ii", "iii", "iv", "v" };

    /// <summary>The labels as they are stored: the five waves of a motive move, the letters of corrections and triangles, and those of combinations.</summary>
    public static IReadOnlyList<string> Labels { get; } = new[] { "1", "2", "3", "4", "5", "A", "B", "C", "D", "E", "W", "X", "Y", "Z" };

    /// <summary>All degrees, largest first.</summary>
    public static IReadOnlyList<WaveDegree> Degrees { get; } = new[]
    {
        WaveDegree.GrandSupercycle, WaveDegree.Supercycle, WaveDegree.Cycle,
        WaveDegree.Primary, WaveDegree.Intermediate, WaveDegree.Minor,
        WaveDegree.Minute, WaveDegree.Minuette, WaveDegree.Subminuette
    };

    public static bool IsLabel(string? label)
    {
        return label != null && Labels.Contains(label);
    }

    /// <summary>
    /// The label the chooser offers after this one has been placed (plan: "the chooser moves to the next
    /// label"): 1 to 5, A to E, W to Z, each run starting again after its last label.
    /// </summary>
    public static string Next(string label)
    {
        switch (label)
        {
            case "5":
                return "1";
            case "E":
                return "A";
            case "Z":
                return "W";
            default:
                for (var index = 0; index < Labels.Count; index++)
                {
                    if (Labels[index] == label)
                    {
                        return Labels[index + 1];
                    }
                }

                throw new ArgumentException("Not a wave label: " + label, nameof(label));
        }
    }

    /// <summary>The label as it is drawn in the degree.</summary>
    public static WaveText Text(WaveDegree degree, string label)
    {
        if (!IsLabel(label))
        {
            throw new ArgumentException("Not a wave label: " + label, nameof(label));
        }

        var group = (int)degree / 3; // 0: Grand Supercycle to Cycle, 1: Primary to Minor, 2: Minute to Subminuette
        var enclosure = ((int)degree % 3) switch
        {
            0 => WaveEnclosure.Circle,
            1 => WaveEnclosure.Parentheses,
            _ => WaveEnclosure.None
        };

        string text;
        if (char.IsDigit(label[0]))
        {
            var number = label[0] - '1';
            text = group switch
            {
                0 => UpperRoman[number],
                1 => label,
                _ => LowerRoman[number]
            };
        }
        else
        {
            text = group == 1 ? label : label.ToLowerInvariant();
        }

        return new WaveText(text, enclosure);
    }
}
