namespace TradeViewer.Application.Abstractions.Ewts;

/// <summary>Why a save wrote nothing.</summary>
public enum ExpertScenarioSaveProblem
{
    /// <summary>A first save without the end of wave 2: the id needs its month (SC14).</summary>
    NoWave2End,

    /// <summary>The id is not an id of the format, or the symbol or context timeframe cannot form one.</summary>
    InvalidId,

    /// <summary>The file of the id exists but cannot be read, so its unknown fields could not be kept; it is left as it is.</summary>
    UnreadableFile
}
