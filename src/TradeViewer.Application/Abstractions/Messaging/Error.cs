namespace TradeViewer.Application.Abstractions.Messaging;

/// <summary>An expected failure of a handler. Handlers return it in a <see cref="Result{T}"/>; they do not throw.</summary>
public sealed class Error
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFields = new Dictionary<string, string[]>();

    public Error(ErrorType type, string code, string message, IReadOnlyDictionary<string, string[]>? fields = null)
    {
        Type = type;
        Code = code;
        Message = message;
        Fields = fields ?? NoFields;
    }

    public ErrorType Type { get; }

    /// <summary>A short stable name, e.g. <c>asset.notFound</c>.</summary>
    public string Code { get; }

    public string Message { get; }

    /// <summary>For a validation error: the messages per field name.</summary>
    public IReadOnlyDictionary<string, string[]> Fields { get; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? fields = null)
    {
        return new Error(ErrorType.Validation, code, message, fields);
    }

    public static Error NotFound(string code, string message)
    {
        return new Error(ErrorType.NotFound, code, message);
    }

    public static Error Conflict(string code, string message)
    {
        return new Error(ErrorType.Conflict, code, message);
    }

    public static Error InvalidData(string code, string message)
    {
        return new Error(ErrorType.InvalidData, code, message);
    }

    public static Error Unavailable(string code, string message)
    {
        return new Error(ErrorType.Unavailable, code, message);
    }
}
