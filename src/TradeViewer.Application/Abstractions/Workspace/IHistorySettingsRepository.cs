using TradeViewer.Domain.Assets;

namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>The default history of new assets (AS3), a setting of the workspace.</summary>
public interface IHistorySettingsRepository
{
    /// <summary>The setting of the user, or the initial one when it was never changed.</summary>
    Task<HistorySettings> GetAsync(CancellationToken cancellationToken);

    Task SetAsync(HistorySettings settings, CancellationToken cancellationToken);
}
