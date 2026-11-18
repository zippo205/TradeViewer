using TradeViewer.Domain.Drawings;

namespace TradeViewer.Domain.Indicators;

/// <summary>
/// Which parameters, instances and templates are valid. The problems are reported by field, as the
/// validators of the Api report them; empty means valid. What could not be calculated or read again is
/// refused rather than stored (priority 1).
/// </summary>
public static class IndicatorRules
{
    public const int MaxLength = 5000;
    public const double MinDeviation = 0.001;
    public const double MaxDeviation = 50;
    public const int MaxTemplateName = 100;
    public const int MaxIndicators = 50;

    public static IReadOnlyDictionary<string, string[]> Check(IndicatorParameters? parameters)
    {
        var problems = new Dictionary<string, string[]>();
        if (parameters == null)
        {
            problems["parameters"] = new[] { "The indicator needs its parameters." };
            return problems;
        }

        switch (parameters)
        {
            case SmaParameters p:
                CheckLength(problems, "length", p.Length);
                CheckSource(problems, p.Source);
                break;
            case EmaParameters p:
                CheckLength(problems, "length", p.Length);
                CheckSource(problems, p.Source);
                break;
            case BollingerBandsParameters p:
                CheckLength(problems, "length", p.Length);
                CheckSource(problems, p.Source);
                if (!double.IsFinite(p.Deviation) || p.Deviation < MinDeviation || p.Deviation > MaxDeviation)
                {
                    problems["deviation"] = new[] { $"The standard deviation is {MinDeviation} to {MaxDeviation}." };
                }

                break;
            case RsiParameters p:
                CheckLength(problems, "length", p.Length);
                CheckSource(problems, p.Source);
                break;
            case MacdParameters p:
                CheckLength(problems, "fast", p.Fast);
                CheckLength(problems, "slow", p.Slow);
                CheckLength(problems, "signal", p.Signal);
                CheckSource(problems, p.Source);
                if (!problems.ContainsKey("fast") && !problems.ContainsKey("slow") && p.Fast >= p.Slow)
                {
                    problems["fast"] = new[] { "The fast length is shorter than the slow one." };
                }

                break;
            case AtrParameters p:
                CheckLength(problems, "length", p.Length);
                break;
            default:
                problems["type"] = new[] { "Unknown indicator." };
                break;
        }

        return problems;
    }

    /// <summary>The problems of an instance; the fields of its parameters are prefixed with "parameters.".</summary>
    public static IReadOnlyDictionary<string, string[]> Check(IndicatorInstance instance)
    {
        var problems = new Dictionary<string, string[]>();

        if (instance.Id == Guid.Empty)
        {
            problems["id"] = new[] { "The indicator needs an id." };
        }

        foreach (var (field, messages) in Check(instance.Parameters))
        {
            problems["parameters." + field] = messages;
        }

        if (instance.Parameters != null && Enum.IsDefined(instance.Parameters.Type))
        {
            CheckStyles(problems, instance.Parameters.Type, instance.Styles);
        }

        return problems;
    }

    public static IReadOnlyDictionary<string, string[]> Check(IndicatorTemplate template)
    {
        var problems = new Dictionary<string, string[]>();

        if (template.Id == Guid.Empty)
        {
            problems["id"] = new[] { "The template needs an id." };
        }

        if (string.IsNullOrWhiteSpace(template.Name) || template.Name.Length > MaxTemplateName)
        {
            problems["name"] = new[] { $"The name has 1 to {MaxTemplateName} characters." };
        }

        CheckList(problems, "indicators", template.Indicators, empty: false);
        return problems;
    }

    /// <summary>The problems of the indicators of a chart or a template, by "indicators[i].field".</summary>
    public static void CheckList(Dictionary<string, string[]> problems, string field, IReadOnlyList<IndicatorInstance>? indicators, bool empty)
    {
        if (indicators == null || (!empty && indicators.Count == 0))
        {
            problems[field] = new[] { "At least one indicator is needed." };
            return;
        }

        if (indicators.Count > MaxIndicators)
        {
            problems[field] = new[] { $"At most {MaxIndicators} indicators." };
            return;
        }

        var ids = new HashSet<Guid>();
        for (var i = 0; i < indicators.Count; i++)
        {
            if (indicators[i] == null)
            {
                problems[$"{field}[{i}]"] = new[] { "An indicator is missing." };
                continue;
            }

            foreach (var (name, messages) in Check(indicators[i]))
            {
                problems[$"{field}[{i}].{name}"] = messages;
            }

            if (indicators[i].Id != Guid.Empty && !ids.Add(indicators[i].Id))
            {
                problems[$"{field}[{i}].id"] = new[] { "Two indicators have the same id." };
            }
        }
    }

    private static void CheckLength(Dictionary<string, string[]> problems, string field, int length)
    {
        if (length < 1 || length > MaxLength)
        {
            problems[field] = new[] { $"The length is 1 to {MaxLength}." };
        }
    }

    private static void CheckSource(Dictionary<string, string[]> problems, PriceSource source)
    {
        if (!Enum.IsDefined(source))
        {
            problems["source"] = new[] { "The price source is open, high, low, close, hl2, hlc3 or ohlc4." };
        }
    }

    private static void CheckStyles(Dictionary<string, string[]> problems, IndicatorType type, IReadOnlyList<IndicatorLineStyle>? styles)
    {
        var entries = IndicatorStyles.Entries(type);
        if (styles == null || styles.Count != entries.Count || styles.Any(s => s == null) ||
            !styles.Select(s => s.Line).Order(StringComparer.Ordinal).SequenceEqual(entries.Order(StringComparer.Ordinal)))
        {
            problems["styles"] = new[] { $"The styles are {string.Join(", ", entries)}, each once." };
            return;
        }

        foreach (var style in styles)
        {
            if (!DrawingRules.IsColor(style.Color))
            {
                problems[$"styles.{style.Line}.color"] = new[] { "The colour is written #RRGGBB or #RRGGBBAA." };
            }

            if (style.Width < 1 || style.Width > 4)
            {
                problems[$"styles.{style.Line}.width"] = new[] { "The width is 1 to 4 pixels." };
            }
        }
    }
}
