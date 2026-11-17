using TradeViewer.Application.Abstractions.Workspace;
using TradeViewer.Domain.Charts;
using TradeViewer.Domain.Drawings;

namespace TradeViewer.UnitTests.Application.Drawings;

/// <summary>In-memory drawings of known assets, as the workspace keeps them.</summary>
internal sealed class FakeDrawingRepository : IDrawingRepository
{
    public HashSet<(string Source, string Symbol)> Assets { get; } = new HashSet<(string, string)>();

    public List<Drawing> Drawings { get; } = new List<Drawing>();

    public Task<IReadOnlyList<Drawing>> GetByAssetAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Drawing>>(Drawings.Where(drawing => drawing.Source == source && drawing.Symbol == symbol).ToList());
    }

    public Task<Drawing?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Drawings.FirstOrDefault(drawing => drawing.Id == id));
    }

    public Task<DrawingSaveResult> SaveAsync(Drawing drawing, CancellationToken cancellationToken)
    {
        var index = Drawings.FindIndex(existing => existing.Id == drawing.Id);
        if (index >= 0 && (Drawings[index].Source != drawing.Source || Drawings[index].Symbol != drawing.Symbol))
        {
            return Task.FromResult(DrawingSaveResult.OtherAsset);
        }

        if (!Assets.Contains((drawing.Source, drawing.Symbol)))
        {
            return Task.FromResult(DrawingSaveResult.UnknownAsset);
        }

        if (index >= 0)
        {
            Drawings[index] = drawing;
        }
        else
        {
            Drawings.Add(drawing);
        }

        return Task.FromResult(DrawingSaveResult.Saved);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Drawings.RemoveAll(drawing => drawing.Id == id) > 0);
    }
}

internal sealed class FakeFibonacciPresetRepository : IFibonacciPresetRepository
{
    public List<FibonacciPreset> Presets { get; } = FibonacciPresets.Defaults.ToList();

    public Task<IReadOnlyList<FibonacciPreset>> GetAllAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<FibonacciPreset>>(Presets.ToList());
    }

    public Task<FibonacciPreset?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Presets.FirstOrDefault(preset => preset.Id == id));
    }

    public Task SaveAsync(FibonacciPreset preset, CancellationToken cancellationToken)
    {
        var index = Presets.FindIndex(existing => existing.Id == preset.Id);
        if (index >= 0)
        {
            Presets[index] = preset;
        }
        else
        {
            Presets.Add(preset);
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult(Presets.RemoveAll(preset => preset.Id == id) > 0);
    }
}

internal sealed class FakeChartSettingsRepository : IChartSettingsRepository
{
    public HashSet<(string Source, string Symbol)> Assets { get; } = new HashSet<(string, string)>();

    public Dictionary<(string Source, string Symbol), ChartSettings> Saved { get; } = new Dictionary<(string, string), ChartSettings>();

    public Task<ChartSettings?> GetAsync(string source, string symbol, CancellationToken cancellationToken)
    {
        return Task.FromResult(Saved.TryGetValue((source, symbol), out var settings) ? settings : null);
    }

    public Task<bool> SetAsync(string source, string symbol, ChartSettings settings, CancellationToken cancellationToken)
    {
        if (!Assets.Contains((source, symbol)))
        {
            return Task.FromResult(false);
        }

        Saved[(source, symbol)] = settings;
        return Task.FromResult(true);
    }
}
