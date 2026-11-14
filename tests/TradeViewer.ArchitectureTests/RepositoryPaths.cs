namespace TradeViewer.ArchitectureTests;

internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string Project(string name)
    {
        return Path.Combine(Root, "src", name, name + ".csproj");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TradeViewer.sln")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("TradeViewer.sln was not found above " + AppContext.BaseDirectory);
        }

        return directory.FullName;
    }
}
