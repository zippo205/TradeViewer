using System.Text.Json;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.ChartSettings.Commands;
using TradeViewer.Application.Features.ChartSettings.Handlers;
using TradeViewer.Application.Features.ChartSettings.Queries;
using TradeViewer.Contracts.Charts;
using TradeViewer.Contracts.Indicators;
using TradeViewer.Domain.Assets;
using TradeViewer.Domain.Indicators;
using TradeViewer.Domain.MarketData;
using TradeViewer.UnitTests.Application.Assets;
using TradeViewer.UnitTests.Application.Drawings;

namespace TradeViewer.UnitTests.Application.Indicators;

/// <summary>IN1, IN2, CP2 at the level of the handlers: indicators in the chart settings, and templates.</summary>
public class IndicatorSettingsHandlersTests
{
    private readonly FakeAssetRepository _assets = new FakeAssetRepository();
    private readonly FakeChartSettingsRepository _chartSettings = new FakeChartSettingsRepository();
    private readonly FakeTemplateRepository _templates = new FakeTemplateRepository();
    private readonly FakeNotifier _notifier = new FakeNotifier();

    public IndicatorSettingsHandlersTests()
    {
        var info = new SymbolInfo("binance", "BTCUSDT", "BTCUSDT", AssetClass.Crypto, 0.01, Session.ContinuousUtc, Timeframes.All);
        _assets.Assets.Add(new Asset(info, new AssetHistory(new Dictionary<Timeframe, DateOnly?>()), 1, true));
        _chartSettings.Assets.Add(("binance", "BTCUSDT"));
    }

    [Fact]
    public async Task Indicators_are_read_back_as_they_were_sent_and_the_other_windows_hear_of_it()
    {
        var indicators = AllKinds();

        var saved = await SaveAsync(new ChartSettingsResponse(false, indicators), "window-1");
        var read = await GetAsync();

        Assert.True(saved.IsSuccess);
        Assert.Equal(Json(indicators), Json(read.Indicators));
        Assert.Equal(Json(indicators), Json(saved.Value.Indicators));
        Assert.Equal(new[] { "ChartSettingsChanged binance/BTCUSDT window-1" }, _notifier.Events);
    }

    [Fact]
    public async Task A_new_asset_has_no_indicators_and_the_answer_always_names_them()
    {
        var read = await GetAsync();

        Assert.NotNull(read.Indicators);
        Assert.Empty(read.Indicators);
    }

    [Fact]
    public async Task Indicators_left_out_stay_as_saved_and_an_empty_list_removes_them()
    {
        await SaveAsync(new ChartSettingsResponse(false, AllKinds()));

        await SaveAsync(new ChartSettingsResponse(true)); // as a client that knows only "drawings hidden" sends it
        var kept = await GetAsync();
        await SaveAsync(new ChartSettingsResponse(true, Array.Empty<IndicatorRecord>()));
        var removed = await GetAsync();

        Assert.True(kept.DrawingsHidden);
        Assert.Equal(6, kept.Indicators!.Count);
        Assert.Empty(removed.Indicators!);
    }

    [Theory]
    [InlineData("type", "wma", "indicators[0].type")]
    [InlineData("length", null, "indicators[0].length")]
    [InlineData("length", 0, "indicators[0].length")]
    [InlineData("price", "Close", "indicators[0].price")]
    [InlineData("fast", 12, "indicators[0].fast")]
    [InlineData("color", "blue", "indicators[0].styles.value.color")]
    public async Task A_wrong_indicator_is_refused_by_the_field_the_client_sent(string change, object? value, string field)
    {
        var sma = Sma();
        sma = change switch
        {
            "type" => sma with { Type = (string)value! },
            "length" => sma with { Length = (int?)value },
            "price" => sma with { Price = (string)value! },
            "fast" => sma with { Fast = (int?)value },
            _ => sma with { Styles = new[] { new IndicatorStyleRecord("value", (string)value!, 1) } }
        };

        var result = await SaveAsync(new ChartSettingsResponse(false, new[] { sma }));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(field, result.Error.Fields.Keys);
        Assert.Null(await _chartSettings.GetAsync("binance", "BTCUSDT", CancellationToken.None)); // nothing saved
        Assert.Empty(_notifier.Events);
    }

    [Fact]
    public async Task Atr_has_no_price_macd_has_no_length_and_ids_are_distinct()
    {
        var atr = Record("atr", length: 14) with { Price = "close" };
        var macd = Record("macd", fast: 12, slow: 26, signal: 9, price: "close") with { Length = 9 };
        var first = Sma();

        var problems = (await SaveAsync(new ChartSettingsResponse(false, new[] { atr, macd }))).Error.Fields.Keys;
        var twice = (await SaveAsync(new ChartSettingsResponse(false, new[] { first, first }))).Error.Fields.Keys;

        Assert.Contains("indicators[0].price", problems);
        Assert.Contains("indicators[1].length", problems);
        Assert.Contains("indicators[1].id", twice);
    }

