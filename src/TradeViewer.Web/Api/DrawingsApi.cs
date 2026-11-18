using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using TradeViewer.Contracts.Charts;
using TradeViewer.Contracts.Drawings;
using TradeViewer.Web.Drawings;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/drawings, /api/fibonacci-presets and /api/chart-settings (DR1-DR6, CP2).</summary>
public sealed class DrawingsApi : IDrawingRemote
{
    private readonly HttpClient _http;
    private readonly WindowIdentity _window;

    private const string Presets = "api/fibonacci-presets";

    public DrawingsApi(HttpClient http, WindowIdentity window)
    {
        _http = http;
        _window = window;
    }

    public Task<ApiResult<List<DrawingRecord>>> GetAsync(string source, string symbol)
    {
        return ApiCalls.SendAsync<List<DrawingRecord>>(() => _http.GetAsync("api/drawings/" + Escape(source) + "/" + Escape(symbol)));
    }

    public Task<SaveOutcome> PutAsync(DrawingRecord drawing)
    {
        return SendAsync(HttpMethod.Put, drawing.Id, JsonContent.Create(drawing), notFoundIsDone: false);
    }

    public Task<SaveOutcome> DeleteAsync(Guid id)
    {
        return SendAsync(HttpMethod.Delete, id, null, notFoundIsDone: true);
    }

    public Task<ApiResult<List<FibonacciPresetRecord>>> GetPresetsAsync()
    {
        return ApiCalls.SendAsync<List<FibonacciPresetRecord>>(() => _http.GetAsync(Presets));
    }

    /// <summary>Stores a preset, new or edited; the answer holds its levels as stored (sorted).</summary>
    public Task<ApiResult<FibonacciPresetRecord>> SavePresetAsync(FibonacciPresetRecord preset)
    {
        return ApiCalls.SendAsync<FibonacciPresetRecord>(() => _http.PutAsJsonAsync(Presets + "/" + preset.Id, preset));
    }

    /// <returns>Null when the preset was deleted, otherwise the message to show (the last of a kind stays).</returns>
    public Task<string?> DeletePresetAsync(Guid id)
    {
        return ApiCalls.SendAsync(() => _http.DeleteAsync(Presets + "/" + id));
    }

    public Task<ApiResult<ChartSettingsResponse>> GetChartSettingsAsync(string source, string symbol)
    {
        return ApiCalls.SendAsync<ChartSettingsResponse>(() => _http.GetAsync(ChartSettings(source, symbol)));
    }

    /// <summary>
    /// Replaces the chart settings of the asset (CP2), indicators included. The window is named, so it ignores its own
    /// ChartSettingsChanged event; the request is sent with the keep-alive flag, so a change made right before the
    /// window is closed still arrives (WS1).
    /// </summary>
    /// <returns>Null when the settings were saved, otherwise the message to show.</returns>
    public Task<string?> SaveChartSettingsAsync(string source, string symbol, ChartSettingsResponse settings)
    {
        return ApiCalls.SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put, ChartSettings(source, symbol)) { Content = JsonContent.Create(settings) };
            request.Headers.Add(WindowIdentity.Header, _window.Id);
            request.SetBrowserRequestOption("keepalive", true);
            return _http.SendAsync(request);
        });
    }

    /// <summary>
    /// One save of a drawing. The request is sent with the browser's keep-alive flag, so a save that has started
    /// still reaches the server when the user closes the window right after letting go.
    /// </summary>
    private async Task<SaveOutcome> SendAsync(HttpMethod method, Guid id, HttpContent? content, bool notFoundIsDone)
    {
        using var request = new HttpRequestMessage(method, "api/drawings/" + id) { Content = content };
        request.Headers.Add(WindowIdentity.Header, _window.Id);
        request.SetBrowserRequestOption("keepalive", true);

        try
        {
            using var response = await _http.SendAsync(request);
            if (response.IsSuccessStatusCode || (notFoundIsDone && response.StatusCode == HttpStatusCode.NotFound))
            {
                return SaveOutcome.Saved;
            }

            var code = (int)response.StatusCode;
            var message = await ApiCalls.ReadProblemAsync(response);
            var passing = code >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == HttpStatusCode.TooManyRequests;
            return new SaveOutcome(passing ? SaveResult.Retry : SaveResult.Refused, message);
        }
        catch (HttpRequestException)
        {
            return new SaveOutcome(SaveResult.Retry, "The server is not reachable.");
        }
    }

    private static string ChartSettings(string source, string symbol)
    {
        return "api/chart-settings/" + Escape(source) + "/" + Escape(symbol);
    }

    private static string Escape(string text)
    {
        return Uri.EscapeDataString(text);
    }
}
