using System.Net.Http.Json;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/scenarios (SC1-SC16, SC20, SC21).</summary>
public sealed class ScenariosApi
{
    private const string Expert = "api/scenarios/expert";

    private readonly HttpClient _http;

    public ScenariosApi(HttpClient http)
    {
        _http = http;
    }

    public Task<ApiResult<ScenariosStatusResponse>> GetStatusAsync()
    {
        return ApiCalls.SendAsync<ScenariosStatusResponse>(() => _http.GetAsync("api/scenarios"));
    }

    public Task<ApiResult<List<ExpertScenarioSummary>>> ListAsync()
    {
        return ApiCalls.SendAsync<List<ExpertScenarioSummary>>(() => _http.GetAsync(Expert));
    }

    public Task<ApiResult<ExpertScenarioResponse>> GetAsync(string id)
    {
        return ApiCalls.SendAsync<ExpertScenarioResponse>(() => _http.GetAsync(Expert + "/" + Uri.EscapeDataString(id)));
    }

    /// <summary>The server snaps a point the user placed or let go; nothing is written (SC10).</summary>
    public Task<ApiResult<ExpertScenarioResponse>> SnapAsync(SnapScenarioPointRequest request)
    {
        return ApiCalls.SendAsync<ExpertScenarioResponse>(() => _http.PostAsJsonAsync(Expert + "/snap", request));
    }

    /// <summary>Shapes and problems of a scenario as the editor holds it; nothing is written.</summary>
    public Task<ApiResult<ExpertScenarioResponse>> PreviewAsync(ExpertScenarioRecord scenario)
    {
        return ApiCalls.SendAsync<ExpertScenarioResponse>(() => _http.PostAsJsonAsync(Expert + "/preview", scenario));
    }

    /// <summary>Writes the scenario: without an id the first save, which takes it (SC13, SC14).</summary>
    public Task<ApiResult<ExpertScenarioResponse>> SaveAsync(ExpertScenarioRecord scenario)
    {
        return scenario.Id == null
            ? ApiCalls.SendAsync<ExpertScenarioResponse>(() => _http.PostAsJsonAsync(Expert, scenario))
            : ApiCalls.SendAsync<ExpertScenarioResponse>(() => _http.PutAsJsonAsync(Expert + "/" + Uri.EscapeDataString(scenario.Id), scenario));
    }

    /// <returns>Null when the scenario was deleted, otherwise the message to show (SC16).</returns>
    public Task<string?> DeleteAsync(string id)
    {
        return ApiCalls.SendAsync(() => _http.DeleteAsync(Expert + "/" + Uri.EscapeDataString(id)));
    }
}
