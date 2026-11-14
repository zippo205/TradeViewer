using Microsoft.Extensions.DependencyInjection;

namespace TradeViewer.Application;

public static class DependencyInjection
{
    /// <summary>Registers the handlers and services of the Application layer, one line each.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
