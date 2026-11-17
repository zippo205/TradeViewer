using System.Text.Json;
using TradeViewer.Contracts.Drawings;
using TradeViewer.Web.Drawings;

namespace TradeViewer.UnitTests.Web;

/// <summary>The way of a drawing to the chart module and back: nothing is lost, times become Unix seconds.</summary>
public class ChartDrawingsTests
{
    [Fact]
    public void A_record_comes_back_from_the_chart_as_it_went()
    {
        var record = new DrawingRecord(
            Guid.NewGuid(),
            "binance",
            "BTCUSDT",
            "polyline",
            1,
            true,
            new[] { new AnchorRecord(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero), 100.5), new AnchorRecord(new DateTimeOffset(2026, 11, 15, 14, 0, 0, TimeSpan.Zero), 99) },
            new DrawingStyleRecord("#2962FF", 2, "dashed"),
            "count",
            null,
            null,
            new[] { new PointLabelRecord(1, new WaveMarkRecord("minor", "1", "above")) },
            true);

        var chart = ChartDrawings.ToChart(record);
        var back = ChartDrawings.ToRecord(chart, "other", "OTHER");

        Assert.Equal(1_794_736_800, chart.Anchors[0].Time);
        Assert.Equal(JsonSerializer.Serialize(record), JsonSerializer.Serialize(back));
    }

    [Fact]
    public void A_drawing_made_in_the_chart_gets_the_asset_shown_and_the_current_payload_version()
    {
        var made = new ChartDrawing(Guid.NewGuid().ToString(), "ray", false, new[] { new ChartAnchor(1_794_736_800, 10) }, new DrawingStyleRecord("#2962FF", 2, "solid"));

        var record = ChartDrawings.ToRecord(made, "binance", "ETHUSDT")!;

        Assert.Equal(("binance", "ETHUSDT", 1, false), (record.Source, record.Symbol, record.Version, record.LineHidden));
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero), record.Anchors[0].Time);
    }

    [Fact]
    public void An_id_that_is_not_a_UUID_is_not_taken()
    {
        var made = new ChartDrawing("preview", "ray", false, Array.Empty<ChartAnchor>(), new DrawingStyleRecord("#2962FF", 2, "solid"));

        Assert.Null(ChartDrawings.ToRecord(made, "binance", "BTCUSDT"));
    }

    [Fact]
    public void The_chart_module_reads_the_camel_case_names_Blazor_writes()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = "{\"id\":\"8d2c1b8e-3b0a-4a5e-9c3e-0f1f2a3b4c5d\",\"tool\":\"textNote\",\"locked\":false,"
            + "\"anchors\":[{\"time\":1794736800,\"price\":5}],\"style\":{\"color\":\"#2962FF\",\"width\":2,\"lineStyle\":\"solid\"},\"text\":\"Text\"}";

        var drawing = JsonSerializer.Deserialize<ChartDrawing>(json, options)!;

        Assert.Equal("Text", drawing.Text);
        Assert.Null(drawing.Style.Fill);
        Assert.Equal(1_794_736_800, drawing.Anchors[0].Time);
    }
}
