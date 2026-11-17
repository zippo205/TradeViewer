namespace TradeViewer.Domain.Drawings;

/// <summary>The default presets of DR4 and the rules of a preset.</summary>
public static class FibonacciPresets
{
    /// <summary>The most levels a preset or a Fibonacci drawing may have.</summary>
    public const int MaxLevels = 30;

    /// <summary>The largest ratio, up or down, a level may have.</summary>
    public const double MaxRatio = 100;

    public const int MaxNameLength = 60;

    /// <summary>The default retracement: 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1.</summary>
    public static FibonacciPreset Retracement { get; } = new FibonacciPreset(
        new Guid("6c0f3b1e-5d2a-4c1b-9a7e-000000000001"),
        "Retracement",
        FibonacciKind.Retracement,
        new[] { 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1 });

    /// <summary>The default extension: 1, 1.272, 1.618, 2, 2.618, 3.618.</summary>
    public static FibonacciPreset Extension { get; } = new FibonacciPreset(
        new Guid("6c0f3b1e-5d2a-4c1b-9a7e-000000000002"),
        "Extension",
        FibonacciKind.Extension,
        new[] { 1, 1.272, 1.618, 2, 2.618, 3.618 });

    /// <summary>The presets a new workspace starts with.</summary>
    public static IReadOnlyList<FibonacciPreset> Defaults { get; } = new[] { Retracement, Extension };

    /// <summary>The problems of a preset, by field; empty when it is valid.</summary>
    public static IReadOnlyDictionary<string, string[]> Check(FibonacciPreset preset)
    {
        var problems = new Dictionary<string, string[]>();

        if (preset.Id == Guid.Empty)
        {
            problems["id"] = new[] { "The preset needs an id." };
        }

        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            problems["name"] = new[] { "The preset needs a name." };
        }
        else if (preset.Name.Length > MaxNameLength)
        {
            problems["name"] = new[] { $"The name may have at most {MaxNameLength} characters." };
        }

        if (!Enum.IsDefined(preset.Kind))
        {
            problems["kind"] = new[] { "The kind is retracement or extension." };
        }

        var levelProblem = CheckLevels(preset.Levels);
        if (levelProblem != null)
        {
            problems["levels"] = new[] { levelProblem };
        }

        return problems;
    }

    /// <summary>The problem of a list of levels, or null: one to <see cref="MaxLevels"/> finite ratios within ±<see cref="MaxRatio"/>, none twice.</summary>
    public static string? CheckLevels(IReadOnlyList<double>? levels)
    {
        if (levels == null || levels.Count == 0)
        {
            return "At least one level is required.";
        }

        if (levels.Count > MaxLevels)
        {
            return $"At most {MaxLevels} levels are allowed.";
        }

        if (levels.Any(level => !double.IsFinite(level) || Math.Abs(level) > MaxRatio))
        {
            return $"Every level is a number between -{MaxRatio} and {MaxRatio}.";
        }

        if (levels.Distinct().Count() != levels.Count)
        {
            return "A level may appear only once.";
        }

        return null;
    }
}
