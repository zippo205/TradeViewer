namespace TradeViewer.Domain.Drawings;

/// <summary>
/// What surrounds the text of a wave label. Circle stands for the double brackets of the notation, such as
/// ((1)), and is drawn as a circle around the text.
/// </summary>
public enum WaveEnclosure
{
    None,
    Parentheses,
    Circle
}
