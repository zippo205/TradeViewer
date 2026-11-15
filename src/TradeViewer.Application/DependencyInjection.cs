using Microsoft.Extensions.DependencyInjection;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Features.Assets.Commands;
using TradeViewer.Application.Features.Assets.Handlers;
using TradeViewer.Application.Features.Assets.Queries;
using TradeViewer.Application.Features.Assets.Services;
using TradeViewer.Application.Features.MarketData.Handlers;
using TradeViewer.Application.Features.MarketData.Queries;
using TradeViewer.Application.Features.Workspace.Commands;
using TradeViewer.Application.Features.Workspace.Handlers;
using TradeViewer.Application.Features.Workspace.Queries;
using TradeViewer.Contracts.Assets;
using TradeViewer.Contracts.MarketData;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application;

public static class DependencyInjection
{
    /// <summary>Registers the handlers and services of the Application layer, one line each.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Assets
        services.AddScoped<AssetStatusReader>();
        services.AddScoped<IQueryHandler<GetSymbolsQuery, IReadOnlyList<SymbolResponse>>, GetSymbolsQueryHandler>();
        services.AddScoped<IQueryHandler<GetAssetsQuery, IReadOnlyList<AssetResponse>>, GetAssetsQueryHandler>();
        services.AddScoped<IQueryHandler<GetAssetQuery, AssetResponse>, GetAssetQueryHandler>();
        services.AddScoped<ICommandHandler<AddAssetCommand, AssetResponse>, AddAssetCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeAssetHistoryCommand, AssetResponse>, ChangeAssetHistoryCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveAssetCommand, Unit>, RemoveAssetCommandHandler>();
        services.AddScoped<ICommandHandler<RescanAssetCommand, Unit>, RescanAssetCommandHandler>();
        services.AddScoped<ICommandHandler<RebuildAssetCommand, Unit>, RebuildAssetCommandHandler>();

        // MarketData
        services.AddScoped<IQueryHandler<GetCandlesQuery, CandlesResponse>, GetCandlesQueryHandler>();

        // Workspace
        services.AddScoped<IQueryHandler<GetSettingsQuery, SettingsResponse>, GetSettingsQueryHandler>();
        services.AddScoped<ICommandHandler<UpdateSettingsCommand, SettingsResponse>, UpdateSettingsCommandHandler>();
        services.AddScoped<IQueryHandler<GetBackupsQuery, IReadOnlyList<BackupResponse>>, GetBackupsQueryHandler>();
        services.AddScoped<IQueryHandler<GetBackupFileQuery, FileContent>, GetBackupFileQueryHandler>();
        services.AddScoped<ICommandHandler<CreateBackupCommand, BackupResponse>, CreateBackupCommandHandler>();
        services.AddScoped<ICommandHandler<RestoreBackupCommand, Unit>, RestoreBackupCommandHandler>();
        services.AddScoped<ICommandHandler<RestoreUploadedBackupCommand, Unit>, RestoreUploadedBackupCommandHandler>();

        return services;
    }
}
