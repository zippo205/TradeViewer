using System.Net.Http.Headers;
using System.Net.Http.Json;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/settings and /api/workspace. A command returns null when it worked, otherwise the message to show.</summary>
public sealed class WorkspaceApi
{
    private const string Backups = "api/workspace/backups";
    private const string Settings = "api/settings";

    private readonly HttpClient _http;

    public WorkspaceApi(HttpClient http)
    {
        _http = http;
    }

    public Task<ApiResult<SettingsResponse>> GetSettingsAsync()
    {
        return ApiCalls.SendAsync<SettingsResponse>(() => _http.GetAsync(Settings));
    }

    public Task<ApiResult<SettingsResponse>> SaveSettingsAsync(SettingsResponse settings)
    {
        return ApiCalls.SendAsync<SettingsResponse>(() => _http.PutAsJsonAsync(Settings, settings));
    }

    /// <summary>Remembers the chart as the one last used (CP3); the other settings stay as they are.</summary>
    public Task<ApiResult<SettingsResponse>> SaveLastChartAsync(string source, string symbol, string timeframe)
    {
        return SaveSettingsAsync(new SettingsResponse(null, new LastChartResponse(source, symbol, timeframe)));
    }

    /// <summary>Saves the price scale mode of all charts (CP2); the other settings stay as they are.</summary>
    public Task<ApiResult<SettingsResponse>> SavePriceScaleAsync(string priceScale)
    {
        return SaveSettingsAsync(new SettingsResponse(null, null, priceScale));
    }

    public async Task<IReadOnlyList<BackupResponse>> GetBackupsAsync()
    {
        var backups = await _http.GetFromJsonAsync<List<BackupResponse>>(Backups);
        return backups ?? new List<BackupResponse>();
    }

    public static string DownloadAddress(string name)
    {
        return Backups + "/" + Uri.EscapeDataString(name);
    }

    public Task<string?> CreateBackupAsync()
    {
        return ApiCalls.SendAsync(() => _http.PostAsync(Backups, null));
    }

    public Task<string?> RestoreAsync(string name)
    {
        return ApiCalls.SendAsync(() => _http.PostAsync(Backups + "/" + Uri.EscapeDataString(name) + "/restore", null));
    }

    public Task<string?> RestoreFileAsync(Stream file)
    {
        return ApiCalls.SendAsync(() =>
        {
            var content = new StreamContent(file);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return _http.PostAsync(Backups + "/restore", content);
        });
    }
}
