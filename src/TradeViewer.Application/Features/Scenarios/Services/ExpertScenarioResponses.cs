using System.Globalization;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Domain.MarketData;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Services;

/// <summary>
/// The answer of every request of the editor: the scenario with its shapes (SC8), its form problems (SC15), the
/// timeframes of its levels (SC11) and the state of its asset (SC6).
/// </summary>
public sealed class ExpertScenarioResponses
{
    private readonly IAssetRepository _assets;
    private readonly ScenarioTurnings _turnings;

    public ExpertScenarioResponses(IAssetRepository assets, ScenarioTurnings turnings)
    {
        _assets = assets;
        _turnings = turnings;
    }

    /// <param name="shown">The timeframe of the chart: the points of higher levels are drawn on its candles (SC8, decision 0013); null: each on its own.</param>
    public async Task<ExpertScenarioResponse> BuildAsync(ExpertScenarioRecord scenario, string? shown, CancellationToken cancellationToken)
    {
        var problems = ScenarioFormRules.Check(ExpertScenarioReadings.Of(scenario))
            .Select(problem => new ScenarioProblemRecord(problem.Field, problem.Message))
            .ToList();
        problems.AddRange(await _turnings.MisfitsAsync(scenario, cancellationToken));
        var view = await _turnings.ForViewAsync(scenario, Timeframes.TryParse(shown, out var timeframe) ? timeframe : null, cancellationToken);
        var context = ContextOf(scenario);
        var levels = context == null
            ? new List<ScenarioLevelRecord>()
            : ScenarioLevels.All.Select(level => new ScenarioLevelRecord(ScenarioLevels.Code(level), Timeframes.Code(ScenarioLevels.TimeframeOf(context.Value, level)))).ToList();

        return new ExpertScenarioResponse(scenario, ExpertScenarioShapes.Of(view), problems, levels, await AssetStateAsync(scenario, context, cancellationToken));
    }

    /// <summary>The context timeframe of the scenario when it is one (1d or 4h).</summary>
    public static Timeframe? ContextOf(ExpertScenarioRecord scenario)
    {
        return Timeframes.TryParse(scenario.ContextTimeframe, out var context) && ScenarioLevels.IsContext(context) ? context : null;
    }

    /// <summary>
    /// SC6: whether the asset is in TradeViewer, and which history starts of the three levels lie after the start of
    /// the time span. The asset is never added or changed here; the page asks the user first.
    /// </summary>
    private async Task<ScenarioAssetRecord> AssetStateAsync(ExpertScenarioRecord scenario, Timeframe? context, CancellationToken cancellationToken)
    {
        var asset = await _assets.GetAsync(scenario.Source, scenario.Symbol, cancellationToken);
        var needed = new Dictionary<string, string?>();
        if (asset == null)
        {
            return new ScenarioAssetRecord(false, needed);
        }

        if (context != null && scenario.From is { } from)
        {
            var firstDay = DateOnly.FromDateTime(from.UtcDateTime);
            foreach (var level in ScenarioLevels.All)
            {
                var timeframe = ScenarioLevels.TimeframeOf(context.Value, level);
                if (asset.History.Has(timeframe) && asset.History.StartOf(timeframe) is { } start && start > firstDay)
                {
                    needed[Timeframes.Code(timeframe)] = firstDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
            }
        }

        return new ScenarioAssetRecord(true, needed);
    }
}
