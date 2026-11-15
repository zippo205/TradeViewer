using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Commands;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Handlers;

public sealed class CreateBackupCommandHandler : ICommandHandler<CreateBackupCommand, BackupResponse>
{
    private readonly IWorkspaceBackups _backups;

    public CreateBackupCommandHandler(IWorkspaceBackups backups)
    {
        _backups = backups;
    }

    public async Task<Result<BackupResponse>> HandleAsync(CreateBackupCommand command, CancellationToken cancellationToken)
    {
        return Result.Success(await _backups.CreateAsync(cancellationToken));
    }
}