    [Fact]
    public async Task Templates_are_listed_by_name_and_two_cannot_share_a_name()
    {
        var trend = new IndicatorTemplateRecord(Guid.NewGuid(), " Trend ", new[] { Sma() });
        var oscillators = new IndicatorTemplateRecord(Guid.NewGuid(), "Oscillators", new[] { Record("rsi", length: 14, price: "close") });

        var saved = await SaveTemplateAsync(trend);
        await SaveTemplateAsync(oscillators);
        var taken = await SaveTemplateAsync(new IndicatorTemplateRecord(Guid.NewGuid(), "TREND", new[] { Sma() }));
        var renamed = await SaveTemplateAsync(trend with { Name = "Trend and more" });
        var all = (await new GetIndicatorTemplatesQueryHandler(_templates).HandleAsync(new GetIndicatorTemplatesQuery(), CancellationToken.None)).Value;

        Assert.Equal("Trend", saved.Value.Name); // trimmed
        Assert.Equal(ErrorType.Conflict, taken.Error.Type);
        Assert.True(renamed.IsSuccess);
        Assert.Equal(new[] { "Oscillators", "Trend and more" }, all.Select(t => t.Name));
    }

    [Fact]
    public async Task A_template_needs_a_name_indicators_and_the_id_of_its_address()
    {
        var id = Guid.NewGuid();

        var noName = await SaveTemplateAsync(new IndicatorTemplateRecord(id, "  ", new[] { Sma() }));
        var empty = await SaveTemplateAsync(new IndicatorTemplateRecord(id, "Empty", Array.Empty<IndicatorRecord>()));
        var otherId = await new SaveIndicatorTemplateCommandHandler(_templates)
            .HandleAsync(new SaveIndicatorTemplateCommand(Guid.NewGuid(), new IndicatorTemplateRecord(id, "Mine", new[] { Sma() })), CancellationToken.None);
        var none = await new SaveIndicatorTemplateCommandHandler(_templates).HandleAsync(new SaveIndicatorTemplateCommand(id, null), CancellationToken.None);

        Assert.Contains("name", noName.Error.Fields.Keys);
        Assert.Contains("indicators", empty.Error.Fields.Keys);
        Assert.Contains("id", otherId.Error.Fields.Keys);
        Assert.Contains("template", none.Error.Fields.Keys);
        Assert.Empty(_templates.Templates);
    }

    [Fact]
    public async Task Deleting_a_template_that_is_not_there_is_not_found()
    {
        var trend = new IndicatorTemplateRecord(Guid.NewGuid(), "Trend", new[] { Sma() });
        await SaveTemplateAsync(trend);
        var delete = new DeleteIndicatorTemplateCommandHandler(_templates);

        Assert.True((await delete.HandleAsync(new DeleteIndicatorTemplateCommand(trend.Id), CancellationToken.None)).IsSuccess);
        Assert.Equal(ErrorType.NotFound, (await delete.HandleAsync(new DeleteIndicatorTemplateCommand(trend.Id), CancellationToken.None)).Error.Type);
    }

    /// <summary>The six kinds as a client sends them: TradingView's defaults, each with its styles.</summary>
    private static IndicatorRecord[] AllKinds() => new[]
    {
        Sma(),
        Record("ema", length: 21, price: "ohlc4") with { Hidden = true },
        Record("bb", length: 20, deviation: 2.5, price: "hl2"),
        Record("rsi", length: 14, price: "close"),
        Record("macd", fast: 12, slow: 26, signal: 9, price: "close"),
        Record("atr", length: 14)
    };

    private static IndicatorRecord Sma() => Record("sma", length: 9, price: "close");

    private static IndicatorRecord Record(string type, int? length = null, double? deviation = null, int? fast = null, int? slow = null, int? signal = null, string? price = null)
    {
        Assert.True(IndicatorCodes.TryParse(type, out IndicatorType parsed));
        var styles = IndicatorStyles.Default(parsed).Select(s => new IndicatorStyleRecord(s.Line, s.Color, s.Width)).ToList();
        return new IndicatorRecord(Guid.NewGuid(), type, length, deviation, fast, slow, signal, price, styles, false);
    }

    private Task<Result<ChartSettingsResponse>> SaveAsync(ChartSettingsResponse settings, string? window = null)
    {
        return new SaveChartSettingsCommandHandler(_chartSettings, _notifier)
            .HandleAsync(new SaveChartSettingsCommand("binance", "BTCUSDT", settings, window), CancellationToken.None);
    }

    private async Task<ChartSettingsResponse> GetAsync()
    {
        var result = await new GetChartSettingsQueryHandler(_assets, _chartSettings).HandleAsync(new GetChartSettingsQuery("binance", "BTCUSDT"), CancellationToken.None);
        return result.Value;
    }

    private Task<Result<IndicatorTemplateRecord>> SaveTemplateAsync(IndicatorTemplateRecord template)
    {
        return new SaveIndicatorTemplateCommandHandler(_templates).HandleAsync(new SaveIndicatorTemplateCommand(template.Id, template), CancellationToken.None);
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    private sealed class FakeTemplateRepository : IIndicatorTemplateRepository
    {
        public List<IndicatorTemplate> Templates { get; } = new List<IndicatorTemplate>();

        public Task<IReadOnlyList<IndicatorTemplate>> GetAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<IndicatorTemplate>>(Templates.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList());
        }

        public Task SaveAsync(IndicatorTemplate template, CancellationToken cancellationToken)
        {
            Templates.RemoveAll(t => t.Id == template.Id);
            Templates.Add(template);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Templates.RemoveAll(t => t.Id == id) > 0);
        }
    }
}
