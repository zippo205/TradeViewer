using TradeViewer.Application.Abstractions.Drawings;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Drawings.Commands;
using TradeViewer.Domain.Drawings;

namespace TradeViewer.Application.Features.Drawings.Validators;

public sealed class SaveFibonacciPresetCommandValidator
{
    /// <param name="preset">The preset to store when the record is valid.</param>
    /// <returns>Null when the preset can be stored, otherwise the error naming the fields.</returns>
    public Error? Validate(SaveFibonacciPresetCommand command, out FibonacciPreset? preset)
    {
        preset = null;
        var problems = new Dictionary<string, string[]>();
        var record = command.Preset;

        if (record == null)
        {
            problems["preset"] = new[] { "The preset is required." };
        }
        else if (record.Id != command.Id)
        {
            problems["id"] = new[] { "The id of the preset must be the id in the address." };
        }
        else if (!DrawingCodes.TryParse(record.Kind, out FibonacciKind kind))
        {
            problems["kind"] = new[] { "The kind is retracement or extension." };
        }
        else
        {
            var levels = DrawingMapping.ToDomain(record.Levels)?.OrderBy(level => level.Ratio).ToList() ?? new List<FibonacciLevel>();
            var candidate = new FibonacciPreset(record.Id, record.Name?.Trim() ?? "", kind, levels);
            foreach (var problem in FibonacciPresets.Check(candidate))
            {
                problems[problem.Key] = problem.Value;
            }

            if (problems.Count == 0)
            {
                preset = candidate;
            }
        }

        return problems.Count == 0 ? null : Error.Validation("fibonacciPreset.invalid", "The preset is not valid.", problems);
    }
}
