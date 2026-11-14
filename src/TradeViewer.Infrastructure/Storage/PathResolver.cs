namespace TradeViewer.Infrastructure.Storage;

public static class PathResolver
{
    /// <summary>A leading ~ is the home folder of the user; a relative path is resolved against the content root.</summary>
    public static string Resolve(string path, string contentRoot)
    {
        var expanded = path;
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = path == "~" ? home : Path.Combine(home, path.Substring(2));
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded, contentRoot));
    }

    /// <summary>True when the path is the folder itself or lies below it.</summary>
    public static bool IsSameOrInside(string path, string folder)
    {
        if (string.Equals(path, folder, StringComparison.Ordinal))
        {
            return true;
        }

        return path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
