using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Contracts.Workspace;

namespace TradeViewer.Application.Features.Workspace.Services;

/// <summary>
/// The price scale mode, kept as the setting "priceScale". It is one setting for all charts: it stays when
/// the asset or the timeframe changes (CP2).
/// </summary>
public static class PriceScaleSetting
{
    public const string Key = "priceScale";

    /// <returns>The mode the user chose; linear before the first choice.</returns>
    public static async Task<string> ReadAsync(ISettingsRepository settings, CancellationToken cancellationToken)
    {
        var json = await settings.GetAsync(Key, cancellationToken);
        return json == "\"" + PriceScales.Log + "\"" ? PriceScales.Log : PriceScales.Linear;
    }

    public static Task WriteAsync(ISettingsRepository settings, string priceScale, CancellationToken cancellationToken)
    {
        return settings.SetAsync(Key, "\"" + priceScale + "\"", cancellationToken);
    }

    /// <returns>Null when the mode is one of the two, otherwise the error naming the field.</returns>
    public static Error? Validate(string priceScale)
    {
        if (priceScale == PriceScales.Linear || priceScale == PriceScales.Log)
        {
            return null;
        }

        return Error.Validation(
            "settings.invalid",
            "The settings are not valid.",
            new Dictionary<string, string[]> { ["priceScale"] = new[] { "Must be linear or log." } });
    }
}
