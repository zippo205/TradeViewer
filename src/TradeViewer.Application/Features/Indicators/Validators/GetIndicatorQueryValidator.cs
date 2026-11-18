using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Indicators.Queries;
using TradeViewer.Domain.Indicators;

namespace TradeViewer.Application.Features.Indicators.Validators;

public sealed class GetIndicatorQueryValidator
{
    /// <param name="parameters">The parameters of the indicator when the query is valid.</param>
    /// <returns>Null when the indicator can be calculated, otherwise the error naming the fields.</returns>
    public Error? Validate(GetIndicatorQuery query, out IndicatorParameters? parameters)
    {
        parameters = null;
        var problems = new Dictionary<string, string[]>();

        if (!IndicatorCodes.TryParse(query.Type, out IndicatorType type))
        {
            problems["type"] = new[] { "The type is sma, ema, bb, rsi, macd or atr." };
            return Invalid(problems);
        }

        var price = PriceSource.Close;
        if (query.Price != null && !IndicatorCodes.TryParse(query.Price, out price))
        {
            problems["price"] = new[] { "The price is open, high, low, close, hl2, hlc3 or ohlc4." };
        }

        // A parameter the type does not have is refused: it would be silently ignored otherwise.
        var used = Used(type);
        foreach (var (field, given) in Given(query))
        {
            if (given && !used.Contains(field))
            {
                problems[field] = new[] { $"{IndicatorCodes.Code(type)} has no parameter {field}." };
            }
        }

        if (problems.Count > 0)
        {
            return Invalid(problems);
        }

        var built = Build(type, query, price);
        foreach (var (field, messages) in IndicatorRules.Check(built))
        {
            problems[field == "source" ? "price" : field] = messages;
        }

        if (problems.Count > 0)
        {
            return Invalid(problems);
        }

        parameters = built;
        return null;
    }

    private static IndicatorParameters Build(IndicatorType type, GetIndicatorQuery query, PriceSource price)
    {
        return IndicatorParameters.Default(type) switch
        {
            SmaParameters d => new SmaParameters(query.Length ?? d.Length, price),
            EmaParameters d => new EmaParameters(query.Length ?? d.Length, price),
            BollingerBandsParameters d => new BollingerBandsParameters(query.Length ?? d.Length, query.Deviation ?? d.Deviation, price),
            RsiParameters d => new RsiParameters(query.Length ?? d.Length, price),
            MacdParameters d => new MacdParameters(query.Fast ?? d.Fast, query.Slow ?? d.Slow, query.Signal ?? d.Signal, price),
            AtrParameters d => new AtrParameters(query.Length ?? d.Length),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static IReadOnlySet<string> Used(IndicatorType type) => type switch
    {
        IndicatorType.Sma or IndicatorType.Ema or IndicatorType.Rsi => new HashSet<string> { "length", "price" },
        IndicatorType.BollingerBands => new HashSet<string> { "length", "deviation", "price" },
        IndicatorType.Macd => new HashSet<string> { "fast", "slow", "signal", "price" },
        IndicatorType.Atr => new HashSet<string> { "length" },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static IEnumerable<(string Field, bool Given)> Given(GetIndicatorQuery query)
    {
        yield return ("length", query.Length != null);
        yield return ("deviation", query.Deviation != null);
        yield return ("fast", query.Fast != null);
        yield return ("slow", query.Slow != null);
        yield return ("signal", query.Signal != null);
        yield return ("price", query.Price != null);
    }

    private static Error Invalid(IReadOnlyDictionary<string, string[]> problems)
    {
        return Error.Validation("indicator.invalid", "The indicator parameters are not valid.", problems);
    }
}
