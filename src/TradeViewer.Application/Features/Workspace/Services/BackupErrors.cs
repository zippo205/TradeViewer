using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;

namespace TradeViewer.Application.Features.Workspace.Services;

/// <summary>The expected failures of the backup handlers.</summary>
public static class BackupErrors
{
    public static Error NotFound(string message)
    {
        return Error.NotFound("backup.notFound", message);
    }

    /// <summary>Maps the outcome of a restore: every failure left the workspace untouched.</summary>
    public static Result<Unit> ToResult(RestoreResult result)
    {
        switch (result.Status)
        {
            case RestoreStatus.Restored:
                return Result.Success();
            case RestoreStatus.NotFound:
                return NotFound(result.Message);
            case RestoreStatus.TooLarge:
                return Error.Validation("backup.tooLarge", result.Message);
            default:
                return Error.InvalidData("backup.invalid", result.Message);
        }
    }
}
