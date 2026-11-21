using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Web.Drawings;

/// <summary>
/// The tool of the tool rail in use and what a new drawing of it gets (DR1-DR4): the Elliott pattern chosen in the
/// group of the rail, the degree and label of the wave chooser, the Fibonacci preset chosen per kind. Shared by the
/// chart page and the scenario editor (SC20), so both rails behave the same. The chart module reports the tool it
/// actually uses (<see cref="ToolChanged"/>); a true from a method means: send the tool again with its new extras.
/// </summary>
public sealed class DrawingToolChoice
{
    /// <summary>The preset chosen per kind (retracement, extension) in this window; the first of the kind before a choice.</summary>
    private readonly Dictionary<string, Guid> _chosenPresets = new Dictionary<string, Guid>();

    /// <summary>The tool in use as the chart reports it; null for the pointer.</summary>
    public string? Tool { get; private set; }

    /// <summary>The Elliott pattern being placed (a polyline whose points get its labels); null for none.</summary>
    public string? Pattern { get; private set; }

    /// <summary>The entry of the Elliott group used last; the group shows it.</summary>
    public string? LastPattern { get; private set; }

    public string WaveDegree { get; private set; } = WaveLabels.FirstDegree;

    public string WaveLabel { get; private set; } = "1";

    public IReadOnlyList<FibonacciPresetRecord> Presets { get; set; } = Array.Empty<FibonacciPresetRecord>();

    /// <summary>The wave chooser is shown: the wave label tool or a pattern is in use.</summary>
    public bool ChoosesWave => Tool == "waveLabel" || Pattern != null;

    /// <summary>A tool of the rail chosen (the free polyline too, which is no pattern); returns the tool to send.</summary>
    public string? ChooseTool(string? tool)
    {
        Pattern = null;
        if (tool == "waveLabel" || tool == "polyline")
        {
            LastPattern = tool; // the Elliott group shows it next time
        }

        return tool;
    }

    /// <summary>A wave pattern chosen in the group of the rail; returns the tool to send, the polyline.</summary>
    public string ChoosePattern(string code)
    {
        Pattern = code;
        LastPattern = code;
        return "polyline";
    }

    /// <summary>The chart reports the tool it uses; a pattern ends with its polyline.</summary>
    public void ToolChanged(string? tool)
    {
        Tool = tool;
        if (tool != "polyline")
        {
            Pattern = null;
        }
    }

    /// <summary>The wave chooser changed; true when the tool in use must get the new label.</summary>
    public bool ChooseWave(string degree, string label)
    {
        WaveDegree = degree;
        WaveLabel = label;
        return ChoosesWave;
    }

    /// <summary>A wave label was placed: the chooser moves on to the next label; true when the tool must place that one.</summary>
    public bool Created(string tool, WaveMarkRecord? wave)
    {
        if (tool != "waveLabel" || wave == null)
        {
            return false;
        }

        WaveDegree = wave.Degree;
        WaveLabel = WaveLabels.Next(wave.Label);
        return Tool == "waveLabel";
    }

    /// <summary>Another preset chosen; true when the tool in use places its levels from now on.</summary>
    public bool ChoosePreset(string kind, Guid id)
    {
        _chosenPresets[kind] = id;
        return KindOf(Tool) == kind;
    }

    /// <summary>What a new drawing of the tool gets besides its anchors: a wave mark, the labels of a pattern, or Fibonacci levels.</summary>
    public object? ExtrasFor(string? tool)
    {
        if (tool == "waveLabel")
        {
            return new { wave = new { degree = WaveDegree, label = WaveLabel } };
        }

        var pattern = tool == "polyline" ? WavePatterns.Find(Pattern) : null;
        if (pattern != null)
        {
            return new { pattern = pattern.Labels, degree = WaveDegree };
        }

        var kind = KindOf(tool);
        var preset = kind == null ? null : PresetOf(kind);
        return preset == null ? null : new { levels = preset.Levels };
    }

    /// <summary>The kind of preset a Fibonacci tool uses; null for the other tools.</summary>
    public static string? KindOf(string? tool)
    {
        switch (tool)
        {
            case "fibonacciRetracement":
                return "retracement";
            case "fibonacciExtension":
                return "extension";
            default:
                return null;
        }
    }

    public IReadOnlyList<FibonacciPresetRecord> PresetsOf(string kind)
    {
        return Presets.Where(preset => preset.Kind == kind).ToList();
    }

    public FibonacciPresetRecord? PresetOf(string kind)
    {
        var presets = PresetsOf(kind);
        return _chosenPresets.TryGetValue(kind, out var id) && presets.Any(preset => preset.Id == id)
            ? presets.First(preset => preset.Id == id)
            : presets.FirstOrDefault();
    }
}
