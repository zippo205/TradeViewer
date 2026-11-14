using System.Globalization;

namespace TradeViewer.Infrastructure.Configuration;

/// <summary>The checks of the configuration values that need no file system. Each returns the messages of what is wrong.</summary>
public static class ConfigurationRules
{
    public static IReadOnlyList<string> Check(MarketDataOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            errors.Add("MarketData:RootPath is required: the Downloader's output root.");
        }

        var downloader = options.Downloader;
        Positive(errors, "MarketData:Downloader:PollSeconds", downloader.PollSeconds);
        Positive(errors, "MarketData:Downloader:PollWindowMinutes", downloader.PollWindowMinutes);
        Positive(errors, "MarketData:Downloader:RequestTimeoutSeconds", downloader.RequestTimeoutSeconds);

        if (downloader.Enabled)
        {
            if (!Uri.TryCreate(downloader.BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add("MarketData:Downloader:BaseUrl must be an http or https address.");
            }

            if (!IsClientName(downloader.Client))
            {
                errors.Add("MarketData:Downloader:Client must be a name of letters, digits, '-' and '_'.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Check(EwtsOptions options)
    {
        var errors = new List<string>();
        Positive(errors, "Ewts:PollSeconds", options.PollSeconds);
        Positive(errors, "Ewts:RequestTimeoutSeconds", options.RequestTimeoutSeconds);
        return errors;
    }

    public static IReadOnlyList<string> Check(StorageOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DataPath))
        {
            errors.Add("Storage:DataPath is required: the folder for marketdata.db, workspace.db and backups.");
        }

        Positive(errors, "Storage:Backup:KeepCount", options.Backup.KeepCount);

        if (!TryParseBackupTime(options.Backup.Time, out _))
        {
            errors.Add("Storage:Backup:Time must be a time of day as HH:mm (UTC).");
        }

        return errors;
    }

    public static bool TryParseBackupTime(string? value, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    private static void Positive(List<string> errors, string name, int value)
    {
        if (value <= 0)
        {
            errors.Add(name + " must be greater than 0.");
        }
    }

    private static bool IsClientName(string? name)
    {
        return !string.IsNullOrEmpty(name) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
    }
}
