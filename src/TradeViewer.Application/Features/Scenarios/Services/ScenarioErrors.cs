using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Scenarios.Services;

/// <summary>The expected failures of the scenario editor.</summary>
public static class ScenarioErrors
{
    public static Error Off()
    {
        return Error.NotFound("scenarios.off", "No scenario folder is configured (Scenarios:RootPath).");
    }

    public static Error ReadOnly()
    {
        return Error.Conflict("scenarios.readOnly", "The scenario folder is not writable; the editor can only show the scenarios.");
    }

    public static Error NotFound(string id)
    {
        return Error.NotFound("scenario.notFound", "There is no expert scenario " + id + ".");
    }

    public static Error Unreadable(string id, string error)
    {
        return Error.InvalidData("scenario.unreadable", "The file of " + id + " cannot be read: " + error);
    }

    public static Error Field(string field, string message)
    {
        return Error.Validation("scenario.invalid", message, new Dictionary<string, string[]> { [field] = new[] { message } });
    }
}
