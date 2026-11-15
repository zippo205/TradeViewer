using System.Globalization;
using System.Text.RegularExpressions;

namespace TradeViewer.Infrastructure.Workspace.Backups;

/// <summary>
/// The names of the backup files: workspace-yyyyMMdd-HHmmss-daily.db, -manual.db and -before-v{n}.db (UTC).
/// Only names of this form are listed, downloaded, restored or deleted; so a name from a request can never
/// lead out of the backups folder.
/// </summary>
public static class BackupFileName
{
    private const string TimeFormat = "yyyyMMdd-HHmmss";

    private static readonly Regex Pattern = new Regex(
        "^workspace-(?<time>[0-9]{8}-[0-9]{6})-(?<kind>daily|manual|before-v(?<version>[0-9]{1,9}))\\.db\\z",
        RegexOptions.CultureInvariant);

    public static string Create(DateTimeOffset createdAt, BackupKind kind, int? upgradeVersion)
    {
        var time = createdAt.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);

        switch (kind)
        {
            case BackupKind.Daily:
                return "workspace-" + time + "-daily.db";
            case BackupKind.Manual:
                return "workspace-" + time + "-manual.db";
            case BackupKind.BeforeUpgrade:
                if (upgradeVersion == null)
                {
                    throw new ArgumentException("A backup before an upgrade needs the schema version.", nameof(upgradeVersion));
                }

                return "workspace-" + time + "-before-v" + upgradeVersion.Value.ToString(CultureInfo.InvariantCulture) + ".db";
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>Reads the time and the kind out of a file name; false for any other file.</summary>
    public static bool TryParse(string? name, long sizeBytes, out BackupFile? backup)
    {
        backup = null;
        if (name == null)
        {
            return false;
        }

        var match = Pattern.Match(name);
        if (!match.Success)
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                match.Groups["time"].Value,
                TimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var time))
        {
            return false;
        }

        var kindText = match.Groups["kind"].Value;
        var kind = kindText == "daily" ? BackupKind.Daily : kindText == "manual" ? BackupKind.Manual : BackupKind.BeforeUpgrade;
        int? version = kind == BackupKind.BeforeUpgrade
            ? int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture)
            : null;

        backup = new BackupFile(name, new DateTimeOffset(time, TimeSpan.Zero), kind, version, sizeBytes);
        return true;
    }
}
