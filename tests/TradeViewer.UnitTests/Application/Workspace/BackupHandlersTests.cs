using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Application.Features.Workspace.Commands;
using TradeViewer.Application.Features.Workspace.Handlers;
using TradeViewer.Application.Features.Workspace.Queries;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.UnitTests.Application.Workspace;

public class BackupHandlersTests
{
    private readonly FakeBackups _backups = new FakeBackups();

    [Fact]
    public async Task The_list_is_what_the_port_returns()
    {
        _backups.Backups.Add(new BackupResponse("workspace-20261115-030000-daily.db", DateTimeOffset.UnixEpoch, "daily", 4096));

        var result = await new GetBackupsQueryHandler(_backups).HandleAsync(new GetBackupsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_backups.Backups, result.Value);
    }

    [Fact]
    public async Task Back_up_now_returns_the_new_backup()
    {
        var result = await new CreateBackupCommandHandler(_backups).HandleAsync(new CreateBackupCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("manual", result.Value.Kind);
        Assert.Single(_backups.Backups);
    }

    [Fact]
    public async Task An_unknown_backup_cannot_be_downloaded()
    {
        var result = await new GetBackupFileQueryHandler(_backups).HandleAsync(new GetBackupFileQuery("missing.db"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("backup.notFound", result.Error.Code);
    }

    [Fact]
    public async Task A_backup_is_downloaded_as_a_file()
    {
        _backups.File = new FileContent(new MemoryStream(new byte[] { 1, 2, 3 }), "b.db", "application/octet-stream");

        var result = await new GetBackupFileQueryHandler(_backups).HandleAsync(new GetBackupFileQuery("b.db"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(_backups.File, result.Value);
    }

    [Theory]
    [InlineData(RestoreStatus.NotFound, ErrorType.NotFound, "backup.notFound")]
    [InlineData(RestoreStatus.Invalid, ErrorType.InvalidData, "backup.invalid")]
    [InlineData(RestoreStatus.TooLarge, ErrorType.Validation, "backup.tooLarge")]
    public async Task A_refused_restore_is_an_error_with_the_reason(RestoreStatus status, ErrorType type, string code)
    {
        _backups.Restore = new RestoreResult(status, "the reason");

        var byName = await new RestoreBackupCommandHandler(_backups).HandleAsync(new RestoreBackupCommand("b.db"), CancellationToken.None);
        var byFile = await new RestoreUploadedBackupCommandHandler(_backups)
            .HandleAsync(new RestoreUploadedBackupCommand(Stream.Null), CancellationToken.None);

        foreach (var result in new[] { byName, byFile })
        {
            Assert.True(result.IsFailure);
            Assert.Equal(type, result.Error.Type);
            Assert.Equal(code, result.Error.Code);
            Assert.Equal("the reason", result.Error.Message);
        }
    }

    [Fact]
    public async Task A_restore_that_worked_is_a_success()
    {
        var byName = await new RestoreBackupCommandHandler(_backups).HandleAsync(new RestoreBackupCommand("b.db"), CancellationToken.None);
        var byFile = await new RestoreUploadedBackupCommandHandler(_backups)
            .HandleAsync(new RestoreUploadedBackupCommand(Stream.Null), CancellationToken.None);

        Assert.True(byName.IsSuccess);
        Assert.True(byFile.IsSuccess);
        Assert.Equal("b.db", _backups.RestoredName);
    }

    private sealed class FakeBackups : IWorkspaceBackups
    {
        public List<BackupResponse> Backups { get; } = new List<BackupResponse>();

        public FileContent? File { get; set; }

        public RestoreResult Restore { get; set; } = new RestoreResult(RestoreStatus.Restored, "ok");

        public string? RestoredName { get; private set; }

        public Task<IReadOnlyList<BackupResponse>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<BackupResponse>>(Backups);
        }

        public Task<BackupResponse> CreateAsync(CancellationToken cancellationToken)
        {
            var backup = new BackupResponse("workspace-20261115-120000-manual.db", DateTimeOffset.UnixEpoch, "manual", 4096);
            Backups.Add(backup);
            return Task.FromResult(backup);
        }

        public Task<FileContent?> OpenAsync(string name, CancellationToken cancellationToken)
        {
            return Task.FromResult(File);
        }

        public Task<RestoreResult> RestoreAsync(string name, CancellationToken cancellationToken)
        {
            RestoredName = name;
            return Task.FromResult(Restore);
        }

        public Task<RestoreResult> RestoreAsync(Stream content, CancellationToken cancellationToken)
        {
            return Task.FromResult(Restore);
        }
    }
}
