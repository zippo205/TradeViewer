using TradeViewer.Domain.Indicators;

namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>The indicator templates in workspace.db (IN2), on the table of script 0007. They belong to no asset.</summary>
public interface IIndicatorTemplateRepository
{
    /// <summary>All templates, ordered by name.</summary>
    Task<IReadOnlyList<IndicatorTemplate>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Stores the template under its id: new, or in place of the one with that id.</summary>
    Task SaveAsync(IndicatorTemplate template, CancellationToken cancellationToken);

    /// <returns>False when there was no such template.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
