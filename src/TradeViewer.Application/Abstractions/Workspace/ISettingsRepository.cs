namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>The settings that are not part of a chart (WS5), one JSON value per key. A write is saved at once (WS1).</summary>
public interface ISettingsRepository
{
    /// <returns>The JSON value, or null when the setting was never written.</returns>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string json, CancellationToken cancellationToken);
}
