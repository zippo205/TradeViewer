using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TradeViewer.Infrastructure.Configuration;
using TradeViewer.Infrastructure.Storage;

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

        // Hosted services start in registration order: the folders first.
        services.AddHostedService<StorageInitializer>();

        return services;
    }
}
