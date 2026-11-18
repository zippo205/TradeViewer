using System.Text.Json;
using TradeViewer.Domain.Indicators;

namespace TradeViewer.Infrastructure.Workspace;

/// <summary>
/// Indicators as the chart settings and the templates store them (architecture chapter 11): a JSON list, each
/// with its type code as discriminator and only the parameters of its type. The stored shape is its own, so the
/// Domain and the API can change without touching stored data. Codes are those of <see cref="IndicatorCodes"/>.
/// </summary>
internal static class IndicatorPayload
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static List<StoredIndicator> Write(IReadOnlyList<IndicatorInstance> indicators)
    {
        return indicators.Select(Write).ToList();
    }

    /// <summary>
    /// The indicators that can be read. One that cannot (a damaged entry, an unknown code) is left out rather than
    /// failing the whole list; a newer build that adds a kind raises the payload version of its table instead.
    /// </summary>
    public static IReadOnlyList<IndicatorInstance> Read(IReadOnlyList<StoredIndicator>? stored)
    {
        if (stored == null || stored.Count == 0)
        {
            return Array.Empty<IndicatorInstance>();
        }

        var indicators = new List<IndicatorInstance>(stored.Count);
        foreach (var entry in stored)
        {
            var indicator = entry == null ? null : Read(entry);
            if (indicator != null && IndicatorRules.Check(indicator).Count == 0)
            {
                indicators.Add(indicator);
            }
        }

        return indicators;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    /// <returns>The value, or default when the text is not JSON of that shape.</returns>
    public static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static StoredIndicator Write(IndicatorInstance indicator)
    {
        var styles = indicator.Styles.Select(style => new StoredStyle(style.Line, style.Color, style.Width)).ToList();
        var stored = new StoredIndicator(indicator.Id, IndicatorCodes.Code(indicator.Type), null, null, null, null, null, null, styles, indicator.Hidden ? true : null);

        return indicator.Parameters switch
        {
            SmaParameters p => stored with { Length = p.Length, Price = IndicatorCodes.Code(p.Source) },
            EmaParameters p => stored with { Length = p.Length, Price = IndicatorCodes.Code(p.Source) },
            BollingerBandsParameters p => stored with { Length = p.Length, Deviation = p.Deviation, Price = IndicatorCodes.Code(p.Source) },
            RsiParameters p => stored with { Length = p.Length, Price = IndicatorCodes.Code(p.Source) },
            MacdParameters p => stored with { Fast = p.Fast, Slow = p.Slow, Signal = p.Signal, Price = IndicatorCodes.Code(p.Source) },
            AtrParameters p => stored with { Length = p.Length },
            _ => throw new ArgumentOutOfRangeException(nameof(indicator))
        };
    }

    private static IndicatorInstance? Read(StoredIndicator entry)
    {
        if (!IndicatorCodes.TryParse(entry.Type, out IndicatorType type) || entry.Styles == null)
        {
            return null;
        }

        var price = PriceSource.Close;
        if (type != IndicatorType.Atr && !IndicatorCodes.TryParse(entry.Price, out price))
        {
            return null;
        }

        IndicatorParameters? parameters = type switch
        {
            IndicatorType.Sma when entry.Length is int n => new SmaParameters(n, price),
            IndicatorType.Ema when entry.Length is int n => new EmaParameters(n, price),
            IndicatorType.BollingerBands when entry.Length is int n && entry.Deviation is double k => new BollingerBandsParameters(n, k, price),
            IndicatorType.Rsi when entry.Length is int n => new RsiParameters(n, price),
            IndicatorType.Macd when entry.Fast is int f && entry.Slow is int s && entry.Signal is int g => new MacdParameters(f, s, g, price),
            IndicatorType.Atr when entry.Length is int n => new AtrParameters(n),
            _ => null
        };

        if (parameters == null || entry.Styles.Any(style => style == null))
        {
            return null;
        }

        var styles = entry.Styles.Select(style => new IndicatorLineStyle(style.Line, style.Color, style.Width)).ToList();
        return new IndicatorInstance(entry.Id, parameters, styles, entry.Hidden == true);
    }

    internal sealed record StoredIndicator(
        Guid Id,
        string Type,
        int? Length,
        double? Deviation,
        int? Fast,
        int? Slow,
        int? Signal,
        string? Price,
        List<StoredStyle> Styles,
        bool? Hidden);

    internal sealed record StoredStyle(string Line, string Color, int Width);
}
