using System.Text.Json;
using TradeViewer.Domain.Drawings;

namespace TradeViewer.Infrastructure.Workspace;

/// <summary>
/// The JSON payload of a row of the drawings table (architecture chapter 11). Id, asset, tool and payload
/// version are columns; everything else is here. The stored shape is its own, so the Domain and the API can
/// change without touching stored data. Codes are those of <see cref="DrawingCodes"/>; times are Unix seconds.
/// </summary>
internal static class DrawingPayload
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Write(Drawing drawing)
    {
        var payload = new StoredDrawing(
            drawing.Locked,
            drawing.Anchors.Select(anchor => new StoredAnchor(anchor.Time, anchor.Price)).ToList(),
            new StoredStyle(drawing.Style.Color, drawing.Style.Width, DrawingCodes.Code(drawing.Style.LineStyle), drawing.Style.Fill),
            drawing.Text,
            StoredLevels.Write(drawing.Levels),
            drawing.Wave == null ? null : Stored(drawing.Wave),
            drawing.PointLabels?.Select(label => new StoredPointLabel(label.Point, Stored(label.Mark))).ToList(),
            drawing.LineHidden ? true : null);

        return JsonSerializer.Serialize(payload, Json);
    }

    /// <returns>The drawing, or null when the payload cannot be read by this build (the row stays as it is).</returns>
    public static Drawing? Read(Guid id, string source, string symbol, string toolCode, int version, string json)
    {
        if (!DrawingCodes.TryParse(toolCode, out DrawingTool tool) || version > DrawingTools.PayloadVersion(tool))
        {
            return null;
        }

        StoredDrawing? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StoredDrawing>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload?.Anchors == null || payload.Style == null || !DrawingCodes.TryParse(payload.Style.LineStyle, out LineStyle lineStyle))
        {
            return null;
        }

        WaveMark? wave = null;
        if (payload.Wave != null && !TryRead(payload.Wave, out wave))
        {
            return null;
        }

        var levels = StoredLevels.Read(payload.Levels, out var levelsReadable);
        if (!levelsReadable)
        {
            return null;
        }

        List<PointLabel>? pointLabels = null;
        if (payload.PointLabels != null)
        {
            pointLabels = new List<PointLabel>();
            foreach (var label in payload.PointLabels)
            {
                if (label?.Mark == null || !TryRead(label.Mark, out var mark))
                {
                    return null;
                }

                pointLabels.Add(new PointLabel(label.Point, mark!));
            }
        }

        return new Drawing(
            id,
            source,
            symbol,
            tool,
            version,
            payload.Locked,
            payload.Anchors.Select(anchor => new Anchor(anchor.Time, anchor.Price)).ToList(),
            new DrawingStyle(payload.Style.Color, payload.Style.Width, lineStyle, payload.Style.Fill),
            payload.Text,
            levels,
            wave,
            pointLabels,
            payload.LineHidden == true);
    }

    private static StoredMark Stored(WaveMark mark)
    {
        return new StoredMark(DrawingCodes.Code(mark.Degree), mark.Label, DrawingCodes.Code(mark.Side));
    }

    private static bool TryRead(StoredMark stored, out WaveMark? mark)
    {
        mark = null;
        if (!DrawingCodes.TryParse(stored.Degree, out WaveDegree degree) || !DrawingCodes.TryParse(stored.Side, out WaveSide side) || stored.Label == null)
        {
            return false;
        }

        mark = new WaveMark(degree, stored.Label, side);
        return true;
    }

    private sealed record StoredDrawing(
        bool Locked,
        List<StoredAnchor>? Anchors,
        StoredStyle? Style,
        string? Text,
        JsonElement? Levels,
        StoredMark? Wave,
        List<StoredPointLabel>? PointLabels,
        bool? LineHidden);

    private sealed record StoredAnchor(long Time, double? Price);

    private sealed record StoredStyle(string Color, int Width, string LineStyle, string? Fill);

    private sealed record StoredMark(string Degree, string Label, string Side);

    private sealed record StoredPointLabel(int Point, StoredMark Mark);
}
