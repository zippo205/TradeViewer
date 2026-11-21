using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TradeViewer.Contracts.Scenarios;

namespace TradeViewer.Infrastructure.Scenarios;

/// <summary>
/// The expert file and the record (docs/knowledge/ewts-interface.md, "Expert Scenario Files"). Writing lays the
/// fields of the record over the JSON of the file: a field the record has replaces the file's, a field the record
/// has as null is removed, every other field (legacy, later additions) stays where it is, at every depth. The items
/// of exits and stopMoves keep their unknown fields by position; drawings belong to TradeViewer and are replaced.
/// </summary>
public static class ExpertScenarioJson
{
    public const string Format = "ewts-expert-scenario";
    public const int Version = 1;

    private static readonly HashSet<string> ArraysMergedByPosition = new HashSet<string>(StringComparer.Ordinal) { "exits", "stopMoves" };

    private static readonly JsonSerializerOptions Read = Options(JsonIgnoreCondition.Never);

    /// <summary>Nulls are written, so the merge sees which known fields are gone.</summary>
    private static readonly JsonSerializerOptions WriteAll = Options(JsonIgnoreCondition.Never);

    /// <summary>Drawings are TradeViewer's own: written without empty parts.</summary>
    private static readonly JsonSerializerOptions WriteCompact = Options(JsonIgnoreCondition.WhenWritingNull);

    /// <summary>Indented by two like EWTS's files; text such as "->" or "≥" is written as it is, not escaped.</summary>
    private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>The JSON of a file, or the reason it is not an expert scenario of this version.</summary>
    public static JsonObject? Parse(string text, out string? error)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text);
        }
        catch (JsonException exception)
        {
            error = "Not valid JSON: " + exception.Message;
            return null;
        }

        if (node is not JsonObject file)
        {
            error = "Not a JSON object.";
            return null;
        }

        var format = (file["format"] as JsonValue)?.TryGetValue(out string? f) == true ? f : null;
        if (format != Format)
        {
            error = "Unknown format \"" + (format ?? "none") + "\"; expected \"" + Format + "\".";
            return null;
        }

        if ((file["version"] as JsonValue)?.TryGetValue(out int version) != true || version != Version)
        {
            error = "Unknown version " + (file["version"]?.ToJsonString() ?? "none") + "; expected " + Version + ".";
            return null;
        }

        error = null;
        return file;
    }

    /// <summary>The record of a parsed file, or the reason it cannot be one. The id is the file's name.</summary>
    public static ExpertScenarioRecord? ToRecord(JsonObject file, string id, out string? error)
    {
        ExpertScenarioRecord? record;
        try
        {
            record = file.Deserialize<ExpertScenarioRecord>(Read);
        }
        catch (JsonException exception)
        {
            error = "A field has the wrong form: " + exception.Message;
            return null;
        }

        if (record == null)
        {
            error = "Empty file.";
            return null;
        }

        var missing = new[] { ("source", record.Source), ("symbol", record.Symbol), ("contextTimeframe", record.ContextTimeframe), ("direction", record.Direction) }
            .Where(field => string.IsNullOrWhiteSpace(field.Item2))
            .Select(field => field.Item1)
            .ToList();
        if (missing.Count > 0)
        {
            error = "Missing " + string.Join(", ", missing) + ".";
            return null;
        }

        if (record.Id != null && record.Id != id)
        {
            error = "The id \"" + record.Id + "\" differs from the file name.";
            return null;
        }

        error = null;
        return record with { Id = id };
    }

    /// <summary>The text of the file after the save: the record laid over the existing file, or a new file.</summary>
    public static string ToText(ExpertScenarioRecord record, JsonObject? existing)
    {
        var file = existing ?? new JsonObject { ["format"] = Format, ["version"] = Version };
        file["format"] = Format;
        file["version"] = Version;

        var known = (JsonObject)JsonSerializer.SerializeToNode(record with { Drawings = null }, WriteAll)!;
        known["drawings"] = record.Drawings is { Count: > 0 } ? JsonSerializer.SerializeToNode(record.Drawings, WriteCompact) : null;
        Merge(file, known);

        return file.ToJsonString(Indented) + "\n";
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (name, value) in source.ToList())
        {
            var current = target[name];
            if (value is JsonValue && current is JsonValue && JsonNode.DeepEquals(value, current))
            {
                continue; // the same value keeps its text (62275.0 stays 62275.0), so an unchanged field is no change in a diff
            }

            if (value == null)
            {
                target.Remove(name);
            }
            else if (value is JsonObject sourceObject && current is JsonObject targetObject)
            {
                Merge(targetObject, sourceObject);
            }
            else if (value is JsonArray sourceArray && current is JsonArray targetArray && ArraysMergedByPosition.Contains(name))
            {
                target[name] = MergeByPosition(targetArray, sourceArray);
            }
            else
            {
                target[name] = WithoutNulls(value);
            }
        }
    }

    /// <summary>A section or point new to the file: written without its empty fields, as a file written by hand would be.</summary>
    private static JsonNode WithoutNulls(JsonNode value)
    {
        switch (value)
        {
            case JsonObject source:
                var created = new JsonObject();
                Merge(created, source);
                return created;
            case JsonArray items:
                var array = new JsonArray();
                foreach (var item in items)
                {
                    array.Add(item == null ? null : WithoutNulls(item));
                }

                return array;
            default:
                return value.DeepClone();
        }
    }

    private static JsonArray MergeByPosition(JsonArray target, JsonArray source)
    {
        var merged = new JsonArray();
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (item is JsonObject sourceItem && i < target.Count && target[i] is JsonObject targetItem)
            {
                var kept = (JsonObject)targetItem.DeepClone();
                Merge(kept, sourceItem);
                merged.Add(kept);
            }
            else
            {
                merged.Add(item == null ? null : WithoutNulls(item));
            }
        }

        return merged;
    }

    private static JsonSerializerOptions Options(JsonIgnoreCondition ignore)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = ignore };
        options.Converters.Add(new UtcTimeConverter());
        return options;
    }

    /// <summary>Times as EWTS writes them: UTC with Z, whole seconds unless the time has a fraction.</summary>
    private sealed class UtcTimeConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            var utc = value.UtcDateTime;
            var format = utc.Ticks % TimeSpan.TicksPerSecond == 0 ? "yyyy-MM-dd'T'HH:mm:ss'Z'" : "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
            writer.WriteStringValue(utc.ToString(format, CultureInfo.InvariantCulture));
        }
    }
}
