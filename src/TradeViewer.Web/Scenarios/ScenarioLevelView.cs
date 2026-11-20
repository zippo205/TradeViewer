using TradeViewer.Contracts.Overlays;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Web.Scenarios;

/// <summary>
/// What the chart of an expert scenario shows on its three levels (SC11, SC21, decision 0012): the levels and their
/// timeframes as the Api names them, the level shown, and the levels the user has hidden. What belongs to a level is
/// shown on its own timeframe and on the lower levels, never on a higher one. The hidden levels stay while the user
/// steps through scenarios.
/// </summary>
public sealed class ScenarioLevelView
{
    public static readonly IReadOnlyList<string> Order = new[] { "context", "setup", "trigger" };

    private readonly HashSet<string> _hidden = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyList<ScenarioLevelRecord> Levels { get; private set; } = Array.Empty<ScenarioLevelRecord>();

    /// <summary>The level shown; context when a scenario is opened.</summary>
    public string Shown { get; private set; } = "context";

    public string? Timeframe => Levels.FirstOrDefault(level => level.Level == Shown)?.Timeframe;

    /// <summary>The levels of a scenario just opened; it starts on its context (SC2).</summary>
    public void Open(IReadOnlyList<ScenarioLevelRecord> levels)
    {
        Levels = levels;
        Shown = "context";
    }

    public void Show(string level)
    {
        if (Order.Contains(level))
        {
            Shown = level;
        }
    }

    public bool IsHidden(string level) => _hidden.Contains(level);

    public void Toggle(string level)
    {
        if (!_hidden.Remove(level))
        {
            _hidden.Add(level);
        }
    }

    /// <summary>Whether what belongs to the level is drawn on the level shown.</summary>
    public bool IsVisible(string? level)
    {
        var owner = Array.IndexOf(Order.ToArray(), level ?? "context");
        var shown = Array.IndexOf(Order.ToArray(), Shown);
        return owner >= 0 && owner <= shown && !_hidden.Contains(level ?? "context");
    }

    /// <summary>The keys of the groups the chart hides on the level shown.</summary>
    public IReadOnlyList<string> HiddenKeys(IEnumerable<OverlayGroupRecord> groups)
    {
        return groups.Where(group => !IsVisible(group.Level)).Select(group => group.Key).ToList();
    }
}
