using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Application.Features.Assets.Services;

/// <summary>The expected failures of the asset handlers.</summary>
public static class AssetErrors
{
    public static Error NotFound(string source, string symbol)
    {
        return Error.NotFound("asset.notFound", "There is no asset " + source + "/" + symbol + ".");
    }

    public static Error AlreadyExists(string source, string symbol)
    {
        return Error.Conflict("asset.exists", source + "/" + symbol + " is an asset already.");
    }

    public static Error UnknownSymbol(string source, string symbol)
    {
        return Error.NotFound("symbol.notFound", symbol + " is not in the symbol list of " + source + ".");
    }

    public static Error NoData(string source, string symbol)
    {
        return Error.InvalidData(
            "asset.noData",
            "The Downloader's output root has no folder for " + source + "/" + symbol + ", and the Downloader is switched off.");
    }
}
