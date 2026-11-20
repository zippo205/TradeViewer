using TradeViewer.Domain.Scenarios;

namespace TradeViewer.UnitTests.Domain.Scenarios;

/// <summary>SC14: the id of an expert scenario by EWTS's rule, and the strict check that keeps a request inside expert/.</summary>
public class ExpertScenarioIdTests
{
    [Theory]
    [InlineData("btcusdt-1d-2024-03-X1")]
    [InlineData("virtualusdt-4h-2026-10-X1")]
    [InlineData("1000pepeusdt-4h-2026-12-X12")]
    public void Ids_of_the_format_are_valid(string id)
    {
        Assert.True(ExpertScenarioId.IsValid(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BTCUSDT-1d-2024-03-X1")]
    [InlineData("btcusdt-1h-2024-03-X1")]
    [InlineData("btcusdt-1d-2024-13-X1")]
    [InlineData("btcusdt-1d-2024-03-X0")]
    [InlineData("btcusdt-1d-2024-03-x1")]
    [InlineData("btcusdt-1d-2024-03-T1")]
    [InlineData("btcusdt-1d-2024-03-X1.json")]
    [InlineData("../btcusdt-1d-2024-03-X1")]
    [InlineData("btc/usdt-1d-2024-03-X1")]
    public void Anything_else_is_not_an_id(string? id)
    {
        Assert.False(ExpertScenarioId.IsValid(id));
    }

    [Fact]
    public void The_prefix_has_the_lower_case_symbol_and_the_month_of_wave_2_end_in_UTC()
    {
        var wave2End = new DateTimeOffset(2024, 3, 31, 23, 0, 0, TimeSpan.FromHours(-2)); // 2024-04-01 01:00 UTC

        Assert.Equal("btcusdt-4h-2024-04-X", ExpertScenarioId.Prefix("BTCUSDT", "4h", wave2End));
    }

    [Fact]
    public void The_next_id_is_one_more_than_the_highest_number_of_the_prefix()
    {
        var taken = new[] { "solusdt-1d-2025-06-X1", "solusdt-1d-2025-06-X10", "solusdt-1d-2025-06-X2", "solusdt-1d-2025-07-X30", "solusdt-4h-2025-06-X40", "notes" };

        Assert.Equal("solusdt-1d-2025-06-X11", ExpertScenarioId.Next("solusdt-1d-2025-06-X", taken));
        Assert.Equal("solusdt-1d-2025-05-X1", ExpertScenarioId.Next("solusdt-1d-2025-05-X", taken));
    }
}
