using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Infrastructure.Configuration;
using TradeViewer.Infrastructure.MarketData.Files;
using TradeViewer.Infrastructure.MarketData.Store;
using TradeViewer.Infrastructure.Storage;
using TradeViewer.Infrastructure.Workspace;
using TradeViewer.Infrastructure.Workspace.Backups;

namespace TradeViewer.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Binds and validates the configuration, resolves the folders and registers the start-up checks.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        services.AddOptions<MarketDataOptions>().Bind(configuration.GetSection(MarketDataOptions.SectionName)).ValidateOnStart();
        services.AddOptions<EwtsOptions>().Bind(configuration.GetSection(EwtsOptions.SectionName)).ValidateOnStart();
        services.AddOptions<ScenariosOptions>().Bind(configuration.GetSection(ScenariosOptions.SectionName)).ValidateOnStart();
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName)).ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MarketDataOptions>, MarketDataOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EwtsOptions>, EwtsOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>());

        services.AddSingleton(provider => StorageLocations.Resolve(
            provider.GetRequiredService<IOptions<MarketDataOptions>>().Value,
            provider.GetRequiredService<IOptions<StorageOptions>>().Value,
            provider.GetRequiredService<IOptions<ScenariosOptions>>().Value,
            contentRoot));

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton(provider => new WorkspaceDatabase(provider.GetRequiredService<StorageLocations>().WorkspaceDatabasePath));
        services.AddSingleton(WorkspaceSchema.FromEmbeddedScripts());
        services.AddSingleton(provider => new BackupStore(
            provider.GetRequiredService<StorageLocations>().BackupsPath,
            provider.GetRequiredService<IOptions<StorageOptions>>().Value.Backup.KeepCount,
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<WorkspaceBackups>();
        services.AddSingleton<IWorkspaceBackups>(provider => provider.GetRequiredService<WorkspaceBackups>());
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<WorkspaceInitializer>();

        services.AddSingleton(MarketDataSchema.FromEmbeddedScripts());
        services.AddSingleton(provider => new MarketDataDatabase(
            provider.GetRequiredService<StorageLocations>().MarketDataDatabasePath,
            provider.GetRequiredService<MarketDataSchema>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<MarketDataDatabase>>()));
        services.AddSingleton<ICandleStore, SqliteCandleStore>();
        services.AddSingleton<ICandleFileReader, BinanceCandleFileReader>();

        // Hosted services start in registration order: the folders first, then the workspace, then what uses it.
        services.AddHostedService<StorageInitializer>();
        services.AddHostedService(provider => provider.GetRequiredService<WorkspaceInitializer>());
        services.AddHostedService<MarketDataInitializer>();
        services.AddHostedService<DailyBackupService>();

        return services;
    }
}
