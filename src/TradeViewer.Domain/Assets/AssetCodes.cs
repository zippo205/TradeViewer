namespace TradeViewer.Domain.Assets;

/// <summary>The codes of asset classes and session types as files, the Downloader and the API write them.</summary>
public static class AssetCodes
{
    private static readonly (AssetClass Value, string Code)[] Classes =
    {
        (AssetClass.Crypto, "crypto"),
        (AssetClass.Stock, "stock"),
        (AssetClass.Etf, "etf"),
        (AssetClass.Forex, "forex"),
        (AssetClass.Commodity, "commodity")
    };

    private static readonly (SessionType Value, string Code)[] Sessions =
    {
        (SessionType.Continuous, "continuous"),
        (SessionType.Exchange, "exchange"),
        (SessionType.NewYorkClose, "newYorkClose")
    };

    public static string Code(AssetClass assetClass)
    {
        return Classes.First(pair => pair.Value == assetClass).Code;
    }

    public static bool TryParse(string? code, out AssetClass assetClass)
    {
        foreach (var pair in Classes)
        {
            if (pair.Code == code)
            {
                assetClass = pair.Value;
                return true;
            }
        }

        assetClass = default;
        return false;
    }

    public static string Code(SessionType type)
    {
        return Sessions.First(pair => pair.Value == type).Code;
    }

    public static bool TryParse(string? code, out SessionType type)
    {
        foreach (var pair in Sessions)
        {
            if (pair.Code == code)
            {
                type = pair.Value;
                return true;
            }
        }

        type = default;
        return false;
    }
}
