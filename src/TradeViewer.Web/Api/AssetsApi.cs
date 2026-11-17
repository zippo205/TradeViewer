using System.Net.Http.Json;
using TradeViewer.Contracts.Assets;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/symbols and /api/assets.</summary>
public sealed class AssetsApi
{
    private readonly HttpClient _http;

    public AssetsApi(HttpClient http)
    {
        _http = http;
    }

    /// <summary>The symbols of a source matching the text: the Downloader's catalog, or the fixed list without it (AS2).</summary>
    public Task<ApiResult<SymbolSearchResponse>> GetSymbolsAsync(string source, string? text)
    {
        var address = "api/symbols/" + Uri.EscapeDataString(source);
        if (!string.IsNullOrWhiteSpace(text))
        {
            address += "?q=" + Uri.EscapeDataString(text.Trim());
        }

        return ApiCalls.SendAsync<SymbolSearchResponse>(() => _http.GetAsync(address));
    }

    /// <summary>A symbol picked, looked up at the Downloader by its ticker: tick size, session, earliest dates.</summary>
    public Task<ApiResult<SymbolDetailsResponse>> GetSymbolDetailsAsync(string source, string ticker)
    {
        return ApiCalls.SendAsync<SymbolDetailsResponse>(() => _http.GetAsync("api/symbols/" + Uri.EscapeDataString(source) + "/" + Uri.EscapeDataString(ticker)));
    }

    public Task<ApiResult<List<AssetResponse>>> GetAssetsAsync()
    {
        return ApiCalls.SendAsync<List<AssetResponse>>(() => _http.GetAsync("api/assets"));
    }

    public Task<ApiResult<AssetResponse>> AddAsync(string source, string symbol, string? sourceSymbol = null)
    {
        return ApiCalls.SendAsync<AssetResponse>(() => _http.PostAsJsonAsync("api/assets", new AddAssetRequest(source, symbol, sourceSymbol)));
    }

    public Task<ApiResult<AssetResponse>> ChangeHistoryAsync(string source, string symbol, IReadOnlyDictionary<string, string?> historyFrom)
    {
        return ApiCalls.SendAsync<AssetResponse>(() => _http.PutAsJsonAsync(Address(source, symbol) + "/history", new ChangeHistoryRequest(historyFrom)));
    }

    public Task<string?> RemoveAsync(string source, string symbol)
    {
        return ApiCalls.SendAsync(() => _http.DeleteAsync(Address(source, symbol)));
    }

    public Task<string?> RescanAsync(string source, string symbol)
    {
        return ApiCalls.SendAsync(() => _http.PostAsync(Address(source, symbol) + "/rescan", null));
    }

    public Task<string?> RebuildAsync(string source, string symbol)
    {
        return ApiCalls.SendAsync(() => _http.PostAsync(Address(source, symbol) + "/rebuild", null));
    }

    private static string Address(string source, string symbol)
    {
        return "api/assets/" + Uri.EscapeDataString(source) + "/" + Uri.EscapeDataString(symbol);
    }
}
