using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Queries;
using TradeViewer.Application.Features.Workspace.Services;

namespace TradeViewer.Application.Features.Workspace.Handlers;

public sealed class GetBackupFileQueryHandler : IQueryHandler<GetBackupFileQuery, FileContent>
{
    private readonly IWorkspaceBackups _backups;

    public GetBackupFileQueryHandler(IWorkspaceBackups backups)
    {
        _backups = backups;
    }

    public async Task<Result<FileContent>> HandleAsync(GetBackupFileQuery query, CancellationToken cancellationToken)
    {
        var file = await _backups.OpenAsync(query.Name, cancellationToken);
        if (file == null)
        {
            return BackupErrors.NotFound("There is no backup named " + query.Name + ".");
        }

        return Result.Success(file);
    }
}
