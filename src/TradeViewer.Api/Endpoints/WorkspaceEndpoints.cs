using Microsoft.AspNetCore.Mvc;
using TradeViewer.Api.Http;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Workspace.Commands;
using TradeViewer.Application.Features.Workspace.Queries;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/settings", async (
            IQueryHandler<GetSettingsQuery, SettingsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetSettingsQuery(), cancellationToken);
            return result.ToHttpResult();
        });

        routes.MapPut("/api/settings", async (
            SettingsResponse request,
            ICommandHandler<UpdateSettingsCommand, SettingsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new UpdateSettingsCommand(request.DefaultHistory), cancellationToken);
            return result.ToHttpResult();
        });

        var backups = routes.MapGroup("/api/workspace/backups");

        backups.MapGet("", async (
            IQueryHandler<GetBackupsQuery, IReadOnlyList<BackupResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetBackupsQuery(), cancellationToken);
            return result.ToHttpResult();
        });

        backups.MapPost("", async (
            ICommandHandler<CreateBackupCommand, BackupResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new CreateBackupCommand(), cancellationToken);
            return result.ToHttpResult();
        });

        backups.MapGet("/{name}", async (
            string name,
            IQueryHandler<GetBackupFileQuery, FileContent> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetBackupFileQuery(name), cancellationToken);
            return result.ToFileResult();
        });

        backups.MapPost("/{name}/restore", async (
            string name,
            ICommandHandler<RestoreBackupCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new RestoreBackupCommand(name), cancellationToken);
            return result.ToNoContentResult();
        });

        // The body is the backup file itself (application/octet-stream). The size limit of the server is off here:
        // the restore itself stops reading at the upload limit and answers with the reason.
        backups.MapPost("/restore", async (
            HttpRequest request,
            ICommandHandler<RestoreUploadedBackupCommand, Unit> handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new RestoreUploadedBackupCommand(request.Body), cancellationToken);
            return result.ToNoContentResult();
        }).WithMetadata(new DisableRequestSizeLimitAttribute());

        return routes;
    }
}
