using System.Net.Http.Json;
using TradeViewer.Contracts.Watchlists;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/watchlists: the five lists with their rows, and the order of a list (AS1, AS6).</summary>
public sealed class WatchlistsApi
{
    private readonly HttpClient _http;

    public WatchlistsApi(HttpClient http)
    {
        _http = http;
    }

    public Task<ApiResult<List<WatchlistResponse>>> GetAsync()
    {
        return ApiCalls.SendAsync<List<WatchlistResponse>>(() => _http.GetAsync("api/watchlists"));
    }

    /// <returns>Null when the order was saved, otherwise the message to show.</returns>
    public Task<string?> ReorderAsync(string assetClass, IReadOnlyList<WatchlistEntry> order)
    {
        var address = "api/watchlists/" + Uri.EscapeDataString(assetClass) + "/order";
        return ApiCalls.SendAsync(() => _http.PutAsJsonAsync(address, new ReorderWatchlistRequest(order)));
    }
}
