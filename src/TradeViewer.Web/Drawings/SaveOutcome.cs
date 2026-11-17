namespace TradeViewer.Web.Drawings;

public enum SaveResult
{
    /// <summary>Stored (or, for a delete, gone already).</summary>
    Saved,

    /// <summary>The server could not be reached or had a passing problem: the save is tried again.</summary>
    Retry,

    /// <summary>The server refused the record (e.g. it breaks a rule); trying again would not help.</summary>
    Refused
}

/// <summary>What became of one save of a drawing, with the message to show when it did not work.</summary>
public sealed record SaveOutcome(SaveResult Result, string? Message = null)
{
    public static SaveOutcome Saved { get; } = new SaveOutcome(SaveResult.Saved);
}
