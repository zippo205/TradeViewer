namespace TradeViewer.Domain.Assets;

/// <summary>The rule for a new order of a watchlist: it names exactly the assets of the list, each once (AS1).</summary>
public static class WatchlistOrder
{
    public static bool IsValid(IReadOnlyCollection<(string Source, string Symbol)> current, IReadOnlyList<(string Source, string Symbol)> requested)
    {
        if (current.Count != requested.Count)
        {
            return false;
        }

        var wanted = new HashSet<(string, string)>(requested);
        return wanted.Count == requested.Count && current.All(wanted.Contains);
    }

    /// <summary>The five watchlists in the order they are shown, one per asset class.</summary>
    public static IReadOnlyList<AssetClass> Lists { get; } = new[]
    {
        AssetClass.Crypto, AssetClass.Stock, AssetClass.Etf, AssetClass.Forex, AssetClass.Commodity
    };

    public static string Title(AssetClass assetClass)
    {
        switch (assetClass)
        {
            case AssetClass.Crypto:
                return "Crypto";
            case AssetClass.Stock:
                return "Stocks";
            case AssetClass.Etf:
                return "ETFs";
            case AssetClass.Forex:
                return "Forex";
            case AssetClass.Commodity:
                return "Commodities";
            default:
                throw new ArgumentOutOfRangeException(nameof(assetClass));
        }
    }
}
