using TradeViewer.Domain.Drawings;

namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>The Fibonacci presets in workspace.db (DR4); a new workspace has the two defaults.</summary>
public interface IFibonacciPresetRepository
{
    /// <summary>All presets in the order they were added.</summary>
    Task<IReadOnlyList<FibonacciPreset>> GetAllAsync(CancellationToken cancellationToken);

    Task<FibonacciPreset?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Stores the preset under its id: new at the end, or in place of the one with that id.</summary>
    Task SaveAsync(FibonacciPreset preset, CancellationToken cancellationToken);

    /// <returns>False when there was no such preset.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
