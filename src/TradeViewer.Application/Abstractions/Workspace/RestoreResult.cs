namespace TradeViewer.Application.Abstractions.Workspace;

public enum RestoreStatus
{
    /// <summary>The workspace is now the content of the backup.</summary>
    Restored,

    /// <summary>There is no backup of that name. Nothing was changed.</summary>
    NotFound,

    /// <summary>The file is damaged, not a workspace, or written by a newer version. Nothing was changed.</summary>
    Invalid,

    /// <summary>The uploaded file is larger than the upload limit (256 MB). Nothing was changed.</summary>
    TooLarge
}

/// <summary>The outcome of a restore. Anything but <see cref="RestoreStatus.Restored"/> left the workspace untouched.</summary>
public sealed record RestoreResult(RestoreStatus Status, string Message);
