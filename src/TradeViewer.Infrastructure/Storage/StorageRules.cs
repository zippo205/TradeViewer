namespace TradeViewer.Infrastructure.Storage;

/// <summary>The checks of the folders that need no file system access.</summary>
public static class StorageRules
{
    public static IReadOnlyList<string> Check(StorageLocations locations)
    {
        var errors = new List<string>();

        if (PathResolver.IsSameOrInside(locations.DataPath, locations.MarketDataRoot))
        {
            errors.Add("Storage:DataPath must not be the Downloader's output root or lie inside it: that folder is read only.");
        }

        if (locations.ScenariosRoot != null)
        {
            if (PathResolver.IsSameOrInside(locations.DataPath, locations.ScenariosRoot))
            {
                errors.Add("Storage:DataPath must not be the scenario folder or lie inside it.");
            }

            if (PathResolver.IsSameOrInside(locations.ScenariosRoot, locations.MarketDataRoot))
            {
                errors.Add("Scenarios:RootPath must not be the Downloader's output root or lie inside it.");
            }
        }

        return errors;
    }
}
