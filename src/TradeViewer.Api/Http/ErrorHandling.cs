namespace TradeViewer.Api.Http;

/// <summary>Every error answer is problem details (RFC 9457): exceptions, and empty 4xx and 5xx answers such as an unknown route.</summary>
public static class ErrorHandling
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        return services;
    }

    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        // An unexpected exception is logged and answered with 500 and no internal details.
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
