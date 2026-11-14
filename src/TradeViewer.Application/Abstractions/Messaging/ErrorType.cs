namespace TradeViewer.Application.Abstractions.Messaging;

/// <summary>The kind of an expected failure; the Api maps it to an HTTP status (architecture, chapter 4).</summary>
public enum ErrorType
{
    /// <summary>Invalid input (400).</summary>
    Validation,

    /// <summary>An unknown asset, instance or other resource (404).</summary>
    NotFound,

    /// <summary>The request conflicts with the current state, e.g. the asset already exists (409).</summary>
    Conflict,

    /// <summary>A file or an upstream answer breaks its contract (422).</summary>
    InvalidData,

    /// <summary>The Downloader or an EWTS instance cannot be reached (503).</summary>
    Unavailable
}
