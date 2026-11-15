using Microsoft.Extensions.DependencyInjection;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Workspace.Commands;
using TradeViewer.Application.Features.Workspace.Handlers;
using TradeViewer.Application.Features.Workspace.Queries;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application;

public static class DependencyInjection
{
    /// <summary>Registers the handlers and services of the Application layer, one line each.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Workspace
        services.AddScoped<IQueryHandler<GetBackupsQuery, IReadOnlyList<BackupResponse>>, GetBackupsQueryHandler>();
        services.AddScoped<IQueryHandler<GetBackupFileQuery, FileContent>, GetBackupFileQueryHandler>();
        services.AddScoped<ICommandHandler<CreateBackupCommand, BackupResponse>, CreateBackupCommandHandler>();
        services.AddScoped<ICommandHandler<RestoreBackupCommand, Unit>, RestoreBackupCommandHandler>();
        services.AddScoped<ICommandHandler<RestoreUploadedBackupCommand, Unit>, RestoreUploadedBackupCommandHandler>();

        return services;
    }
}
