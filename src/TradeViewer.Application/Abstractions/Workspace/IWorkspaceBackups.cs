using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>Backups of the workspace (WS3): made daily by the application, on request by the user; listed, downloaded and restored.</summary>
public interface IWorkspaceBackups
{
    /// <summary>The backups, newest first.</summary>
    Task<IReadOnlyList<BackupResponse>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Makes a backup now ("Back up now").</summary>
    Task<BackupResponse> CreateAsync(CancellationToken cancellationToken);

    /// <summary>Opens a backup for download; null when there is no backup of that name. The caller disposes the content.</summary>
    Task<FileContent?> OpenAsync(string name, CancellationToken cancellationToken);

    /// <summary>Replaces the workspace by the backup of that name, while the application runs.</summary>
    Task<RestoreResult> RestoreAsync(string name, CancellationToken cancellationToken);

    /// <summary>Replaces the workspace by an uploaded backup file.</summary>
    Task<RestoreResult> RestoreAsync(Stream content, CancellationToken cancellationToken);
}
