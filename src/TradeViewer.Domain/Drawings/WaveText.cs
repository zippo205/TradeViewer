namespace TradeViewer.Domain.Drawings;

/// <summary>A wave label as it is drawn: the text and what surrounds it, e.g. "iv" in parentheses for (iv).</summary>
public sealed record WaveText(string Text, WaveEnclosure Enclosure)
{
    /// <summary>The label written out in the textbook notation: ((1)), (iv), C.</summary>
    public override string ToString()
    {
        switch (Enclosure)
        {
            case WaveEnclosure.Parentheses:
                return "(" + Text + ")";
            case WaveEnclosure.Circle:
                return "((" + Text + "))";
            default:
                return Text;
        }
    }
}
