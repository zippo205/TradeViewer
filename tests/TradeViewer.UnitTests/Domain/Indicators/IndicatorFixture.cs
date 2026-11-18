using System.Globalization;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Domain.Indicators;

/// <summary>tests/fixtures/indicators: BINANCE BTCUSDT 1d, 2017-08-17 to 2025-01-01, the reference of TradingView's values.</summary>
public static class IndicatorFixture
{
    public static IReadOnlyList<Candle> Daily { get; } = Read("BTCUSDT-1d.csv");

    public static long Time(string day) => DateTimeOffset.Parse(day + "T00:00:00Z", CultureInfo.InvariantCulture).ToUnixTimeSeconds();

    public static string Day(long time) => DateTimeOffset.FromUnixTimeSeconds(time).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<Candle> Read(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "indicators", name);
        return File.ReadLines(path)
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line =>
            {
                var f = line.Split(',');
                double D(int i) => double.Parse(f[i], CultureInfo.InvariantCulture);
                return new Candle(long.Parse(f[0], CultureInfo.InvariantCulture), D(1), D(2), D(3), D(4), 0);
            })
            .ToList();
    }
}
