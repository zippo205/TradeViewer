using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Web.Api;

/// <summary>The client of /api/workspace. A command returns null when it worked, otherwise the message to show.</summary>
public sealed class WorkspaceApi
{
    private const string Backups = "api/workspace/backups";

    private readonly HttpClient _http;

    public WorkspaceApi(HttpClient http)
    {
        _http = http;
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
        return SendAsync(() => _http.PostAsync(Backups, null));
    }

    public Task<string?> RestoreAsync(string name)
    {
        return SendAsync(() => _http.PostAsync(Backups + "/" + Uri.EscapeDataString(name) + "/restore", null));
    }

    public Task<string?> RestoreFileAsync(Stream file)
    {
        return SendAsync(() =>
        {
            var content = new StreamContent(file);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return _http.PostAsync(Backups + "/restore", content);
        });
    }

    private static async Task<string?> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            using var response = await send();
            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            return await ReadProblemAsync(response);
        }
        catch (HttpRequestException)
        {
            return "The server is not reachable.";
        }
    }

    /// <summary>The detail of the problem details (RFC 9457) the Api answers with.</summary>
    private static async Task<string> ReadProblemAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString()!;
            }
        }
        catch (JsonException)
        {
            // no problem details: the status has to do
        }

        return "The request failed (" + (int)response.StatusCode + ").";
    }
}
