using System.Net.Http.Json;
using TradeViewer.Contracts.Charts;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/chart-settings: the settings of a chart, saved per asset (CP2).</summary>
public sealed class ChartsApi
{
    private readonly HttpClient _http;

    public ChartsApi(HttpClient http)
    {
        _http = http;
    }

    public Task<ApiResult<ChartSettingsResponse>> GetSettingsAsync(string source, string symbol)
    {
        return ApiCalls.SendAsync<ChartSettingsResponse>(() => _http.GetAsync(Address(source, symbol)));
    }

    public Task<ApiResult<ChartSettingsResponse>> SaveSettingsAsync(string source, string symbol, ChartSettingsResponse settings)
    {
        return ApiCalls.SendAsync<ChartSettingsResponse>(() => _http.PutAsJsonAsync(Address(source, symbol), settings));
    }

    private static string Address(string source, string symbol)
    {
        return "api/chart-settings/" + Uri.EscapeDataString(source) + "/" + Uri.EscapeDataString(symbol);
    }
}
