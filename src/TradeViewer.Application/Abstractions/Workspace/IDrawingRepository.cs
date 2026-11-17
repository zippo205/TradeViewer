using TradeViewer.Domain.Drawings;

namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>The drawings of the user in workspace.db (DR1-DR5). A write is saved at once (WS1); the last save wins.</summary>
public interface IDrawingRepository
{
    /// <summary>The drawings of an asset in the order they were made. A stored drawing this build cannot read is left in place and skipped.</summary>
    Task<IReadOnlyList<Drawing>> GetByAssetAsync(string source, string symbol, CancellationToken cancellationToken);

    Task<Drawing?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Stores the drawing under its id: new, or in place of the one with that id. Saving the same drawing again changes nothing.</summary>
    Task<DrawingSaveResult> SaveAsync(Drawing drawing, CancellationToken cancellationToken);

    /// <returns>False when there was no such drawing.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
