using TradeViewer.Contracts.Drawings;

namespace TradeViewer.Web.Drawings;

/// <summary>Where the save queue stores drawings: the Api (DrawingsApi), or a fake in the tests.</summary>
public interface IDrawingRemote
{
    Task<SaveOutcome> PutAsync(DrawingRecord drawing);

    Task<SaveOutcome> DeleteAsync(Guid id);
}
