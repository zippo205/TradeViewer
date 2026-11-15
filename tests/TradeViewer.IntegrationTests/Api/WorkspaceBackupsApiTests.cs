using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.IntegrationTests.Api;

/// <summary>/api/workspace/backups through the real application (WS2, WS3).</summary>
public class WorkspaceBackupsApiTests : IDisposable
{
    private const string Backups = "/api/workspace/backups";

    private readonly TradeViewerFactory _factory = new TradeViewerFactory();
    private readonly HttpClient _client;

    public WorkspaceBackupsApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task The_start_creates_the_workspace_in_the_data_path()
    {
        var response = await _client.GetAsync(Backups);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(File.Exists(Path.Combine(_factory.DataPath, "workspace.db")));
    }

    [Fact]
    public async Task Back_up_now_makes_a_backup_that_is_listed_and_can_be_downloaded()
    {
        var created = await CreateBackupAsync();

        var list = await _client.GetFromJsonAsync<List<BackupResponse>>(Backups);
        var download = await _client.GetAsync(Backups + "/" + created.Name);
        var bytes = await download.Content.ReadAsByteArrayAsync();

        Assert.Equal("manual", created.Kind);
        Assert.Contains(created, list!);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/octet-stream", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(created.Name, download.Content.Headers.ContentDisposition!.FileNameStar ?? download.Content.Headers.ContentDisposition.FileName);
        Assert.Equal(created.SizeBytes, bytes.Length);
        Assert.Equal("SQLite format 3", System.Text.Encoding.ASCII.GetString(bytes, 0, 15));
    }

    [Fact]
    public async Task A_restore_by_name_replaces_the_workspace_while_the_application_runs()
    {
        var settings = _factory.Services.GetRequiredService<ISettingsRepository>();
        await settings.SetAsync("lastChart", "\"old\"", CancellationToken.None);
        var backup = await CreateBackupAsync();
        await settings.SetAsync("lastChart", "\"new\"", CancellationToken.None);

        var response = await _client.PostAsync(Backups + "/" + backup.Name + "/restore", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("\"old\"", await settings.GetAsync("lastChart", CancellationToken.None));
    }

    [Fact]
    public async Task A_downloaded_backup_can_be_uploaded_and_restored()
    {
        var settings = _factory.Services.GetRequiredService<ISettingsRepository>();
        await settings.SetAsync("lastChart", "\"old\"", CancellationToken.None);
        var backup = await CreateBackupAsync();
        var bytes = await _client.GetByteArrayAsync(Backups + "/" + backup.Name);
        await settings.SetAsync("lastChart", "\"new\"", CancellationToken.None);

        var response = await _client.PostAsync(Backups + "/restore", Upload(bytes));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("\"old\"", await settings.GetAsync("lastChart", CancellationToken.None));
    }

    [Fact]
    public async Task An_uploaded_file_that_is_no_backup_is_answered_with_422_and_changes_nothing()
    {
        var settings = _factory.Services.GetRequiredService<ISettingsRepository>();
        await settings.SetAsync("lastChart", "\"kept\"", CancellationToken.None);

        var response = await _client.PostAsync(Backups + "/restore", Upload(System.Text.Encoding.UTF8.GetBytes(new string('x', 10000))));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("backup.invalid", await ProblemCodeAsync(response));
        Assert.Equal("\"kept\"", await settings.GetAsync("lastChart", CancellationToken.None));
    }

    [Theory]
    [InlineData("GET", "/workspace-20200101-000000-daily.db")]
    [InlineData("POST", "/workspace-20200101-000000-daily.db/restore")]
    [InlineData("GET", "/workspace.db")]
    [InlineData("POST", "/workspace.db/restore")]
    [InlineData("GET", "/..%2Fworkspace.db")]
    [InlineData("POST", "/..%2Fworkspace.db/restore")]
    public async Task An_unknown_backup_is_answered_with_404_problem_details(string method, string path)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), Backups + path));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task The_workspace_survives_a_restart_of_the_application()
    {
        var settings = _factory.Services.GetRequiredService<ISettingsRepository>();
        await settings.SetAsync("lastChart", "\"binance/BTCUSDT\"", CancellationToken.None);
        var backup = await CreateBackupAsync();

        using (var restarted = _factory.Restart())
        using (var client = restarted.CreateClient())
        {
            var restartedSettings = restarted.Services.GetRequiredService<ISettingsRepository>();
            var list = await client.GetFromJsonAsync<List<BackupResponse>>(Backups);

            Assert.Equal("\"binance/BTCUSDT\"", await restartedSettings.GetAsync("lastChart", CancellationToken.None));
            Assert.Contains(backup, list!);
        }
    }

    private async Task<BackupResponse> CreateBackupAsync()
    {
        var response = await _client.PostAsync(Backups, null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BackupResponse>())!;
    }

    private static ByteArrayContent Upload(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
