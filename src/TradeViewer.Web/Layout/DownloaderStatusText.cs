using System.Globalization;
using TradeViewer.Contracts.Downloader;

namespace TradeViewer.Web.Layout;

/// <summary>The words for the Downloader's state, shared by the header dot and the status page (DL1, DL4).</summary>
public static class DownloaderStatusText
{
    /// <summary>One line: what the colour of the dot means just now.</summary>
    public static string Summary(DownloaderStatusResponse? status)
    {
        if (status == null)
        {
            return "Downloader: not known yet";
        }

        if (!status.Enabled)
        {
            return "Downloader: switched off in the configuration";
        }

        if (status.Reachable == null)
        {
            return "Downloader: not asked yet";
        }

        if (status.Reachable == false)
        {
            return "Downloader: not reachable since " + Time(status.UnreachableSince ?? status.CheckedAt);
        }

        var service = status.Service;
        if (service == null)
        {
            return "Downloader: reachable, but its answer could not be read";
        }

        var notOk = service.Sources.Where(source => source.State != "ok").Select(source => source.Source + " " + StateText(source.State)).ToList();
        var cycle = service.LastCycle == null
            ? "no update cycle yet"
            : "last cycle " + service.LastCycle.Result + " at " + Time(service.LastCycle.FinishedAt ?? service.LastCycle.StartedAt);
        return "Downloader: reachable, " + cycle + (notOk.Count == 0 ? string.Empty : "; " + string.Join(", ", notOk));
    }

    public static string StateText(string state)
    {
        switch (state)
        {
            case "ok":
                return "ok";
            case "rateLimited":
                return "rate limited";
            case "unavailable":
                return "unavailable";
            default:
                return state;
        }
    }

    public static string GapCount(int gaps)
    {
        return gaps == 1 ? "1 gap" : gaps.ToString(CultureInfo.InvariantCulture) + " gaps";
    }

    /// <summary>UTC everywhere (MD7).</summary>
    public static string Time(DateTimeOffset? time)
    {
        return time == null ? "-" : time.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }
}
