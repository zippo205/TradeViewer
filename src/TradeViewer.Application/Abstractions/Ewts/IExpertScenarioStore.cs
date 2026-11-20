using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Application.Abstractions.Ewts;

/// <summary>
/// The expert scenarios in <c>expert/</c> of the scenario folder (decisions 0010, 0012). The file is the only record
/// of a scenario. A save keeps every field of the file this build does not know (legacy, later additions) and is
/// atomic; nothing outside <c>expert/</c> is ever read or written.
/// </summary>
public interface IExpertScenarioStore
{
    /// <summary>
    /// Every *.json of the folder, ordered by id: the head of each scenario, or its error when the file cannot be read
    /// (SC7). Only files that changed since the last call are read again.
    /// </summary>
    Task<IReadOnlyList<ExpertScenarioSummary>> ListAsync(CancellationToken cancellationToken);

    Task<ExpertScenarioRead> ReadAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the scenario. Without an id it is the first save: the next free id of its prefix is taken under a lock,
    /// and a file that exists is never overwritten (SC14). Every save sets updatedAt; the first save that finds no
    /// createdAt sets it too.
    /// </summary>
    Task<ExpertScenarioSave> SaveAsync(ExpertScenarioRecord scenario, CancellationToken cancellationToken);

    /// <returns>False when there was no such scenario.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
