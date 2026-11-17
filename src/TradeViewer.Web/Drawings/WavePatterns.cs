namespace TradeViewer.Web.Drawings;

/// <summary>An Elliott wave pattern of the tool rail (DR5): a polyline whose points after the start get these labels.</summary>
public sealed record WavePattern(string Code, string Title, IReadOnlyList<string> Labels, string Icon);

/// <summary>The five wave patterns, grouped under one button of the rail as in TradingView.</summary>
public static class WavePatterns
{
    public static IReadOnlyList<WavePattern> All { get; } = new[]
    {
        new WavePattern("impulse", "Impulse wave (1-2-3-4-5)", new[] { "1", "2", "3", "4", "5" },
            "<path class=\"line\" d=\"M1 17L4.5 9 7.5 13 12.5 3 15.5 7 19 1\" /><circle cx=\"4.5\" cy=\"9\" r=\"1.4\" /><circle cx=\"12.5\" cy=\"3\" r=\"1.4\" /><circle cx=\"19\" cy=\"1.5\" r=\"1.4\" />"),
        new WavePattern("correction", "Correction wave (A-B-C)", new[] { "A", "B", "C" },
            "<path class=\"line\" d=\"M2 3L8 13 12 8 18 18\" /><circle cx=\"8\" cy=\"13\" r=\"1.4\" /><circle cx=\"12\" cy=\"8\" r=\"1.4\" /><circle cx=\"18\" cy=\"17.5\" r=\"1.4\" />"),
        new WavePattern("triangle", "Triangle wave (A-B-C-D-E)", new[] { "A", "B", "C", "D", "E" },
            "<path class=\"line\" d=\"M1 2L5 17 9 5 12.5 14 15.5 8 18.5 12\" /><path class=\"line\" d=\"M1 2L18.5 9M5 17L18.5 11\" stroke-dasharray=\"1.5 2\" />"),
        new WavePattern("doubleCombo", "Double combination (W-X-Y)", new[] { "W", "X", "Y" },
            "<path class=\"line\" d=\"M2 3L8 11 12 7 18 16\" /><text x=\"8\" y=\"19\" font-size=\"6\" text-anchor=\"middle\">W</text><text x=\"12\" y=\"5\" font-size=\"6\" text-anchor=\"middle\">X</text>"),
        new WavePattern("tripleCombo", "Triple combination (W-X-Y-X-Z)", new[] { "W", "X", "Y", "X", "Z" },
            "<path class=\"line\" d=\"M1 2L5 9 8 6 12 13 15 10 19 18\" /><circle cx=\"5\" cy=\"9\" r=\"1.2\" /><circle cx=\"12\" cy=\"13\" r=\"1.2\" /><circle cx=\"19\" cy=\"17.5\" r=\"1.2\" />")
    };

    public static WavePattern? Find(string? code)
    {
        return All.FirstOrDefault(pattern => pattern.Code == code);
    }
}
