using System.Globalization;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Scenarios.Queries;
using TradeViewer.Application.Features.Scenarios.Services;
using TradeViewer.Application.Features.Scenarios.Validators;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Handlers;

/// <summary>
/// The server snaps a point (SC10, decision 0012): it reads the candle of the point's timeframe that contains the
/// time where the user let it go and applies the snap rule of the point. Nothing is written; the editor saves the
/// answer like any other change.
/// </summary>
public sealed class SnapScenarioPointQueryHandler : IQueryHandler<SnapScenarioPointQuery, ExpertScenarioResponse>
{
    private readonly ICandleSeriesReader _candles;
    private readonly ExpertScenarioResponses _responses;

    public SnapScenarioPointQueryHandler(ICandleSeriesReader candles, ExpertScenarioResponses responses)
    {
        _candles = candles;
        _responses = responses;
    }

    public async Task<Result<ExpertScenarioResponse>> HandleAsync(SnapScenarioPointQuery query, CancellationToken cancellationToken)
    {
        var request = query.Request;
        var invalid = ExpertScenarioValidator.Validate(request?.Scenario);
        if (invalid != null)
        {
            return invalid;
        }

        var name = ScenarioPointEditor.NameOf(request!.Point);
        if (name == null)
        {
            return ScenarioErrors.Field("point", "Unknown point \"" + request.Point + "\".");
        }

        if (request.Kind != null && !ScenarioCodes.ExitKinds.Contains(request.Kind))
        {
            return ScenarioErrors.Field("kind", "An exit is at a target or at the stop.");
        }

        if (!double.IsFinite(request.Price))
        {
            return ScenarioErrors.Field("price", "The price is a number.");
        }

        var scenario = request.Scenario;
        var timeframe = ScenarioPointEditor.SnapTimeframe(scenario, name.Value)!.Value;
        var time = request.Time.ToUnixTimeSeconds();
        var page = await _candles.ReadAsync(
            scenario.Source, scenario.Symbol, Timeframes.Code(timeframe), time + Timeframes.Seconds(timeframe), limit: 2, warmUp: 0, cancellationToken);
        if (page.IsFailure)
        {
            return page.Error;
        }

        var candle = ScenarioSnap.ContainingCandle(page.Value.Candles, timeframe, time);
        if (candle == null)
        {
            return Error.InvalidData(
                "scenario.noCandle",
                "There is no " + Timeframes.Code(timeframe) + " candle of " + scenario.Symbol + " at " + request.Time.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC.");
        }

        var snapped = ScenarioSnap.Snap(name.Value, candle.Value, request.Price);
        var placed = ScenarioPointEditor.Place(scenario, name.Value, request.Index, request.Kind, snapped, timeframe);
        return Result.Success(await _responses.BuildAsync(placed, cancellationToken));
    }
}
