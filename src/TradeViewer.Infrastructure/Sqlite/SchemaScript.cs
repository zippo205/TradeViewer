namespace TradeViewer.Infrastructure.Sqlite;

/// <summary>One numbered schema script; its number is the schema version it leads to.</summary>
public sealed class SchemaScript
{
    public SchemaScript(int version, string name, string sql)
    {
        Version = version;
        Name = name;
        Sql = sql;
    }

    public int Version { get; }

    public string Name { get; }

    public string Sql { get; }
}
