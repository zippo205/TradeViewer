using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Commands;
using TradeViewer.Application.Features.Workspace.Services;

namespace TradeViewer.Application.Features.Workspace.Handlers;

public sealed class RestoreUploadedBackupCommandHandler : ICommandHandler<RestoreUploadedBackupCommand, Unit>
{
    private readonly IWorkspaceBackups _backups;

    public RestoreUploadedBackupCommandHandler(IWorkspaceBackups backups)
    {
        _backups = backups;
    }

    public async Task<Result<Unit>> HandleAsync(RestoreUploadedBackupCommand command, CancellationToken cancellationToken)
    {
        return BackupErrors.ToResult(await _backups.RestoreAsync(command.Content, cancellationToken));
    }
}
