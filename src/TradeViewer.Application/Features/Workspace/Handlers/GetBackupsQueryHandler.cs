using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Queries;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Handlers;

public sealed class GetBackupsQueryHandler : IQueryHandler<GetBackupsQuery, IReadOnlyList<BackupResponse>>
{
    private readonly IWorkspaceBackups _backups;

    public GetBackupsQueryHandler(IWorkspaceBackups backups)
    {
        _backups = backups;
    }

    public async Task<Result<IReadOnlyList<BackupResponse>>> HandleAsync(GetBackupsQuery query, CancellationToken cancellationToken)
    {
        return Result.Success(await _backups.ListAsync(cancellationToken));
    }
}
