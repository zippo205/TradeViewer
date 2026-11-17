namespace TradeViewer.Domain.Drawings;

/// <summary>
/// A wave label as it is stored: degree, label and side, not the drawn text (DR5). Label is one of
/// <see cref="WaveNotation.Labels"/> (1 to 5, A to E, W, X, Y, Z); <see cref="WaveNotation.Text"/> gives its form in the degree.
/// </summary>
public sealed record WaveMark(WaveDegree Degree, string Label, WaveSide Side);
