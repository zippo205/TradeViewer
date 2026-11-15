using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeViewer.Infrastructure.Configuration;

namespace TradeViewer.Infrastructure.Workspace.Backups;

/// <summary>
/// Backs up the workspace once a day at Storage:Backup:Time (UTC), and at once after a start when the backup
/// of the day is missing, e.g. because the machine was off at that time (WS3).
/// </summary>
public sealed class DailyBackupService : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    private readonly WorkspaceBackups _backups;
    private readonly BackupStore _store;
    private readonly TimeProvider _time;
    private readonly TimeOnly _timeOfDay;
    private readonly ILogger<DailyBackupService> _logger;

    public DailyBackupService(
        WorkspaceBackups backups,
        BackupStore store,
        IOptions<StorageOptions> options,
        TimeProvider time,
        ILogger<DailyBackupService> logger)
    {
        _backups = backups;
        _store = store;
        _time = time;
        _logger = logger;

        // The value was validated at the start (ConfigurationRules).
        ConfigurationRules.TryParseBackupTime(options.Value.Backup.Time, out _timeOfDay);
    }

    /// <summary>Makes the daily backup when it is due.</summary>
    /// <returns>True when a backup was made.</returns>
    public async Task<bool> BackupIfDueAsync(CancellationToken cancellationToken)
    {
        var newest = _store.List().FirstOrDefault();
        if (!BackupSchedule.IsDue(_time.GetUtcNow(), _timeOfDay, newest?.CreatedAt))
        {
            return false;
        }

        await _backups.CreateAsync(BackupKind.Daily, cancellationToken);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                await BackupIfDueAsync(stoppingToken);

                var now = _time.GetUtcNow();
                delay = BackupSchedule.NextDue(now, _timeOfDay) - now;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The daily backup of the workspace failed; next try in {Delay}.", RetryDelay);
                delay = RetryDelay;
            }

            try
            {
                await Task.Delay(delay, _time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
