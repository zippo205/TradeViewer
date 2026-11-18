using TradeViewer.Contracts.Downloader;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/downloader: the Downloader as TradeViewer sees it (DL1-DL4). It only reads.</summary>
public sealed class DownloaderApi
{
    private readonly HttpClient _http;

    public DownloaderApi(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Always answered while TradeViewer runs; a Downloader that does not answer is a state (red), not an error.</summary>
    public Task<ApiResult<DownloaderStatusResponse>> GetStatusAsync()
    {
        return ApiCalls.SendAsync<DownloaderStatusResponse>(() => _http.GetAsync("api/downloader/status"));
    }

    /// <summary>Fails when the Downloader does not answer or is switched off: there is nothing to show without it.</summary>
    public Task<ApiResult<DownloaderSubscriptionsResponse>> GetSubscriptionsAsync()
    {
        return ApiCalls.SendAsync<DownloaderSubscriptionsResponse>(() => _http.GetAsync("api/downloader/subscriptions"));
    }

    /// <summary>Coverage, jobs and gaps of an asset; the sync's last status (Live false) when the Downloader is away.</summary>
    public Task<ApiResult<DownloaderAssetResponse>> GetAssetAsync(string source, string symbol)
    {
        return ApiCalls.SendAsync<DownloaderAssetResponse>(() => _http.GetAsync("api/downloader/assets/" + Uri.EscapeDataString(source) + "/" + Uri.EscapeDataString(symbol)));
    }
}
