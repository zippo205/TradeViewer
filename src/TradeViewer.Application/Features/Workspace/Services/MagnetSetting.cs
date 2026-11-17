using TradeViewer.Application.Abstractions.Workspace;

namespace TradeViewer.Application.Features.Workspace.Services;

/// <summary>
/// The magnet of the drawing tools, kept as the setting "magnet". It is one setting for all charts: a way of
/// working, not a property of an asset (DR2).
/// </summary>
public static class MagnetSetting
{
    public const string Key = "magnet";

    /// <returns>The magnet as the user left it; on before the first choice (DR2).</returns>
    public static async Task<bool> ReadAsync(ISettingsRepository settings, CancellationToken cancellationToken)
    {
        return await settings.GetAsync(Key, cancellationToken) != "false";
    }

    public static Task WriteAsync(ISettingsRepository settings, bool magnet, CancellationToken cancellationToken)
    {
        return settings.SetAsync(Key, magnet ? "true" : "false", cancellationToken);
    }
}
