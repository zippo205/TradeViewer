using System.Net.Http.Json;
using TradeViewer.Contracts.Indicators;

namespace TradeViewer.Web.Api;

/// <summary>
/// The client of /api/indicator-defaults and /api/indicator-templates (IN1, IN2). The indicators of a chart are
/// saved with its chart settings (DrawingsApi); their values the chart module fetches itself.
/// </summary>
public sealed class IndicatorsApi
{
    private const string Templates = "api/indicator-templates";

    private readonly HttpClient _http;

    public IndicatorsApi(HttpClient http)
    {
        _http = http;
    }

    /// <summary>A new indicator of each kind with TradingView's defaults (IN1); the ids are empty.</summary>
    public Task<ApiResult<List<IndicatorRecord>>> GetDefaultsAsync()
    {
        return ApiCalls.SendAsync<List<IndicatorRecord>>(() => _http.GetAsync("api/indicator-defaults"));
    }

    public Task<ApiResult<List<IndicatorTemplateRecord>>> GetTemplatesAsync()
    {
        return ApiCalls.SendAsync<List<IndicatorTemplateRecord>>(() => _http.GetAsync(Templates));
    }

    public Task<ApiResult<IndicatorTemplateRecord>> SaveTemplateAsync(IndicatorTemplateRecord template)
    {
        return ApiCalls.SendAsync<IndicatorTemplateRecord>(() => _http.PutAsJsonAsync(Templates + "/" + template.Id, template));
    }

    /// <returns>Null when the template was deleted, otherwise the message to show.</returns>
    public Task<string?> DeleteTemplateAsync(Guid id)
    {
        return ApiCalls.SendAsync(() => _http.DeleteAsync(Templates + "/" + id));
    }
}
