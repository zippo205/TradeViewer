using TradeViewer.Infrastructure.Sqlite;
using TradeViewer.Infrastructure.Workspace;

namespace TradeViewer.UnitTests.Infrastructure.Workspace;

public class WorkspaceSchemaTests
{
    [Fact]
    public void The_embedded_scripts_are_numbered_without_gaps()
    {
        var schema = WorkspaceSchema.FromEmbeddedScripts();

        Assert.True(schema.LatestVersion >= 1);
        Assert.Equal(Enumerable.Range(1, schema.LatestVersion), schema.Scripts.Select(script => script.Version));
        Assert.StartsWith("0001_", schema.Scripts[0].Name);
    }

    [Fact]
    public void Scripts_are_applied_in_the_order_of_their_numbers()
    {
        var schema = new WorkspaceSchema(new[] { Script(2), Script(1), Script(3) });

        Assert.Equal(new[] { 1, 2, 3 }, schema.Scripts.Select(script => script.Version));
        Assert.Equal(3, schema.LatestVersion);
    }

    [Fact]
    public void A_gap_in_the_numbers_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => new WorkspaceSchema(new[] { Script(1), Script(3) }));
    }

    [Fact]
    public void A_double_number_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => new WorkspaceSchema(new[] { Script(1), Script(1) }));
    }

    [Fact]
    public void Only_a_higher_version_than_the_last_script_is_newer()
    {
        var schema = new WorkspaceSchema(new[] { Script(1), Script(2) });

        Assert.False(schema.IsNewer(0));
        Assert.False(schema.IsNewer(2));
        Assert.True(schema.IsNewer(3));
    }

    private static SchemaScript Script(int version)
    {
        return new SchemaScript(version, version.ToString("0000") + "_test.sql", "SELECT 1;");
    }
}
