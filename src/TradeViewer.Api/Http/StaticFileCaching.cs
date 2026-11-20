using Microsoft.Net.Http.Headers;

namespace TradeViewer.Api.Http;

/// <summary>
/// The files of the client (the page, the chart module, the Blazor files) are answered with
/// <c>Cache-Control: no-cache</c>: the browser keeps them but asks with their ETag on every load, so after an
/// update it never runs an old chart module against a new client (an unchanged file costs a 304).
/// </summary>
public static class StaticFileCaching
{
    public const string Value = "no-cache";

    public static IApplicationBuilder UseClientFiles(this IApplicationBuilder app)
    {
        var options = new StaticFileOptions { OnPrepareResponse = context => SetNoCache(context.Context.Response) };
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles(options);
        return app;
    }

    /// <summary>The options of the fallback to index.html, which is served apart from the static files.</summary>
    public static StaticFileOptions FallbackOptions()
    {
        return new StaticFileOptions { OnPrepareResponse = context => SetNoCache(context.Context.Response) };
    }

    private static void SetNoCache(HttpResponse response)
    {
        response.Headers[HeaderNames.CacheControl] = Value;
    }
}
