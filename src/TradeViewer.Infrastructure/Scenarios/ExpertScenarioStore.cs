using System.Text.Json.Nodes;
using TradeViewer.Application.Abstractions.Ewts;
using TradeViewer.Contracts.Scenarios;
using TradeViewer.Domain.Scenarios;
using TradeViewer.Infrastructure.Storage;

namespace TradeViewer.Infrastructure.Scenarios;

/// <summary>
/// The expert scenarios as files in <c>&lt;Scenarios:RootPath&gt;/expert/</c> (architecture, chapter 8). Every request
/// reads the files, so a file changed by hand or copied in is seen at once (SC5); the heads of the list are kept by
/// file size and modified time. Writes go to <c>&lt;id&gt;.json.part</c> and are then renamed, all under one lock, so
/// two windows can neither take the same id nor interleave their writes. Nothing outside <c>expert/</c> is touched.
/// </summary>
public sealed class ExpertScenarioStore : IExpertScenarioStore
{
    public const string FolderName = "expert";
    private const string Extension = ".json";
    private const string PartExtension = ".part";

    private readonly string? _folder;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
    private readonly object _cacheLock = new object();
    private readonly Dictionary<string, CachedHead> _heads = new Dictionary<string, CachedHead>(StringComparer.Ordinal);

    public ExpertScenarioStore(StorageLocations locations, TimeProvider clock)
        : this(locations.ScenariosRoot, clock)
    {
    }

    /// <param name="scenariosRoot">the scenario folder, or null when the feature is off (then there are no scenarios)</param>
    public ExpertScenarioStore(string? scenariosRoot, TimeProvider clock)
    {
        _folder = scenariosRoot == null ? null : Path.Combine(scenariosRoot, FolderName);
        _clock = clock;
    }

    public async Task<IReadOnlyList<ExpertScenarioSummary>> ListAsync(CancellationToken cancellationToken)
    {
        if (_folder == null || !Directory.Exists(_folder))
        {
            return Array.Empty<ExpertScenarioSummary>();
        }

        var files = new DirectoryInfo(_folder).GetFiles("*" + Extension);
        var heads = new List<ExpertScenarioSummary>(files.Length);
        foreach (var file in files)
        {
            heads.Add(await HeadOfAsync(file, cancellationToken));
        }

        lock (_cacheLock)
        {
            foreach (var gone in _heads.Keys.Except(files.Select(file => file.Name)).ToList())
            {
                _heads.Remove(gone);
            }
        }

        return heads.OrderBy(head => head.Id, StringComparer.Ordinal).ToList();
    }

    public async Task<ExpertScenarioRead> ReadAsync(string id, CancellationToken cancellationToken)
    {
        var path = PathOf(id);
        if (path == null || !File.Exists(path))
        {
            return ExpertScenarioRead.NotFound;
        }

        var text = await ReadTextAsync(path, cancellationToken);
        if (text == null)
        {
            return ExpertScenarioRead.NotFound;
        }

        var file = ExpertScenarioJson.Parse(text, out var error);
        var record = file == null ? null : ExpertScenarioJson.ToRecord(file, id, out error);
        return record == null ? ExpertScenarioRead.Broken(error!) : ExpertScenarioRead.Of(record);
    }

