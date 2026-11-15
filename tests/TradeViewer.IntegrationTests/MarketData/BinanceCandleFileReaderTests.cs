using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.MarketData;
using TradeViewer.Infrastructure.MarketData.Files;

namespace TradeViewer.IntegrationTests.MarketData;

/// <summary>MD1 and MD6: candles are read unchanged; a file that breaks a rule is refused with file name and line.</summary>
public class BinanceCandleFileReaderTests : IDisposable
{
    private const string Row1 = "1762214400000,100.5,101.25,99.75,100.75,12.5,1762215299999,1257.1,340,6.1,612.9,0";
    private const string Row2 = "1762215300000,100.75,102,100.5,101.5,8.25,1762216199999,830.2,210,4.0,404.1,0";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tradeviewer-tests", Guid.NewGuid().ToString("N"));
    private readonly BinanceCandleFileReader _reader = new BinanceCandleFileReader();

    public BinanceCandleFileReaderTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, true);
    }

    [Fact]
    public void A_file_in_milliseconds_is_read_unchanged()
    {
        var candles = ReadFixture("15m", "BTCUSDT-15m-2024-12-31.csv");

        Assert.Equal(96, candles.Count);
        Assert.Equal(new Candle(1735603200, 92792.05, 92907.03, 92706.02, 92831.10, 148.07186), candles[0]);
        Assert.Equal(1735603200 + 95 * 900, candles[95].Time);
    }

    [Fact]
    public void A_file_in_microseconds_is_read_unchanged()
    {
        var candles = ReadFixture("15m", "BTCUSDT-15m-2025-01-01.csv");

        Assert.Equal(96, candles.Count);
        Assert.Equal(new Candle(1735689600, 93576.00, 93702.15, 93489.03, 93656.18, 175.85673), candles[0]);
        Assert.All(candles.Zip(candles.Skip(1)), pair => Assert.Equal(900, pair.Second.Time - pair.First.Time));
    }

    [Fact]
    public void A_weekly_file_holds_the_weeks_that_open_in_its_month()
    {
        var candles = ReadFixture("1w", "BTCUSDT-1w-2026-10.csv");

        Assert.Equal(4, candles.Count);
        Assert.All(candles, candle => Assert.Equal(DayOfWeek.Monday, DateTimeOffset.FromUnixTimeSeconds(candle.Time).DayOfWeek));
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), candles[0].Time);
    }

    [Fact]
    public void One_file_can_mix_milliseconds_and_microseconds()
    {
        var result = Read("BTCUSDT-15m-2025-11-04.csv", Row1, Row2.Replace("1762215300000,", "1762215300000000,"));

        Assert.Equal(new long[] { 1762214400, 1762215300 }, result.Value.Select(candle => candle.Time));
    }

    [Fact]
    public void A_header_row_and_empty_lines_at_the_end_are_skipped()
    {
        var result = Read("BTCUSDT-15m-2025-11-04.csv", "open_time,open,high,low,close,volume,close_time", Row1, Row2, "", "");

        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public void An_empty_file_has_no_candles()
    {
        Assert.Empty(Read("BTCUSDT-15m-2025-11-04.csv").Value);
    }

    [Fact]
    public void Only_the_first_six_columns_are_needed()
    {
        var result = Read("BTCUSDT-15m-2025-11-04.csv", "1762214400000,100.5,101.25,99.75,100.75,12.5");

        Assert.Equal(new Candle(1762214400, 100.5, 101.25, 99.75, 100.75, 12.5), Assert.Single(result.Value));
    }

    [Theory]
    [InlineData("1762214400000,100.5,101.25,99.75,100.75", 2, "fewer than 6 columns")]
    [InlineData("x1762214400000,100.5,101.25,99.75,100.75,12.5", 2, "open time is not a number")]
    [InlineData("-1762214400000,100.5,101.25,99.75,100.75,12.5", 2, "open time is not a number")]
    [InlineData("1762214400000,100.5,abc,99.75,100.75,12.5", 2, "column 2 is not a number")]
    [InlineData("1762214400000,100.5,101.25,99.75,100.75,", 2, "column 5 is not a number")]
    [InlineData("1762214400000,100.5,101.25,99.75,1e2,12.5", 2, "column 4 is not a number")]
    [InlineData("1762214400000,100.5,\"101,25\",99.75,100.75,12.5", 2, "column 2 is not a number")]
    [InlineData("1762214400500,100.5,101.25,99.75,100.75,12.5", 2, "not a whole second")]
    [InlineData("1762214400000,100.5,99,99.75,100.75,12.5", 2, "high is below")]
    [InlineData("1762214400000,100.5,101.25,99.75,100.75,-1", 2, "column 5 is not a number")]
    [InlineData("1762215300000,100.75,102,100.5,101.5,8.25", 2, "not after the row before")]
    [InlineData("1762300800000,100.5,101.25,99.75,100.75,12.5", 2, "outside the period")]
    public void A_broken_row_refuses_the_whole_file_with_its_name_and_line(string brokenRow, int line, string problem)
    {
        var result = Read("BTCUSDT-15m-2025-11-04.csv", Row2, brokenRow, Row1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.InvalidData, result.Error.Type);
        Assert.StartsWith("BTCUSDT-15m-2025-11-04.csv, line " + line + ": ", result.Error.Message);
        Assert.Contains(problem, result.Error.Message);
    }

    [Fact]
    public void An_empty_line_between_rows_is_invalid()
    {
        var result = Read("BTCUSDT-15m-2025-11-04.csv", Row1, "", Row2);

        Assert.True(result.IsFailure);
        Assert.StartsWith("BTCUSDT-15m-2025-11-04.csv, line 2: ", result.Error.Message);
    }

    [Fact]
    public void A_header_row_is_accepted_in_the_first_line_only()
    {
        var result = Read("BTCUSDT-15m-2025-11-04.csv", Row1, "open_time,open,high,low,close,volume");

        Assert.True(result.IsFailure);
        Assert.Contains("line 2", result.Error.Message);
    }

    [Fact]
    public void A_file_that_is_gone_is_not_found()
    {
        CandleFileRules.TryParseName("BTCUSDT-15m-2025-11-04.csv", out var file);

        var result = _reader.Read(Path.Combine(_folder, "missing", file!.Name), file);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    private IReadOnlyList<Candle> ReadFixture(string timeframe, string name)
    {
        CandleFileRules.TryParseName(name, out var file);
        var result = _reader.Read(Fixtures.CandleFile("binance", "BTCUSDT", timeframe, name), file!);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }

    private Result<IReadOnlyList<Candle>> Read(string name, params string[] lines)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, string.Join("\n", lines));
        CandleFileRules.TryParseName(name, out var file);
        return _reader.Read(path, file!);
    }
}
