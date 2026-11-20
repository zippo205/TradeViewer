using System.Text.RegularExpressions;
using TradeViewer.Application.Abstractions.Drawings;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Drawings;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Domain.Drawings;
using TradeViewer.Domain.Scenarios;

namespace TradeViewer.Application.Features.Scenarios.Validators;

/// <summary>
/// What a scenario must be so it can be written and read again: an asset, a context timeframe of 1d or 4h, and
/// drawings that follow the rules of their tool (SC20, as on the chart page). This refuses only what could not be
/// read again; everything else is a form problem (SC15) that never prevents a save.
/// </summary>
public static partial class ExpertScenarioValidator
{
    public static Error? Validate(ExpertScenarioRecord? scenario)
    {
        var problems = new Dictionary<string, string[]>();
        if (scenario == null)
        {
            problems["scenario"] = new[] { "The scenario is required." };
            return Invalid(problems);
        }

        if (string.IsNullOrWhiteSpace(scenario.Source))
        {
            problems["source"] = new[] { "The source is required." };
        }

        if (scenario.Symbol == null || !SymbolPattern().IsMatch(scenario.Symbol))
        {
            problems["symbol"] = new[] { "The symbol is the name of the asset's folder (letters and digits)." };
        }

        if (!ExpertScenarioId.ContextTimeframes.Contains(scenario.ContextTimeframe))
        {
            problems["contextTimeframe"] = new[] { "The context timeframe is 1d or 4h." };
        }

        CheckDrawings(scenario, problems);
        return problems.Count == 0 ? null : Invalid(problems);
    }

    private static void CheckDrawings(ExpertScenarioRecord scenario, Dictionary<string, string[]> problems)
    {
        var drawings = scenario.Drawings ?? Array.Empty<ScenarioDrawingRecord>();
        var ids = new HashSet<Guid>();
        for (var i = 0; i < drawings.Count; i++)
        {
            var field = "drawings[" + i + "]";
            var drawing = drawings[i];
            if (drawing == null)
            {
                problems[field] = new[] { "A drawing is missing." };
                continue;
            }

            if (!ScenarioLevels.TryParse(drawing.Level, out _))
            {
                problems[field + ".level"] = new[] { "The level is context, setup or trigger." };
            }

            if (!ids.Add(drawing.Id))
            {
                problems[field + ".id"] = new[] { "Two drawings have the same id." };
            }

            var record = new DrawingRecord(
                drawing.Id, scenario.Source ?? "", scenario.Symbol ?? "", drawing.Tool, drawing.Version, drawing.Locked, drawing.Anchors, drawing.Style,
                drawing.Text, drawing.Levels, drawing.Wave, drawing.PointLabels, drawing.LineHidden);
            var drawingProblems = new Dictionary<string, string[]>();
            var mapped = DrawingMapping.ToDomain(record, drawingProblems);
            if (mapped != null)
            {
                foreach (var problem in DrawingRules.Check(mapped))
                {
                    drawingProblems[problem.Key] = problem.Value;
                }
            }

            foreach (var problem in drawingProblems)
            {
                problems[field + "." + problem.Key] = problem.Value;
            }
        }
    }

    private static Error Invalid(Dictionary<string, string[]> problems)
    {
        return Error.Validation("scenario.invalid", "The scenario cannot be written as it is.", problems);
    }

    [GeneratedRegex("^[A-Za-z0-9]{1,40}$")]
    private static partial Regex SymbolPattern();
}
