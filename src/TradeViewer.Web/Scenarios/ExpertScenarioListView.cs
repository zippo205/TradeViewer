using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Web.Scenarios;

/// <summary>
/// The list of expert scenarios as the page shows it (SC7): filtered by asset, verdict and draft mark, sorted by any
/// column; "previous" and "next" step through it in that order (SC4). A file that cannot be read stays in the list
/// with its error, whatever the filters, so it is never hidden.
/// </summary>
public sealed class ExpertScenarioListView
{
    public const string AnyValue = "";

    public static readonly IReadOnlyList<string> Columns = new[] { "id", "asset", "timeframe", "verdict", "draft" };

    private IReadOnlyList<ExpertScenarioSummary> _all = Array.Empty<ExpertScenarioSummary>();

    public string Asset { get; set; } = AnyValue;

    public string Verdict { get; set; } = AnyValue;

    /// <summary>"" any, "draft" only drafts, "final" only those without the mark.</summary>
    public string Draft { get; set; } = AnyValue;

    public string SortColumn { get; private set; } = "id";

    public bool Descending { get; private set; }

    public IReadOnlyList<ExpertScenarioSummary> All => _all;

    /// <summary>The assets of the list, for the filter.</summary>
    public IReadOnlyList<string> Assets => _all.Where(head => head.Symbol != null).Select(head => head.Symbol!).Distinct().OrderBy(symbol => symbol, StringComparer.Ordinal).ToList();

    public void Set(IReadOnlyList<ExpertScenarioSummary> heads)
    {
        _all = heads;
    }

    /// <summary>Sorts by the column; the same column again turns the order around.</summary>
    public void SortBy(string column)
    {
        if (SortColumn == column)
        {
            Descending = !Descending;
            return;
        }

        SortColumn = Columns.Contains(column) ? column : "id";
        Descending = false;
    }

    public IReadOnlyList<ExpertScenarioSummary> Shown()
    {
        var shown = _all.Where(Matches);
        var sorted = SortColumn switch
        {
            "asset" => shown.OrderBy(head => head.Symbol ?? "", StringComparer.Ordinal).ThenBy(head => head.Id, StringComparer.Ordinal),
            "timeframe" => shown.OrderBy(head => head.ContextTimeframe ?? "", StringComparer.Ordinal).ThenBy(head => head.Id, StringComparer.Ordinal),
            "verdict" => shown.OrderBy(head => head.Verdict ?? "", StringComparer.Ordinal).ThenBy(head => head.Id, StringComparer.Ordinal),
            "draft" => shown.OrderBy(head => head.Draft).ThenBy(head => head.Id, StringComparer.Ordinal),
            _ => shown.OrderBy(head => head.Id, StringComparer.Ordinal)
        };

        var list = sorted.ToList();
        if (Descending)
        {
            list.Reverse();
        }

        return list;
    }

    /// <summary>The scenario before (step -1) or after (step +1) the one shown, in the order shown; null at either end.</summary>
    public string? Step(string? current, int step)
    {
        var shown = Shown();
        var index = shown.ToList().FindIndex(head => head.Id == current);
        if (index < 0)
        {
            return shown.Count > 0 && step > 0 ? shown[0].Id : null;
        }

        var next = index + step;
        return next >= 0 && next < shown.Count ? shown[next].Id : null;
    }

    private bool Matches(ExpertScenarioSummary head)
    {
        if (head.Error != null)
        {
            return true;
        }

        return (Asset == AnyValue || head.Symbol == Asset)
            && (Verdict == AnyValue || head.Verdict == Verdict)
            && (Draft == AnyValue || (Draft == "draft") == head.Draft);
    }
}
