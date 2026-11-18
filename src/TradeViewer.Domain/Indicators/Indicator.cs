using TradeViewer.Domain.MarketData;

namespace TradeViewer.Domain.Indicators;

/// <summary>
/// Calculates an indicator from candles (IN1), as TradingView's built-in scripts do, seeds included, so a
/// value equals TradingView's once both start from the same candle:
/// <code>
/// SMA    mean of the last n prices
/// EMA    alpha = 2 / (n + 1), seeded with the SMA of the first n prices
/// RMA    alpha = 1 / n, seeded with the SMA of the first n values (Wilder; used by RSI and ATR)
/// BB     basis SMA(n), bands basis +- k * population standard deviation of the last n prices
/// RSI    100 - 100 / (1 + RMA(gain, n) / RMA(loss, n)); the first candle has no change
/// MACD   EMA(fast) - EMA(slow); signal EMA(signal) of it; histogram MACD - signal
/// ATR    RMA(true range, n); the first true range is high - low
/// </code>
/// A value is null until the indicator has the candles it needs. Each line has one value per candle.
/// </summary>
public static class Indicator
{
    public static IReadOnlyList<IndicatorLine> Calculate(IReadOnlyList<Candle> candles, IndicatorParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(candles);

        return parameters switch
        {
            SmaParameters p => new[] { Line(IndicatorLines.Value, Sma(Prices(candles, p.Source), p.Length)) },
            EmaParameters p => new[] { Line(IndicatorLines.Value, Ema(Prices(candles, p.Source), p.Length)) },
            BollingerBandsParameters p => BollingerBands(Prices(candles, p.Source), p),
            RsiParameters p => new[] { Line(IndicatorLines.Value, Rsi(Prices(candles, p.Source), p.Length)) },
            MacdParameters p => Macd(Prices(candles, p.Source), p),
            AtrParameters p => new[] { Line(IndicatorLines.Value, Rma(TrueRange(candles), p.Length)) },
            null => throw new ArgumentNullException(nameof(parameters)),
            _ => throw new ArgumentOutOfRangeException(nameof(parameters))
        };
    }

    /// <summary>The price of each candle the indicator is calculated from.</summary>
    public static double?[] Prices(IReadOnlyList<Candle> candles, PriceSource source)
    {
        var prices = new double?[candles.Count];
        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            prices[i] = source switch
            {
                PriceSource.Open => c.Open,
                PriceSource.High => c.High,
                PriceSource.Low => c.Low,
                PriceSource.Close => c.Close,
                PriceSource.Hl2 => (c.High + c.Low) / 2,
                PriceSource.Hlc3 => (c.High + c.Low + c.Close) / 3,
                PriceSource.Ohlc4 => (c.Open + c.High + c.Low + c.Close) / 4,
                _ => throw new ArgumentOutOfRangeException(nameof(source))
            };
        }

        return prices;
    }

    /// <summary>The mean of the last <paramref name="length"/> values; null while one of them is null.</summary>
    public static double?[] Sma(double?[] values, int length)
    {
        var result = new double?[values.Length];
        for (var i = length - 1; i < values.Length; i++)
        {
            result[i] = Mean(values, i, length);
        }

        return result;
    }

    /// <summary>The exponential moving average, seeded with the SMA of the first <paramref name="length"/> values.</summary>
    public static double?[] Ema(double?[] values, int length)
    {
        return Smoothed(values, length, 2.0 / (length + 1));
    }

    /// <summary>Wilder's moving average (RMA), seeded with the SMA of the first <paramref name="length"/> values.</summary>
    public static double?[] Rma(double?[] values, int length)
    {
        return Smoothed(values, length, 1.0 / length);
    }

    private static IReadOnlyList<IndicatorLine> BollingerBands(double?[] prices, BollingerBandsParameters p)
    {
        var basis = Sma(prices, p.Length);
        var upper = new double?[prices.Length];
        var lower = new double?[prices.Length];

        for (var i = 0; i < prices.Length; i++)
        {
            if (basis[i] is not double mean)
            {
                continue;
            }

            var sumOfSquares = 0.0;
            for (var j = i - p.Length + 1; j <= i; j++)
            {
                var difference = prices[j]!.Value - mean;
                sumOfSquares += difference * difference;
            }

            var deviation = Math.Sqrt(sumOfSquares / p.Length); // population: divided by n, as TradingView does
            upper[i] = mean + p.Deviation * deviation;
            lower[i] = mean - p.Deviation * deviation;
        }

        return new[] { Line(IndicatorLines.Basis, basis), Line(IndicatorLines.Upper, upper), Line(IndicatorLines.Lower, lower) };
    }

    private static double?[] Rsi(double?[] prices, int length)
    {
        var gains = new double?[prices.Length];
        var losses = new double?[prices.Length];
        for (var i = 1; i < prices.Length; i++)
        {
            var change = prices[i]!.Value - prices[i - 1]!.Value;
            gains[i] = Math.Max(change, 0);
            losses[i] = Math.Max(-change, 0);
        }

        var averageGain = Rma(gains, length);
        var averageLoss = Rma(losses, length);
        var rsi = new double?[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            if (averageGain[i] is double gain && averageLoss[i] is double loss)
            {
                rsi[i] = loss == 0 ? 100 : gain == 0 ? 0 : 100 - 100 / (1 + gain / loss);
            }
        }

        return rsi;
    }

    private static IReadOnlyList<IndicatorLine> Macd(double?[] prices, MacdParameters p)
    {
        var fast = Ema(prices, p.Fast);
        var slow = Ema(prices, p.Slow);
        var macd = new double?[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            macd[i] = fast[i] - slow[i]; // null while either is null
        }

        var signal = Ema(macd, p.Signal);
        var histogram = new double?[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            histogram[i] = macd[i] - signal[i];
        }

        return new[] { Line(IndicatorLines.Macd, macd), Line(IndicatorLines.Signal, signal), Line(IndicatorLines.Histogram, histogram) };
    }

    private static double?[] TrueRange(IReadOnlyList<Candle> candles)
    {
        var ranges = new double?[candles.Count];
        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            ranges[i] = i == 0
                ? c.High - c.Low
                : Math.Max(c.High - c.Low, Math.Max(Math.Abs(c.High - candles[i - 1].Close), Math.Abs(c.Low - candles[i - 1].Close)));
        }

        return ranges;
    }

    /// <summary>
    /// An exponential smoothing as TradingView writes it: the first value is the SMA of the first
    /// <paramref name="length"/> values that are not null; from then on value = alpha * x + (1 - alpha) * previous.
    /// </summary>
    private static double?[] Smoothed(double?[] values, int length, double alpha)
    {
        var result = new double?[values.Length];
        double? previous = null;

        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is not double value)
            {
                continue;
            }

            previous = previous is double last
                ? alpha * value + (1 - alpha) * last
                : i >= length - 1 ? Mean(values, i, length) : null;
            result[i] = previous;
        }

        return result;
    }

    /// <summary>The mean of the <paramref name="length"/> values ending at <paramref name="end"/>; null when one is null.</summary>
    private static double? Mean(double?[] values, int end, int length)
    {
        var sum = 0.0;
        for (var j = end - length + 1; j <= end; j++)
        {
            if (values[j] is not double value)
            {
                return null;
            }

            sum += value;
        }

        return sum / length;
    }

    private static IndicatorLine Line(string name, double?[] values) => new(name, values);
}