    public async Task<ExpertScenarioSave> SaveAsync(ExpertScenarioRecord scenario, CancellationToken cancellationToken)
    {
        if (_folder == null)
        {
            throw new InvalidOperationException("The scenario folder is not configured (Scenarios:RootPath).");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_folder);
            var now = WholeSecond(_clock.GetUtcNow());
            return scenario.Id == null
                ? await CreateAsync(scenario, now, cancellationToken)
                : await UpdateAsync(scenario, now, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var path = PathOf(id);
        if (path == null)
        {
            return false;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<ExpertScenarioSave> CreateAsync(ExpertScenarioRecord scenario, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var wave2End = scenario.Waves?.Wave2End;
        if (wave2End == null)
        {
            return ExpertScenarioSave.Refused(ExpertScenarioSaveProblem.NoWave2End);
        }

        var prefix = ExpertScenarioId.Prefix(scenario.Symbol, scenario.ContextTimeframe, wave2End.Time);
        var taken = Directory.GetFiles(_folder!, "*" + Extension).Select(file => Path.GetFileNameWithoutExtension(file)).ToList();
        while (true)
        {
            var id = ExpertScenarioId.Next(prefix, taken);
            if (!ExpertScenarioId.IsValid(id))
            {
                return ExpertScenarioSave.Refused(ExpertScenarioSaveProblem.InvalidId);
            }

            var saved = scenario with { Id = id, CreatedAt = scenario.CreatedAt ?? now, UpdatedAt = now };
            if (await WriteAsync(PathOf(id)!, ExpertScenarioJson.ToText(saved, null), overwrite: false, cancellationToken))
            {
                return ExpertScenarioSave.Of(saved);
            }

            taken.Add(id); // made meanwhile by someone else (EWTS's copy, a hand-made file): take the next number
        }
    }

    private async Task<ExpertScenarioSave> UpdateAsync(ExpertScenarioRecord scenario, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var path = PathOf(scenario.Id!);
        if (path == null)
        {
            return ExpertScenarioSave.Refused(ExpertScenarioSaveProblem.InvalidId);
        }

        JsonObject? existing = null;
        var text = File.Exists(path) ? await ReadTextAsync(path, cancellationToken) : null;
        if (text != null)
        {
            existing = ExpertScenarioJson.Parse(text, out _);
            if (existing == null)
            {
                return ExpertScenarioSave.Refused(ExpertScenarioSaveProblem.UnreadableFile);
            }
        }

        var createdAt = scenario.CreatedAt ?? CreatedAtOf(existing) ?? now;
        var saved = scenario with { CreatedAt = createdAt, UpdatedAt = now };
        await WriteAsync(path, ExpertScenarioJson.ToText(saved, existing), overwrite: true, cancellationToken);
        return ExpertScenarioSave.Of(saved);
    }

    /// <returns>False when the file exists and overwrite is false; nothing is changed then.</returns>
    private static async Task<bool> WriteAsync(string path, string text, bool overwrite, CancellationToken cancellationToken)
    {
        var part = path + PartExtension;
        await using (var stream = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var writer = new StreamWriter(stream))
        {
            await writer.WriteAsync(text.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        try
        {
            File.Move(part, path, overwrite);
            return true;
        }
        catch (IOException) when (!overwrite && File.Exists(path))
        {
            File.Delete(part);
            return false;
        }
    }

    private async Task<ExpertScenarioSummary> HeadOfAsync(FileInfo file, CancellationToken cancellationToken)
    {
        lock (_cacheLock)
        {
            if (_heads.TryGetValue(file.Name, out var cached) && cached.Length == file.Length && cached.Modified == file.LastWriteTimeUtc)
            {
                return cached.Head;
            }
        }

        var id = Path.GetFileNameWithoutExtension(file.Name);
        var head = await ReadHeadAsync(file.FullName, id, cancellationToken);
        lock (_cacheLock)
        {
            _heads[file.Name] = new CachedHead(file.Length, file.LastWriteTimeUtc, head);
        }

        return head;
    }

    private static async Task<ExpertScenarioSummary> ReadHeadAsync(string path, string id, CancellationToken cancellationToken)
    {
        if (!ExpertScenarioId.IsValid(id))
        {
            return Broken(id, "The file name is not an id of the format (<symbol>-<context tf>-<yyyy-MM>-X<n>.json).");
        }

        var text = await ReadTextAsync(path, cancellationToken);
        if (text == null)
        {
            return Broken(id, "The file is gone or cannot be opened.");
        }

        var file = ExpertScenarioJson.Parse(text, out var error);
        var record = file == null ? null : ExpertScenarioJson.ToRecord(file, id, out error);
        if (record == null)
        {
            return Broken(id, error!);
        }

        return new ExpertScenarioSummary(id, record.Source, record.Symbol, record.ContextTimeframe, record.Verdict, record.Draft, record.From, record.To, record.UpdatedAt);
    }

    private static ExpertScenarioSummary Broken(string id, string error)
    {
        return new ExpertScenarioSummary(id, null, null, null, null, false, null, null, null, error);
    }

    private static async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTimeOffset? CreatedAtOf(JsonObject? file)
    {
        var value = file?["createdAt"] as JsonValue;
        return value != null && value.TryGetValue(out string? text) && DateTimeOffset.TryParse(text, out var time) ? time.ToUniversalTime() : null;
    }

    private static DateTimeOffset WholeSecond(DateTimeOffset time)
    {
        return new DateTimeOffset(time.UtcTicks - (time.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }

    /// <summary>The path of an id inside expert/, or null for anything that is not an id (no request leaves the folder).</summary>
    private string? PathOf(string id)
    {
        return _folder != null && ExpertScenarioId.IsValid(id) ? Path.Combine(_folder, id + Extension) : null;
    }

    private sealed record CachedHead(long Length, DateTime Modified, ExpertScenarioSummary Head);
}
