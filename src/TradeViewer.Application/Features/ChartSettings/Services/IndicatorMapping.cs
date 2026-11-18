using TradeViewer.Contracts.Indicators;
using TradeViewer.Domain.Indicators;

namespace TradeViewer.Application.Features.ChartSettings.Services;

/// <summary>
/// Indicators between the API (<see cref="IndicatorRecord"/>: a type code and the parameters by name) and the
/// Domain (<see cref="IndicatorInstance"/>: one parameter record per type). Used by the chart settings and
/// the templates, which both hold indicators.
/// </summary>
public static class IndicatorMapping
{
    public static IndicatorRecord ToRecord(IndicatorInstance instance)
    {
        var styles = instance.Styles.Select(style => new IndicatorStyleRecord(style.Line, style.Color, style.Width)).ToList();
        var type = IndicatorCodes.Code(instance.Type);

        return instance.Parameters switch
        {
            SmaParameters p => new IndicatorRecord(instance.Id, type, p.Length, null, null, null, null, Code(p.Source), styles, instance.Hidden),
            EmaParameters p => new IndicatorRecord(instance.Id, type, p.Length, null, null, null, null, Code(p.Source), styles, instance.Hidden),
            BollingerBandsParameters p => new IndicatorRecord(instance.Id, type, p.Length, p.Deviation, null, null, null, Code(p.Source), styles, instance.Hidden),
            RsiParameters p => new IndicatorRecord(instance.Id, type, p.Length, null, null, null, null, Code(p.Source), styles, instance.Hidden),
            MacdParameters p => new IndicatorRecord(instance.Id, type, null, null, p.Fast, p.Slow, p.Signal, Code(p.Source), styles, instance.Hidden),
            AtrParameters p => new IndicatorRecord(instance.Id, type, p.Length, null, null, null, null, null, styles, instance.Hidden),
            _ => throw new ArgumentOutOfRangeException(nameof(instance))
        };
    }

    public static IndicatorTemplateRecord ToRecord(IndicatorTemplate template)
    {
        return new IndicatorTemplateRecord(template.Id, template.Name, template.Indicators.Select(ToRecord).ToList());
    }

    /// <summary>
    /// The indicators of a request, checked by <see cref="IndicatorRules"/>; the problems are added by
    /// "<paramref name="field"/>[i].name".
    /// </summary>
    /// <returns>The indicators, or null when there are problems.</returns>
    public static IReadOnlyList<IndicatorInstance>? ToInstances(
        IReadOnlyList<IndicatorRecord>? records, string field, bool emptyAllowed, Dictionary<string, string[]> problems)
    {
        if (records == null)
        {
            problems[field] = new[] { "The indicators are required." };
            return null;
        }

        var instances = new List<IndicatorInstance>(records.Count);
        var before = problems.Count;
        for (var i = 0; i < records.Count; i++)
        {
            var instance = ToInstance(records[i], $"{field}[{i}]", problems);
            if (instance != null)
            {
                instances.Add(instance);
            }
        }

        if (problems.Count > before)
        {
            return null;
        }

        // The rules name the fields of the Domain; the problems name those of the request.
        var rules = new Dictionary<string, string[]>();
        IndicatorRules.CheckList(rules, field, instances, emptyAllowed);
        foreach (var (name, messages) in rules)
        {
            problems[name.Replace(".parameters.source", ".price").Replace(".parameters.", ".")] = messages;
        }

        return rules.Count > 0 ? null : instances;
    }

    private static IndicatorInstance? ToInstance(IndicatorRecord? record, string field, Dictionary<string, string[]> problems)
    {
        if (record == null)
        {
            problems[field] = new[] { "An indicator is missing." };
            return null;
        }

        if (!IndicatorCodes.TryParse(record.Type, out IndicatorType type))
        {
            problems[field + ".type"] = new[] { "The type is sma, ema, bb, rsi, macd or atr." };
            return null;
        }

        var before = problems.Count;
        var price = PriceSource.Close;
        var hasPrice = type != IndicatorType.Atr;
        if (hasPrice && !IndicatorCodes.TryParse(record.Price, out price))
        {
            problems[field + ".price"] = new[] { "The price is open, high, low, close, hl2, hlc3 or ohlc4." };
        }

        Expect(problems, field, "length", record.Length != null, type is not IndicatorType.Macd);
        Expect(problems, field, "deviation", record.Deviation != null, type == IndicatorType.BollingerBands);
        Expect(problems, field, "fast", record.Fast != null, type == IndicatorType.Macd);
        Expect(problems, field, "slow", record.Slow != null, type == IndicatorType.Macd);
        Expect(problems, field, "signal", record.Signal != null, type == IndicatorType.Macd);
        if (!hasPrice && record.Price != null)
        {
            problems[field + ".price"] = new[] { "atr has no price." };
        }

        if (problems.Count > before)
        {
            return null;
        }

        IndicatorParameters parameters = type switch
        {
            IndicatorType.Sma => new SmaParameters(record.Length!.Value, price),
            IndicatorType.Ema => new EmaParameters(record.Length!.Value, price),
            IndicatorType.BollingerBands => new BollingerBandsParameters(record.Length!.Value, record.Deviation!.Value, price),
            IndicatorType.Rsi => new RsiParameters(record.Length!.Value, price),
            IndicatorType.Macd => new MacdParameters(record.Fast!.Value, record.Slow!.Value, record.Signal!.Value, price),
            _ => new AtrParameters(record.Length!.Value)
        };

        var styles = (record.Styles ?? Array.Empty<IndicatorStyleRecord>())
            .Select(style => style == null ? null! : new IndicatorLineStyle(style.Line, style.Color, style.Width))
            .ToList();

        return new IndicatorInstance(record.Id, parameters, styles, record.Hidden);
    }

    /// <summary>A parameter the type has must be given, one it does not have must be left out.</summary>
    private static void Expect(Dictionary<string, string[]> problems, string field, string name, bool given, bool belongs)
    {
        if (belongs && !given)
        {
            problems[field + "." + name] = new[] { "Required." };
        }
        else if (!belongs && given)
        {
            problems[field + "." + name] = new[] { "This indicator has no " + name + "." };
        }
    }

    private static string Code(PriceSource source) => IndicatorCodes.Code(source);
}
