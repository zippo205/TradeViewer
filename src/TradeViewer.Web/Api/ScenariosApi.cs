using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Web.Drawings;
using TradeViewer.Web.Scenarios;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/scenarios (SC1-SC16, SC20, SC21).</summary>
public sealed class ScenariosApi : IExpertScenarioRemote
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

    /// <summary>
    /// Writes the scenario for the save queue: without an id the first save, which takes it (SC13, SC14). A passing
    /// problem (no connection, 5xx) is tried again; a refusal (4xx) is not. Sent with keepalive, so a save still
    /// reaches the server when the window is closed right after a change.
    /// </summary>
    public async Task<ScenarioSaveOutcome> SaveAsync(ExpertScenarioRecord scenario)
    {
        using var request = scenario.Id == null
            ? new HttpRequestMessage(HttpMethod.Post, Expert)
            : new HttpRequestMessage(HttpMethod.Put, Expert + "/" + Uri.EscapeDataString(scenario.Id));
        request.Content = JsonContent.Create(scenario);
        request.SetBrowserRequestOption("keepalive", true);

        try
        {
            using var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var saved = await response.Content.ReadFromJsonAsync<ExpertScenarioResponse>();
                return new ScenarioSaveOutcome(SaveResult.Saved, saved?.Scenario ?? scenario);
            }

            var code = (int)response.StatusCode;
            var message = await ApiCalls.ReadProblemAsync(response);
            var passing = code >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == HttpStatusCode.TooManyRequests;
            return new ScenarioSaveOutcome(passing ? SaveResult.Retry : SaveResult.Refused, Message: message);
        }
        catch (HttpRequestException)
        {
            return new ScenarioSaveOutcome(SaveResult.Retry, Message: "The server is not reachable.");
        }
        catch (JsonException)
        {
            return new ScenarioSaveOutcome(SaveResult.Saved, scenario);
        }
    }

    /// <returns>Null when the scenario was deleted, otherwise the message to show (SC16).</returns>
    public Task<string?> DeleteAsync(string id)
    {
        return ApiCalls.SendAsync(() => _http.DeleteAsync(Expert + "/" + Uri.EscapeDataString(id)));
    }
}
