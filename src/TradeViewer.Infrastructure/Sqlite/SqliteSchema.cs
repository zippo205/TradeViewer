using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TradeViewer.Infrastructure.Sqlite;

/// <summary>
/// The numbered SQL scripts of one database (embedded resources NNNN_name.sql), applied in order, one
/// transaction each. The number of the last applied script is stored in PRAGMA user_version.
/// </summary>
public class SqliteSchema
{
    private readonly string _databaseName;
    private readonly IReadOnlyList<SchemaScript> _scripts;

    /// <param name="databaseName">How messages call the database, e.g. "The workspace".</param>
    /// <param name="scripts">The scripts of the build.</param>
    public SqliteSchema(string databaseName, IEnumerable<SchemaScript> scripts)
    {
        _databaseName = databaseName;
        _scripts = scripts.OrderBy(script => script.Version).ToList();

        // The numbers are 1, 2, 3, ...: a gap or a double number is a mistake in the build, never a state of a database.
        for (var index = 0; index < _scripts.Count; index++)
        {
            if (_scripts[index].Version != index + 1)
            {
                throw new InvalidOperationException(
                    "The schema scripts must be numbered 1, 2, 3, ... without gaps; found " + _scripts[index].Name + " at position " + (index + 1) + ".");
            }
        }
    }

    public IReadOnlyList<SchemaScript> Scripts => _scripts;

    public int LatestVersion => _scripts.Count;

    /// <summary>The embedded scripts whose resource names start with the prefix (the namespace of their folder and a dot).</summary>
    public static IReadOnlyList<SchemaScript> ReadEmbeddedScripts(string resourcePrefix)
    {
        var assembly = typeof(SqliteSchema).Assembly;
        var scripts = new List<SchemaScript>();

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(resourcePrefix, StringComparison.Ordinal) || !resource.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var name = resource.Substring(resourcePrefix.Length);
            var version = int.Parse(name.Substring(0, 4), CultureInfo.InvariantCulture);

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            scripts.Add(new SchemaScript(version, name, reader.ReadToEnd()));
        }

        return scripts;
    }

    public static int ReadVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>True when the database was written by a newer build; such a file is never opened for work.</summary>
    public bool IsNewer(int version)
    {
        return version > LatestVersion;
    }

    /// <summary>Applies the scripts the database has not seen. A failing script leaves the database at the version before it.</summary>
    /// <returns>The schema version after the upgrade.</returns>
    public int Apply(SqliteConnection connection)
    {
        var current = ReadVersion(connection);
        if (IsNewer(current))
        {
            throw new InvalidOperationException(NewerMessage(current));
        }

        foreach (var script in _scripts)
        {
            if (script.Version <= current)
            {
                continue;
            }

            using var transaction = connection.BeginTransaction();
            Execute(connection, transaction, script.Sql);
            Execute(connection, transaction, "PRAGMA user_version = " + script.Version.ToString(CultureInfo.InvariantCulture) + ";");
            transaction.Commit();
        }

        return ReadVersion(connection);
    }

    public string NewerMessage(int version)
    {
        return _databaseName + " has schema version " + version + ", but this version of TradeViewer only knows version " + LatestVersion
            + ". Run a newer version of TradeViewer.";
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
