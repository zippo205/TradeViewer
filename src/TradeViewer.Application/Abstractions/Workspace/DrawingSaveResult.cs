namespace TradeViewer.Application.Abstractions.Workspace;

/// <summary>What became of a save of a drawing.</summary>
public enum DrawingSaveResult
{
    Saved,

    /// <summary>The asset of the drawing is not in the workspace; nothing was stored.</summary>
    UnknownAsset,

    /// <summary>The id belongs to a drawing of another asset; a drawing never moves to another asset. Nothing was stored.</summary>
    OtherAsset
}
