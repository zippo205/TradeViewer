using TradeViewer.Application.Abstractions.Drawings;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Drawings.Commands;
using TradeViewer.Domain.Drawings;

namespace TradeViewer.Application.Features.Drawings.Validators;

public sealed class SaveDrawingCommandValidator
{
    /// <summary>Checks the record against the rules of its tool (DR1-DR5): what could not be read again is refused, not stored.</summary>
    /// <param name="drawing">The drawing to store when the record is valid.</param>
    /// <returns>Null when the drawing can be stored, otherwise the error naming the fields.</returns>
    public Error? Validate(SaveDrawingCommand command, out Drawing? drawing)
    {
        drawing = null;
        var problems = new Dictionary<string, string[]>();

        if (command.Drawing == null)
        {
            problems["drawing"] = new[] { "The drawing is required." };
            return Invalid(problems);
        }

        if (command.Drawing.Id != command.Id)
        {
            problems["id"] = new[] { "The id of the drawing must be the id in the address." };
            return Invalid(problems);
        }

        var mapped = DrawingMapping.ToDomain(command.Drawing, problems);
        if (mapped == null)
        {
            return Invalid(problems);
        }

        foreach (var problem in DrawingRules.Check(mapped))
        {
            problems[problem.Key] = problem.Value;
        }

        if (problems.Count > 0)
        {
            return Invalid(problems);
        }

        drawing = mapped;
        return null;
    }

    private static Error Invalid(Dictionary<string, string[]> problems)
    {
        return Error.Validation("drawing.invalid", "The drawing is not valid.", problems);
    }
}
