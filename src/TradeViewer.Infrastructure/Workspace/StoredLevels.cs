using System.Text.Json;
using TradeViewer.Domain.Drawings;

namespace TradeViewer.Infrastructure.Workspace;

/// <summary>
/// The Fibonacci levels of a stored drawing or preset: a JSON array of <c>{ "ratio": 0.618, "visible": true }</c>.
/// A plain number is read too, as a level that is shown: that is how levels were stored before they could be
/// switched off, and what was stored must stay readable (priority 1).
/// </summary>
internal static class StoredLevels
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public static JsonElement? Write(IReadOnlyList<FibonacciLevel>? levels)
    {
        return levels == null ? null : JsonSerializer.SerializeToElement(levels.Select(level => new Stored(level.Ratio, level.Visible)), Json);
    }

    public static string WriteText(IReadOnlyList<FibonacciLevel> levels)
    {
        return Write(levels)!.Value.GetRawText();
    }

    /// <returns>The levels, null when there are none, or null with <paramref name="readable"/> false when the JSON is not a list of levels.</returns>
    public static List<FibonacciLevel>? Read(JsonElement? element, out bool readable)
    {
        readable = true;
        if (element == null || element.Value.ValueKind == JsonValueKind.Null || element.Value.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.Value.ValueKind != JsonValueKind.Array)
        {
            readable = false;
            return null;
        }

        var levels = new List<FibonacciLevel>();
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number)
            {
                levels.Add(new FibonacciLevel(item.GetDouble(), true));
            }
            else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("ratio", out var ratio) && ratio.ValueKind == JsonValueKind.Number)
            {
                var visible = !item.TryGetProperty("visible", out var shown) || shown.ValueKind != JsonValueKind.False;
                levels.Add(new FibonacciLevel(ratio.GetDouble(), visible));
            }
            else
            {
                readable = false;
                return null;
            }
        }

        return levels;
    }

    public static List<FibonacciLevel>? ReadText(string json, out bool readable)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Read(document.RootElement.Clone(), out readable);
        }
        catch (JsonException)
        {
            readable = false;
            return null;
        }
    }

    private sealed record Stored(double Ratio, bool Visible);
}
