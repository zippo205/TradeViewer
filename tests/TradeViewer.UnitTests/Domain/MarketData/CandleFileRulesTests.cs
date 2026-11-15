using TradeViewer.Domain.MarketData;

namespace TradeViewer.UnitTests.Domain.MarketData;

public class CandleFileRulesTests
{
    private static readonly CandleFile Day = Parse("BTCUSDT-15m-2026-11-04.csv");

    [Theory]
    [InlineData("BTCUSDT-15m-2026-11-04.csv", "BTCUSDT", Timeframe.M15, "2026-11-04", "2026-11-05")]
    [InlineData("BTCUSDT-1d-2024-02-29.csv", "BTCUSDT", Timeframe.D1, "2024-02-29", "2024-03-01")]
    [InlineData("BTCUSDT-4h-2025-12-31.csv", "BTCUSDT", Timeframe.H4, "2025-12-31", "2026-01-01")]
    [InlineData("BTCUSDT-1w-2026-11.csv", "BTCUSDT", Timeframe.W1, "2026-11-01", "2026-12-01")]
    [InlineData("BTCUSDT-1w-2026-12.csv", "BTCUSDT", Timeframe.W1, "2026-12-01", "2027-01-01")]
    [InlineData("TSLA-1w-2026.csv", "TSLA", Timeframe.W1, "2026-01-01", "2027-01-01")]
    [InlineData("BRK-B-1d-2026-10.csv", "BRK-B", Timeframe.D1, "2026-10-01", "2026-11-01")]
    public void The_name_tells_symbol_timeframe_and_period(string name, string symbol, Timeframe timeframe, string start, string end)
    {
        Assert.True(CandleFileRules.TryParseName(name, out var file));

        Assert.Equal(name, file!.Name);
        Assert.Equal(symbol, file.Symbol);
        Assert.Equal(timeframe, file.Timeframe);
        Assert.Equal(Seconds(start), file.PeriodStart);
        Assert.Equal(Seconds(end), file.PeriodEnd);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("manifest.json")]
    [InlineData("BTCUSDT-15m-2026-11-04.csv.part")]
    [InlineData("BTCUSDT-15m-2026-11-04.CSV")]
    [InlineData("BTCUSDT-5m-2026-11-04.csv")]
    [InlineData("BTCUSDT-15m-2026-13-04.csv")]
    [InlineData("BTCUSDT-15m-2026-02-30.csv")]
    [InlineData("BTCUSDT-15m-2026-11-4.csv")]
    [InlineData("-15m-2026-11-04.csv")]
    [InlineData("BTCUSDT-15m-2026-11-04.csv\n")]
    public void Any_other_name_is_not_a_candle_file(string? name)
    {
        Assert.False(CandleFileRules.TryParseName(name, out var file));
        Assert.Null(file);
    }

    [Fact]
    public void A_sound_row_is_valid()
    {
        Assert.Null(CandleFileRules.CheckRow(Row(Day.PeriodStart), null, Day));
        Assert.Null(CandleFileRules.CheckRow(Row(Day.PeriodEnd - 900), Day.PeriodEnd - 1800, Day));
        Assert.Null(CandleFileRules.CheckRow(new Candle(Day.PeriodStart, 5, 5, 5, 5, 0), null, Day)); // a flat candle without volume
    }

    [Theory]
    [InlineData(0, 12, 9, 11, 1, "price")]
    [InlineData(10, 12, -1, 11, 1, "price")]
    [InlineData(10, double.NaN, 9, 11, 1, "price")]
    [InlineData(10, double.PositiveInfinity, 9, 11, 1, "price")]
    [InlineData(10, 12, 10.5, 11, 1, "low is above")]
    [InlineData(10, 12, 9, 8, 1, "low is above")]
    [InlineData(10, 9.5, 9, 9.2, 1, "high is below")]
    [InlineData(10, 12, 9, 13, 1, "high is below")]
    [InlineData(10, 12, 9, 11, -0.1, "volume")]
    [InlineData(10, 12, 9, 11, double.NaN, "volume")]
    public void A_row_with_impossible_values_is_invalid(double open, double high, double low, double close, double volume, string expected)
    {
        var error = CandleFileRules.CheckRow(new Candle(Day.PeriodStart, open, high, low, close, volume), null, Day);

        Assert.Contains(expected, error);
    }

    [Fact]
    public void Open_times_must_increase()
    {
        Assert.Contains("not after", CandleFileRules.CheckRow(Row(Day.PeriodStart + 900), Day.PeriodStart + 900, Day));
        Assert.Contains("not after", CandleFileRules.CheckRow(Row(Day.PeriodStart), Day.PeriodStart + 900, Day));
    }

    [Fact]
    public void A_row_outside_the_period_of_its_file_is_invalid()
    {
        Assert.Contains("outside the period", CandleFileRules.CheckRow(Row(Day.PeriodStart - 900), null, Day));
        Assert.Contains("outside the period", CandleFileRules.CheckRow(Row(Day.PeriodEnd), null, Day));
    }

    private static Candle Row(long time)
    {
        return new Candle(time, 10, 12, 9, 11, 100);
    }

    private static CandleFile Parse(string name)
    {
        CandleFileRules.TryParseName(name, out var file);
        return file!;
    }

    private static long Seconds(string date)
    {
        return new DateTimeOffset(DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture), TimeSpan.Zero).ToUnixTimeSeconds();
    }
}
