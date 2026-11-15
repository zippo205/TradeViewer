using TradeViewer.Infrastructure.Sqlite;

namespace TradeViewer.Infrastructure.MarketData.Store;

/// <summary>The schema scripts of marketdata.db (MarketData/Store/Scripts/NNNN_name.sql).</summary>
public sealed class MarketDataSchema : SqliteSchema
{
    public MarketDataSchema(IEnumerable<SchemaScript> scripts)
        : base("The candle store", scripts)
    {
    }

    public static MarketDataSchema FromEmbeddedScripts()
    {
        return new MarketDataSchema(ReadEmbeddedScripts(typeof(MarketDataSchema).Namespace + ".Scripts."));
    }
}
